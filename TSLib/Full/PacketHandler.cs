// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using NLog;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TSLib.Helper;
using static TSLib.Full.PacketHandlerConst;

namespace TSLib.Full
{
	internal sealed class PacketHandler<TIn, TOut>
	{
		private static readonly int OutHeaderSize = TsCrypt.MacLen + Packet<TOut>.HeaderLength;
		private static readonly int MaxOutContentSize = MaxOutPacketSize - OutHeaderSize;

		// Timout calculations
		/// <summary>The SmoothedRoundTripTime holds the smoothed average time
		/// it takes for a packet to get ack'd.</summary>
		private TimeSpan smoothedRtt;
		/// <summary>Holds the smoothed rtt variation.</summary>
		private TimeSpan smoothedRttVar;
		/// <summary>Holds the current RetransmissionTimeOut, which determines the timespan until
		/// a packet is considered to be lost.</summary>
		private TimeSpan currentRto;
		private readonly Stopwatch pingTimer = new Stopwatch();
		private readonly Stopwatch lastMessageTimer = new Stopwatch();
		private ushort lastSentPingId;
		private ushort lastReceivedPingId;

		// Out Packets
		private readonly ushort[] packetCounter;
		private readonly uint[] generationCounter;
		private ResendPacket<TOut> initPacketCheck;
		private readonly Dictionary<ushort, ResendPacket<TOut>> packetAckManager = new Dictionary<ushort, ResendPacket<TOut>>();
		// In Packets
		private readonly GenerationWindow receiveWindowVoice;
		private readonly GenerationWindow receiveWindowVoiceWhisper;
		private readonly RingQueue<Packet<TIn>> receiveQueueCommand;
		private readonly RingQueue<Packet<TIn>> receiveQueueCommandLow;
		// ====
		private readonly object sendLoopLock = new object();
		private readonly TsCrypt tsCrypt;
		private Socket socket;
		private Timer resendTimer;
		private DateTime pingCheck;
		private int pingCheckRunning; // bool
		private readonly Id id; // Log id

		public NetworkStats NetworkStats { get; }

		public ClientId ClientId { get; set; }
		private IPEndPoint remoteAddress;
		private int closed; // bool

		public PacketEvent<TIn> PacketEvent;
		public Action<Reason> StopEvent;

		public PacketHandler(TsCrypt ts3Crypt, Id id)
		{
			receiveQueueCommand = new RingQueue<Packet<TIn>>(ReceivePacketWindowSize, ushort.MaxValue + 1);
			receiveQueueCommandLow = new RingQueue<Packet<TIn>>(ReceivePacketWindowSize, ushort.MaxValue + 1);
			receiveWindowVoice = new GenerationWindow(ushort.MaxValue + 1);
			receiveWindowVoiceWhisper = new GenerationWindow(ushort.MaxValue + 1);

			NetworkStats = new NetworkStats();

			packetCounter = new ushort[TsCrypt.PacketTypeKinds];
			generationCounter = new uint[TsCrypt.PacketTypeKinds];
			this.tsCrypt = ts3Crypt;
			this.id = id;
		}

		public void Connect(IPEndPoint address)
		{
			Initialize(address, true);
			// The old client used to send 'clientinitiv' as the first message.
			// All newer servers still ack it but do not require it anymore.
			// Therefore there is no use in sending it.
			// We still have to increase the packet counter as if we had sent
			//  it because the packed-ids the server expects are fixed.
			IncPacketCounter(PacketType.Command);
			// Send the actual new init packet.
			AddOutgoingPacket(tsCrypt.ProcessInit1<TIn>(null).Value, PacketType.Init1);
		}

		public void Listen(IPEndPoint address)
		{
			lock (sendLoopLock)
			{
				Initialize(address, false);
				// dummy
				initPacketCheck = new ResendPacket<TOut>(new Packet<TOut>(Array.Empty<byte>(), 0, 0, 0))
				{
					FirstSendTime = DateTime.MaxValue,
					LastSendTime = DateTime.MaxValue
				};
			}
		}

