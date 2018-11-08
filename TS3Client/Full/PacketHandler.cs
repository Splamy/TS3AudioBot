// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full
{
	using Helper;
	using NLog;
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;

	internal sealed class PacketHandler<TIn, TOut> : PacketHandler
	{
		/// <summary>Greatest allowed packet size, including the complete header.</summary>
		private const int MaxOutPacketSize = 500;
		private static readonly int OutHeaderSize = Ts3Crypt.MacLen + Packet<TOut>.HeaderLength;
		private static readonly int MaxOutContentSize = MaxOutPacketSize - OutHeaderSize;
		private const int MaxDecompressedSize = 1024 * 1024; // ServerDefault: 40000 (check original code again)
		private const int ReceivePacketWindowSize = 128;

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
		private readonly Dictionary<ushort, ResendPacket<TOut>> packetAckManager;
		// In Packets
		private readonly GenerationWindow receiveWindowVoice;
		private readonly GenerationWindow receiveWindowVoiceWhisper;
		private readonly RingQueue<Packet<TIn>> receiveQueueCommand;
		private readonly RingQueue<Packet<TIn>> receiveQueueCommandLow;
		// ====
		private readonly object sendLoopLock = new object();
		private readonly AutoResetEvent sendLoopPulse = new AutoResetEvent(false);
		private readonly Ts3Crypt ts3Crypt;
		private UdpClient udpClient;
		private int resendThreadId;

		public NetworkStats NetworkStats { get; }

		public ushort ClientId { get; set; }
		private IPEndPoint remoteAddress;
		public Reason? ExitReason { get; set; }
		private bool Closed => ExitReason != null;

		public event PacketEvent<TIn> PacketEvent;

		public PacketHandler(Ts3Crypt ts3Crypt)
		{
			Util.Init(out packetAckManager);
			receiveQueueCommand = new RingQueue<Packet<TIn>>(ReceivePacketWindowSize, ushort.MaxValue + 1);
			receiveQueueCommandLow = new RingQueue<Packet<TIn>>(ReceivePacketWindowSize, ushort.MaxValue + 1);
			receiveWindowVoice = new GenerationWindow(ushort.MaxValue + 1);
			receiveWindowVoiceWhisper = new GenerationWindow(ushort.MaxValue + 1);

			NetworkStats = new NetworkStats();

			packetCounter = new ushort[Ts3Crypt.PacketTypeKinds];
			generationCounter = new uint[Ts3Crypt.PacketTypeKinds];
			this.ts3Crypt = ts3Crypt;
			resendThreadId = -1;
		}

		public void Connect(IPEndPoint address)
		{
			Initialize(address, true);
			// The old client used to send 'clientinitiv' as the first message.
			// All newer server still ack it but do not require it anymore.
			// Therefore there is no use in seding it.
			// We still have to increase the packet counter as if we had sent
			//  it because the packed-ids the server expects are fixed.
			IncPacketCounter(PacketType.Command);
			// Send the actual new init packet.
			AddOutgoingPacket(ts3Crypt.ProcessInit1<TIn>(null).Value, PacketType.Init1);
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
			var resendThread = new Thread(ResendLoop) { Name = "PacketHandler" };
			resendThreadId = resendThread.ManagedThreadId;

			lock (sendLoopLock)
			{
				ClientId = 0;
				ExitReason = null;
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

				udpClient?.Dispose();
				try
				{
					if (connect)
					{
						remoteAddress = address;
						udpClient = new UdpClient(address.AddressFamily);
						udpClient.Connect(address);
					}
					else
					{
						remoteAddress = null;
						udpClient = new UdpClient(address);
					}
				}
				catch (SocketException ex) { throw new Ts3Exception("Could not connect", ex); }
			}

			try
			{
				resendThread.Start();
			}
			catch (SystemException ex) { throw new Ts3Exception("Error initializing internal stuctures", ex); }
		}

		public void Stop(Reason closeReason = Reason.LeftServer)
		{
			resendThreadId = -1;
			lock (sendLoopLock)
			{
				udpClient?.Dispose();
				if (!ExitReason.HasValue)
					ExitReason = closeReason;
				sendLoopPulse.Set();
			}
		}

		public E<string> AddOutgoingPacket(ReadOnlySpan<byte> packet, PacketType packetType, PacketFlags addFlags = PacketFlags.None)
		{
			lock (sendLoopLock)
			{
				if (Closed)
					return "Connection closed";

				if (NeedsSplitting(packet.Length) && packetType != PacketType.VoiceWhisper)
				{
					// VoiceWhisper packets are for some reason excluded
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
				meta.ClientId = ClientId;
				packet.HeaderExt = (TOut)(object)meta;
			}
			packet.PacketFlags |= flags;

			switch (packet.PacketType)
			{
			case PacketType.Voice:
			case PacketType.VoiceWhisper:
				packet.PacketFlags |= PacketFlags.Unencrypted;
				BinaryPrimitives.WriteUInt16BigEndian(packet.Data, packet.PacketId);
				LoggerRawVoice.Trace("[O] {0}", packet);
				break;

			case PacketType.Command:
			case PacketType.CommandLow:
				packet.PacketFlags |= PacketFlags.Newprotocol;
				var resendPacket = new ResendPacket<TOut>(packet);
				packetAckManager.Add(packet.PacketId, resendPacket);
				LoggerRaw.Debug("[O] {0}", packet);
				break;

			case PacketType.Ping:
				lastSentPingId = packet.PacketId;
				packet.PacketFlags |= PacketFlags.Unencrypted;
				LoggerRaw.Trace("[O] Ping {0}", packet.PacketId);
				break;

			case PacketType.Pong:
				packet.PacketFlags |= PacketFlags.Unencrypted;
				LoggerRaw.Trace("[O] Pong {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
				break;

			case PacketType.Ack:
				LoggerRaw.Debug("[O] Acking Ack: {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
				break;

			case PacketType.AckLow:
				packet.PacketFlags |= PacketFlags.Unencrypted;
				LoggerRaw.Debug("[O] Acking AckLow: {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
				break;

			case PacketType.Init1:
				packet.PacketFlags |= PacketFlags.Unencrypted;
				initPacketCheck = new ResendPacket<TOut>(packet);
				LoggerRaw.Debug("[O] InitID: {0}", packet.Data[4]);
				LoggerRaw.Trace("[O] {0}", packet);
				break;

			default: throw Util.UnhandledDefault(packet.PacketType);
			}

			ts3Crypt.Encrypt(ref packet);

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

		public void FetchPackets()
		{
			while (true)
			{
				if (Closed)
					return;

				var dummy = new IPEndPoint(IPAddress.Any, 0);
				byte[] buffer;

				try { buffer = udpClient.Receive(ref dummy); }
				catch (IOException) { return; }
				catch (SocketException) { return; }
				catch (ObjectDisposedException) { return; }

				if (dummy.Address.Equals(remoteAddress.Address) && dummy.Port != remoteAddress.Port)
					continue;

				var optpacket = Packet<TIn>.FromRaw(buffer);
				// Invalid packet, ignore
				if (optpacket is null)
				{
					LoggerRaw.Warn("Dropping invalid packet: {0}", DebugUtil.DebugToHex(buffer));
					continue;
				}
				var packet = optpacket.Value;

				// DebubToHex is costly and allocates, precheck before logging
				if (LoggerRaw.IsTraceEnabled)
					LoggerRaw.Trace("[I] Raw {0}", DebugUtil.DebugToHex(packet.Raw));

				FindIncommingGenerationId(ref packet);
				if (!ts3Crypt.Decrypt(ref packet))
				{
					LoggerRaw.Warn("Dropping not decryptable packet: {0}", DebugUtil.DebugToHex(packet.Raw));
					continue;
				}

				lastMessageTimer.Restart();
				NetworkStats.LogInPacket(ref packet);

				bool passPacketToEvent = true;
				switch (packet.PacketType)
				{
				case PacketType.Voice:
					LoggerRawVoice.Trace("[I] {0}", packet);
					passPacketToEvent = ReceiveVoice(ref packet, receiveWindowVoice);
					break;
				case PacketType.VoiceWhisper:
					LoggerRawVoice.Trace("[I] {0}", packet);
					passPacketToEvent = ReceiveVoice(ref packet, receiveWindowVoiceWhisper);
					break;
				case PacketType.Command:
					LoggerRaw.Debug("[I] {0}", packet);
					passPacketToEvent = ReceiveCommand(ref packet, receiveQueueCommand, PacketType.Ack);
					break;
				case PacketType.CommandLow:
					LoggerRaw.Debug("[I] {0}", packet);
					passPacketToEvent = ReceiveCommand(ref packet, receiveQueueCommandLow, PacketType.AckLow);
					break;
				case PacketType.Ping:
					LoggerRaw.Trace("[I] Ping {0}", packet.PacketId);
					ReceivePing(ref packet);
					break;
				case PacketType.Pong:
					LoggerRaw.Trace("[I] Pong {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
					passPacketToEvent = ReceivePong(ref packet);
					break;
				case PacketType.Ack:
					LoggerRaw.Debug("[I] Acking: {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
					passPacketToEvent = ReceiveAck(ref packet);
					break;
				case PacketType.AckLow: break;
				case PacketType.Init1:
					if (!LoggerRaw.IsTraceEnabled) LoggerRaw.Debug("[I] InitID: {0}", packet.Data[0]);
					if (!LoggerRaw.IsDebugEnabled) LoggerRaw.Trace("[I] {0}", packet);
					passPacketToEvent = ReceiveInitAck(ref packet);
					break;
				default: throw Util.UnhandledDefault(packet.PacketType);
				}

				if (passPacketToEvent)
					PacketEvent?.Invoke(ref packet);
			}
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
			while (TryFetchPacket(packetQueue, out packet))
				PacketEvent?.Invoke(ref packet);

			return false;
		}

		private static bool TryFetchPacket(RingQueue<Packet<TIn>> packetQueue, out Packet<TIn> packet)
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
					LoggerRaw.Warn(ex, "Got invalid compressed data.");
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
					UpdateRto(Util.Now - ackPacket.LastSendTime);
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
				var forwardData = ts3Crypt.ProcessInit1<TIn>(packet.Data);
				if (!forwardData.Ok)
				{
					LoggerRaw.Debug("Error init: {0}", forwardData.Error);
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
			currentRto = smoothedRtt + Util.Max(ClockResolution, TimeSpan.FromTicks(4 * smoothedRttVar.Ticks));
			LoggerRtt.Debug("RTT SRTT:{0} RTTVAR:{1} RTO:{2}", smoothedRtt, smoothedRttVar, currentRto);
		}

		/// <summary>
		/// ResendLoop will regularly check if a packet has be acknowleged and trys to send it again
		/// if the timeout for a packet ran out.
		/// </summary>
		private void ResendLoop()
		{
			var pingCheck = Util.Now;

			while (Thread.CurrentThread.ManagedThreadId == resendThreadId)
			{
				var now = Util.Now;
				lock (sendLoopLock)
				{
					if (Closed)
						break;

					if ((packetAckManager.Count > 0 && ResendPackets(packetAckManager.Values))
						|| (initPacketCheck != null && ResendPacket(initPacketCheck)))
					{
						Stop(Reason.Timeout);
						return;
					}
				}

				var nextTest = now - pingCheck - PingInterval;
				// we need to check if CryptoInitComplete because while false packet ids won't be incremented
				if (nextTest > TimeSpan.Zero && ts3Crypt.CryptoInitComplete)
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
					LoggerTimeout.Debug("TIMEOUT: Got no ping packet response for {0}", elapsed);
					Stop(Reason.Timeout);
					return;
				}

				sendLoopPulse.WaitOne(ClockResolution);
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
			var now = Util.Now;
			// Check if the packet timed out completely
			if (packet.FirstSendTime < now - PacketTimeout)
			{
				LoggerTimeout.Debug("TIMEOUT: {0}", packet);
				return true;
			}

			// Check if we should retransmit a packet because it probably got lost
			if (packet.LastSendTime < now - currentRto)
			{
				LoggerTimeout.Debug("RESEND: {0}", packet);
				currentRto = currentRto + currentRto;
				if (currentRto > MaxRetryInterval)
					currentRto = MaxRetryInterval;
				packet.LastSendTime = Util.Now;
				SendRaw(ref packet.Packet);
			}

			return false;
		}

		private E<string> SendRaw(ref Packet<TOut> packet)
		{
			NetworkStats.LogOutPacket(ref packet);

			// DebubToHex is costly and allocates, precheck before logging
			if (LoggerRaw.IsTraceEnabled)
				LoggerRaw.Trace("[O] Raw: {0}", DebugUtil.DebugToHex(packet.Raw));

			try
			{
				udpClient.Send(packet.Raw, packet.Raw.Length); // , remoteAddress // TODO
				return R.Ok;
			}
			catch (SocketException ex)
			{
				LoggerRaw.Warn(ex, "Failed to deliver packet (Err:{0})", ex.SocketErrorCode);
				return "Socket send error";
			}
		}
	}

	internal class PacketHandler
	{
		protected static readonly Logger LoggerRtt = LogManager.GetLogger("TS3Client.PacketHandler.Rtt");
		protected static readonly Logger LoggerRaw = LogManager.GetLogger("TS3Client.PacketHandler.Raw");
		protected static readonly Logger LoggerRawVoice = LogManager.GetLogger("TS3Client.PacketHandler.Raw.Voice");
		protected static readonly Logger LoggerTimeout = LogManager.GetLogger("TS3Client.PacketHandler.Timeout");

		/// <summary>Elapsed time since first send timestamp until the connection is considered lost.</summary>
		protected static readonly TimeSpan PacketTimeout = TimeSpan.FromSeconds(20);
		/// <summary>Smoothing factor for the SmoothedRtt.</summary>
		protected const float AlphaSmooth = 0.125f;
		/// <summary>Smoothing factor for the SmoothedRttDev.</summary>
		protected const float BetaSmooth = 0.25f;
		/// <summary>The maximum wait time to retransmit a packet.</summary>
		protected static readonly TimeSpan MaxRetryInterval = TimeSpan.FromMilliseconds(1000);
		/// <summary>The timeout check loop interval.</summary>
		protected static readonly TimeSpan ClockResolution = TimeSpan.FromMilliseconds(100);
		protected static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(1);

		protected PacketHandler() { }
	}

	internal delegate void PacketEvent<TDir>(ref Packet<TDir> packet);
}