		private void Initialize(IPEndPoint address, bool connect)
		{
			lock (sendLoopLock)
			{
				ClientId = default;
				closed = 0;
				smoothedRtt = MaxRetryInterval;
				smoothedRttVar = TimeSpan.Zero;
				currentRto = MaxRetryInterval;
				lastSentPingId = 0;
				lastReceivedPingId = 0;
				lastMessageTimer.Restart();

				initPacketCheck = null;
				packetAckManager.Clear();
				receiveQueueCommand.Clear();
				receiveQueueCommandLow.Clear();
				receiveWindowVoice.Reset();
				receiveWindowVoiceWhisper.Reset();
				Array.Clear(packetCounter, 0, packetCounter.Length);
				Array.Clear(generationCounter, 0, generationCounter.Length);
				NetworkStats.Reset();

				socket?.Dispose();
				try
				{
					if (connect)
					{
						remoteAddress = address;
						socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
						socket.Bind(new IPEndPoint(address.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0));

						var socketEventArgs = new SocketAsyncEventArgs();
						socketEventArgs.SetBuffer(new byte[4096], 0, 4096);
						socketEventArgs.Completed += FetchPacketEvent;
						socketEventArgs.UserToken = this;
						socketEventArgs.RemoteEndPoint = remoteAddress;
						socket.ReceiveFromAsync(socketEventArgs);
					}
					else
					{
						remoteAddress = null;
						socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
						socket.Bind(address);
						// TODO init socketevargs stuff
					}
				}
				catch (SocketException ex) { throw new TsException("Could not connect", ex); }

				pingCheckRunning = 0;
				pingCheck = Tools.Now;
				if (resendTimer == null)
					resendTimer = new Timer((_) => { using (MappedDiagnosticsContext.SetScoped("BotId", id)) ResendLoop(); }, null, ClockResolution, ClockResolution);
			}
		}

		public void Stop(Reason closeReason = Reason.Timeout)
		{
			var wasClosed = Interlocked.Exchange(ref closed, 1);
			if (wasClosed != 0)
				return;
			Log.Debug("Stopping PacketHandler {0}", closeReason);

			lock (sendLoopLock)
			{
				resendTimer?.Dispose();
				socket?.Dispose();
				PacketEvent = null;
			}
			StopEvent?.Invoke(closeReason);
		}

		public E<string> AddOutgoingPacket(ReadOnlySpan<byte> packet, PacketType packetType, PacketFlags addFlags = PacketFlags.None)
		{
			lock (sendLoopLock)
			{
				if (closed != 0)
					return "Connection closed";

				if (NeedsSplitting(packet.Length) && packetType != PacketType.VoiceWhisper)
				{
					// VoiceWhisper packets are excluded for some reason
					if (packetType == PacketType.Voice)
						return "Voice packet too big"; // This happens when a voice packet is bigger than the allowed size

					var tmpCompress = QuickerLz.Compress(packet, 1);
					if (tmpCompress.Length < packet.Length)
					{
						packet = tmpCompress;
						addFlags |= PacketFlags.Compressed;
					}

					if (NeedsSplitting(packet.Length))
					{
						return AddOutgoingSplitData(packet, packetType, addFlags);
					}
				}
				return SendOutgoingData(packet, packetType, addFlags);
			}
		}

		private E<string> AddOutgoingSplitData(ReadOnlySpan<byte> rawData, PacketType packetType, PacketFlags addFlags = PacketFlags.None)
		{
			int pos = 0;
			bool first = true;
			bool last;

			// TODO check if "packBuffer.FreeSlots >= packetSplit.Count"

			do
			{
				int blockSize = Math.Min(MaxOutContentSize, rawData.Length - pos);
				if (blockSize <= 0) break;

				var flags = PacketFlags.None;
				last = pos + blockSize == rawData.Length;
				if (first ^ last)
					flags |= PacketFlags.Fragmented;
				if (first)
				{
					flags |= addFlags;
					first = false;
				}

				var sendResult = SendOutgoingData(rawData.Slice(pos, blockSize), packetType, flags);
				if (!sendResult.Ok)
					return sendResult;

				pos += blockSize;
			} while (!last);

			return R.Ok;
		}

		// is always locked on 'sendLoopLock' from a higher call
		private E<string> SendOutgoingData(ReadOnlySpan<byte> data, PacketType packetType, PacketFlags flags = PacketFlags.None)
		{
			var ids = GetPacketCounter(packetType);
			IncPacketCounter(packetType);

			var packet = new Packet<TOut>(data, packetType, ids.Id, ids.Generation) { PacketType = packetType };
			if (typeof(TOut) == typeof(C2S)) // TODO: XXX
			{
				var meta = (C2S)(object)packet.HeaderExt;
				meta.ClientId = ClientId.Value;
				packet.HeaderExt = (TOut)(object)meta;
			}
			packet.PacketFlags |= flags;

			switch (packet.PacketType)
			{
			case PacketType.Voice:
			case PacketType.VoiceWhisper:
				packet.PacketFlags |= PacketFlags.Unencrypted;
				BinaryPrimitives.WriteUInt16BigEndian(packet.Data, packet.PacketId);
				LogRawVoice.Trace("[O] {0}", packet);
				break;

			case PacketType.Command:
			case PacketType.CommandLow:
				packet.PacketFlags |= PacketFlags.Newprotocol;
				var resendPacket = new ResendPacket<TOut>(packet);
				packetAckManager.Add(packet.PacketId, resendPacket);
				LogRaw.Debug("[O] {0}", packet);
				break;

			case PacketType.Ping:
				lastSentPingId = packet.PacketId;
				packet.PacketFlags |= PacketFlags.Unencrypted;
				LogRaw.Trace("[O] Ping {0}", packet.PacketId);
				break;

			case PacketType.Pong:
				packet.PacketFlags |= PacketFlags.Unencrypted;
				LogRaw.Trace("[O] Pong {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
				break;

			case PacketType.Ack:
				LogRaw.Debug("[O] Acking Ack: {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
				break;

			case PacketType.AckLow:
				packet.PacketFlags |= PacketFlags.Unencrypted;
				LogRaw.Debug("[O] Acking AckLow: {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
				break;

			case PacketType.Init1:
				packet.PacketFlags |= PacketFlags.Unencrypted;
				initPacketCheck = new ResendPacket<TOut>(packet);
				LogRaw.Debug("[O] InitID: {0}", packet.Data[4]);
				LogRaw.Trace("[O] {0}", packet);
				break;

			default: throw Tools.UnhandledDefault(packet.PacketType);
			}

			tsCrypt.Encrypt(ref packet);

			return SendRaw(ref packet);
		}

		private (ushort Id, uint Generation) GetPacketCounter(PacketType packetType)
			=> (packetType != PacketType.Init1)
				? (packetCounter[(int)packetType], generationCounter[(int)packetType])
				: (101, 0);

		private void IncPacketCounter(PacketType packetType)
		{
			unchecked { packetCounter[(int)packetType]++; }
			if (packetCounter[(int)packetType] == 0)
				generationCounter[(int)packetType]++;
		}

		private static bool NeedsSplitting(int dataSize) => dataSize + OutHeaderSize > MaxOutPacketSize;

		private static void FetchPacketEvent(object selfObj, SocketAsyncEventArgs args)
		{
			var self = (PacketHandler<TIn, TOut>)args.UserToken;

			bool isAsync;
			using (MappedDiagnosticsContext.SetScoped("BotId", self.id))
			{
				do
				{
					if (self.closed != 0)
						return;

					if (args.SocketError == SocketError.Success)
					{
						self.FetchPackets(args.Buffer.AsSpan(0, args.BytesTransferred));
					}
					else
					{
						Log.Debug("Socket error: {@args}", args);
						if (args.SocketError == SocketError.ConnectionReset)
						{
							self.Stop();
						}
					}

					lock (self.sendLoopLock)
					{
						if (self.closed != 0)
							return;

						try { isAsync = self.socket.ReceiveFromAsync(args); }
						catch (Exception ex) { Log.Debug(ex, "Error starting socket receive"); return; }
					}
				} while (!isAsync);
			}
		}

		private void FetchPackets(Span<byte> buffer)
		{
			var optpacket = Packet<TIn>.FromRaw(buffer);
			// Invalid packet, ignore
			if (optpacket is null)
			{
				LogRaw.Warn("Dropping invalid packet: {0}", DebugUtil.DebugToHex(buffer));
				return;
			}
			var packet = optpacket.Value;

			// DebugToHex is costly and allocates, precheck before logging
			if (LogRaw.IsTraceEnabled)
				LogRaw.Trace("[I] Raw {0}", DebugUtil.DebugToHex(packet.Raw));

			FindIncommingGenerationId(ref packet);
			if (!tsCrypt.Decrypt(ref packet))
			{
				LogRaw.Warn("Dropping not decryptable packet: {0}", DebugUtil.DebugToHex(packet.Raw));
				return;
			}

			lastMessageTimer.Restart();
			NetworkStats.LogInPacket(ref packet);

			bool passPacketToEvent = true;
			switch (packet.PacketType)
			{
			case PacketType.Voice:
				LogRawVoice.Trace("[I] {0}", packet);
				passPacketToEvent = ReceiveVoice(ref packet, receiveWindowVoice);
				break;
			case PacketType.VoiceWhisper:
				LogRawVoice.Trace("[I] {0}", packet);
				passPacketToEvent = ReceiveVoice(ref packet, receiveWindowVoiceWhisper);
				break;
			case PacketType.Command:
				LogRaw.Debug("[I] {0}", packet);
				passPacketToEvent = ReceiveCommand(ref packet, receiveQueueCommand, PacketType.Ack);
				break;
			case PacketType.CommandLow:
				LogRaw.Debug("[I] {0}", packet);
				passPacketToEvent = ReceiveCommand(ref packet, receiveQueueCommandLow, PacketType.AckLow);
				break;
			case PacketType.Ping:
				LogRaw.Trace("[I] Ping {0}", packet.PacketId);
				ReceivePing(ref packet);
				break;
			case PacketType.Pong:
				LogRaw.Trace("[I] Pong {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
				passPacketToEvent = ReceivePong(ref packet);
				break;
			case PacketType.Ack:
				LogRaw.Debug("[I] Acking: {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
				passPacketToEvent = ReceiveAck(ref packet);
				break;
			case PacketType.AckLow: break;
			case PacketType.Init1:
				if (!LogRaw.IsTraceEnabled) LogRaw.Debug("[I] InitID: {0}", packet.Data[0]);
				if (!LogRaw.IsDebugEnabled) LogRaw.Trace("[I] {0}", packet);
				passPacketToEvent = ReceiveInitAck(ref packet);
				break;
			default: throw Tools.UnhandledDefault(packet.PacketType);
			}

			if (passPacketToEvent)
				PacketEvent?.Invoke(ref packet);
		}

		#region Packet checking
		// These methods are for low level packet processing which the
		// rather high level TS3FullClient should not worry about.

		private void FindIncommingGenerationId(ref Packet<TIn> packet)
		{
			GenerationWindow window;
			switch (packet.PacketType)
			{
			case PacketType.Voice: window = receiveWindowVoice; break;
			case PacketType.VoiceWhisper: window = receiveWindowVoiceWhisper; break;
			case PacketType.Command: window = receiveQueueCommand.Window; break;
			case PacketType.CommandLow: window = receiveQueueCommandLow.Window; break;
			default: return;
			}

			packet.GenerationId = window.GetGeneration(packet.PacketId);
		}

		private bool ReceiveVoice(ref Packet<TIn> packet, GenerationWindow window)
			=> window.SetAndDrag(packet.PacketId);

		private bool ReceiveCommand(ref Packet<TIn> packet, RingQueue<Packet<TIn>> packetQueue, PacketType ackType)
		{
			var setStatus = packetQueue.IsSet(packet.PacketId);

			// Check if we cannot accept this packet since it doesn't fit into the receive window
			if (setStatus == ItemSetStatus.OutOfWindowNotSet)
				return false;

			SendAck(packet.PacketId, ackType);

			// Check if we already have this packet and only need to ack it.
			if (setStatus == ItemSetStatus.InWindowSet || setStatus == ItemSetStatus.OutOfWindowSet)
				return false;

			packetQueue.Set(packet.PacketId, packet);
			while (TryGetCommand(packetQueue, out packet))
				PacketEvent?.Invoke(ref packet);

			return false;
		}

		private static bool TryGetCommand(RingQueue<Packet<TIn>> packetQueue, out Packet<TIn> packet)
		{
			if (packetQueue.Count <= 0) { packet = default; return false; }

			int take = 0;
			int takeLen = 0;
			bool hasStart = false;
			bool hasEnd = false;
			for (int i = 0; i < packetQueue.Count; i++)
			{
				if (packetQueue.TryPeekStart(i, out var peekPacket))
				{
					take++;
					takeLen += peekPacket.Size;
					if (peekPacket.FragmentedFlag)
					{
						if (!hasStart) { hasStart = true; }
						else { hasEnd = true; break; }
					}
					else
					{
						if (!hasStart) { hasStart = true; hasEnd = true; break; }
					}
				}
				else
				{
					break;
				}
			}

			if (!hasStart || !hasEnd) { packet = default; return false; }

			// GET
			if (!packetQueue.TryDequeue(out packet))
				throw new InvalidOperationException("Packet in queue got missing (?)");

			// MERGE
			if (take > 1)
			{
				var preFinalArray = new byte[takeLen];

				// for loop at 0th element
				int curCopyPos = packet.Size;
				packet.Data.CopyTo(preFinalArray.AsSpan(0, packet.Size));

				for (int i = 1; i < take; i++)
				{
					if (!packetQueue.TryDequeue(out var nextPacket))
						throw new InvalidOperationException("Packet in queue got missing (?)");

					nextPacket.Data.CopyTo(preFinalArray.AsSpan(curCopyPos, nextPacket.Size));
					curCopyPos += nextPacket.Size;
				}
				packet.Data = preFinalArray;
			}

			// DECOMPRESS
			if (packet.CompressedFlag)
			{
				try
				{
					packet.Data = QuickerLz.Decompress(packet.Data, MaxDecompressedSize);
				}
				catch (Exception ex)
				{
					LogRaw.Warn(ex, "Got invalid compressed data.");
					return false;
				}
			}
			return true;
		}

		private void SendAck(ushort ackId, PacketType ackType)
		{
			Span<byte> ackData = stackalloc byte[2];
			BinaryPrimitives.WriteUInt16BigEndian(ackData, ackId);
			if (ackType == PacketType.Ack || ackType == PacketType.AckLow)
				AddOutgoingPacket(ackData, ackType);
			else
				throw new InvalidOperationException("Packet type is not an Ack-type");
		}

		private bool ReceiveAck(ref Packet<TIn> packet)
		{
			if (!BinaryPrimitives.TryReadUInt16BigEndian(packet.Data, out var packetId))
				return false;

			lock (sendLoopLock)
			{
				if (packetAckManager.TryGetValue(packetId, out var ackPacket))
				{
					UpdateRto(Tools.Now - ackPacket.LastSendTime);
					packetAckManager.Remove(packetId);
				}
			}
			return true;
		}

		private void SendPing()
		{
			pingTimer.Restart();
			AddOutgoingPacket(Array.Empty<byte>(), PacketType.Ping);
		}

		private void ReceivePing(ref Packet<TIn> packet)
		{
			var idDiff = packet.PacketId - lastReceivedPingId;
			if (idDiff > 1 && idDiff < ReceivePacketWindowSize)
				NetworkStats.LogLostPings(idDiff - 1);
			if (idDiff > 0 || idDiff < -ReceivePacketWindowSize)
				lastReceivedPingId = packet.PacketId;
			Span<byte> pongData = stackalloc byte[2];
			BinaryPrimitives.WriteUInt16BigEndian(pongData, packet.PacketId);
			AddOutgoingPacket(pongData, PacketType.Pong);
		}

		private bool ReceivePong(ref Packet<TIn> packet)
		{
			if (!BinaryPrimitives.TryReadUInt16BigEndian(packet.Data, out var answerId))
				return false;

			if (lastSentPingId == answerId)
			{
				var rtt = pingTimer.Elapsed;
				UpdateRto(rtt);
				NetworkStats.AddPing(rtt);
			}
			return true;
		}

		public void ReceivedFinalInitAck()
		{
			initPacketCheck = null;
		}

		private bool ReceiveInitAck(ref Packet<TIn> packet)
		{
			lock (sendLoopLock)
			{
				if (initPacketCheck is null)
					return true;
				// optional: add random number check from init data
				var forwardData = tsCrypt.ProcessInit1<TIn>(packet.Data);
				if (!forwardData.Ok)
				{
					LogRaw.Debug("Error init: {0}", forwardData.Error);
					return false;
				}
				initPacketCheck = null;
				if (forwardData.Value.Length == 0) // TODO XXX
					return true;
				AddOutgoingPacket(forwardData.Value, PacketType.Init1);
				return true;
			}
		}

		#endregion

		private void UpdateRto(TimeSpan sampleRtt)
		{
			// Timeout calculation (see: https://tools.ietf.org/html/rfc6298)
			// SRTT_{i+1}    = (1-a) * SRTT_i   + a * RTT
			// DevRTT_{i+1}  = (1-b) * DevRTT_i + b * | RTT - SRTT_{i+1} |
			// Timeout_{i+1} = SRTT_{i+1} + max(ClockRes, 4 * DevRTT_{i+1})
			if (smoothedRtt < TimeSpan.Zero)
				smoothedRtt = sampleRtt;
			else
				smoothedRtt = TimeSpan.FromTicks((long)((1 - AlphaSmooth) * smoothedRtt.Ticks + AlphaSmooth * sampleRtt.Ticks));
			smoothedRttVar = TimeSpan.FromTicks((long)((1 - BetaSmooth) * smoothedRttVar.Ticks + BetaSmooth * Math.Abs(sampleRtt.Ticks - smoothedRtt.Ticks)));
			currentRto = smoothedRtt + Tools.Max(ClockResolution, TimeSpan.FromTicks(4 * smoothedRttVar.Ticks));
			LogRtt.Debug("RTT SRTT:{0} RTTVAR:{1} RTO:{2}", smoothedRtt, smoothedRttVar, currentRto);
		}

		/// <summary>
		/// ResendLoop will regularly check if a packet has be acknowleged and trys to send it again
		/// if the timeout for a packet ran out.
		/// </summary>
		private void ResendLoop()
		{
			var wasRunning = Interlocked.Exchange(ref pingCheckRunning, 1);
			if (wasRunning != 0)
			{
				Log.Warn("Previous resend tick didn't finish");
				return;
			}

			try
			{
				bool close = false;
				lock (sendLoopLock)
				{
					if (closed != 0)
						return;

					close = (packetAckManager.Count > 0 && ResendPackets(packetAckManager.Values))
						|| (initPacketCheck != null && ResendPacket(initPacketCheck));
				}
				if (close)
				{
					Stop();
					return;
				}

				var now = Tools.Now;
				var nextTest = now - pingCheck - PingInterval;
				// we need to check if CryptoInitComplete because while false packet ids won't be incremented
				if (nextTest > TimeSpan.Zero && tsCrypt.CryptoInitComplete)
				{
					// Check that the last ping is more than PingInterval but not more than
					// 2*PingInterval away. This might happen for e.g. when the process was
					// suspended. If it was too long ago, reset the ping tick to now.
					if (nextTest > PingInterval)
						pingCheck = now;
					else
						pingCheck += PingInterval;
					SendPing();
				}

				var elapsed = lastMessageTimer.Elapsed;
				if (elapsed > PacketTimeout)
				{
					LogTimeout.Debug("TIMEOUT: Got no ping packet response for {0}", elapsed);
					Stop();
					return;
				}
			}
			finally
			{
				Interlocked.Exchange(ref pingCheckRunning, 0);
			}
		}

		private bool ResendPackets(IEnumerable<ResendPacket<TOut>> packetList)
		{
			foreach (var outgoingPacket in packetList)
				if (ResendPacket(outgoingPacket))
					return true;
			return false;
		}

		private bool ResendPacket(ResendPacket<TOut> packet)
		{
			var now = Tools.Now;
			// Check if the packet timed out completely
			if (packet.FirstSendTime < now - PacketTimeout)
			{
				LogTimeout.Debug("TIMEOUT: {0}", packet);
				return true;
			}

			// Check if we should retransmit a packet because it probably got lost
			if (packet.LastSendTime < now - currentRto)
			{
				LogTimeout.Debug("RESEND: {0}", packet);
				currentRto += currentRto;
				if (currentRto > MaxRetryInterval)
					currentRto = MaxRetryInterval;
				packet.LastSendTime = Tools.Now;
				SendRaw(ref packet.Packet);
			}

			return false;
		}

		private E<string> SendRaw(ref Packet<TOut> packet)
		{
			NetworkStats.LogOutPacket(ref packet);

			// DebugToHex is costly and allocates, precheck before logging
			if (LogRaw.IsTraceEnabled)
				LogRaw.Trace("[O] Raw: {0}", DebugUtil.DebugToHex(packet.Raw));

			try
			{
				socket.SendTo(packet.Raw, packet.Raw.Length, SocketFlags.None, remoteAddress);
				return R.Ok;
			}
			catch (SocketException ex)
			{
				LogRaw.Warn(ex, "Failed to deliver packet (Err:{0})", ex.SocketErrorCode);
				return "Socket send error";
			}
		}
	}

	internal static class PacketHandlerConst
	{
		public static readonly Logger Log = LogManager.GetLogger("TSLib.PacketHandler");
		public static readonly Logger LogRtt = LogManager.GetLogger("TSLib.PacketHandler.Rtt");
		public static readonly Logger LogRaw = LogManager.GetLogger("TSLib.PacketHandler.Raw");
		public static readonly Logger LogRawVoice = LogManager.GetLogger("TSLib.PacketHandler.Raw.Voice");
		public static readonly Logger LogTimeout = LogManager.GetLogger("TSLib.PacketHandler.Timeout");

		/// <summary>Elapsed time since first send timestamp until the connection is considered lost.</summary>
		public static readonly TimeSpan PacketTimeout = TimeSpan.FromSeconds(20);
		/// <summary>Smoothing factor for the SmoothedRtt.</summary>
		public const float AlphaSmooth = 0.125f;
		/// <summary>Smoothing factor for the SmoothedRttDev.</summary>
		public const float BetaSmooth = 0.25f;
		/// <summary>The maximum wait time to retransmit a packet.</summary>
		public static readonly TimeSpan MaxRetryInterval = TimeSpan.FromMilliseconds(1000);
		/// <summary>The timeout check loop interval.</summary>
		public static readonly TimeSpan ClockResolution = TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(1);

		/// <summary>Greatest allowed packet size, including the complete header.</summary>
		public const int MaxOutPacketSize = 500;
		public const int MaxDecompressedSize = 1024 * 1024; // ServerDefault: 40000 (check original code again)
		public const int ReceivePacketWindowSize = 128;
	}

	internal delegate void PacketEvent<TDir>(ref Packet<TDir> packet);
}
