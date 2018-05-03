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
		private const int MaxPacketSize = 500;
		private const int HeaderSize = 13;
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
		private ushort lastSentPingId;
		private ushort lastReceivedPingId;

		private readonly ushort[] packetCounter;
		private readonly uint[] generationCounter;
		private ResendPacket<TOut> initPacketCheck;
		private readonly Dictionary<ushort, ResendPacket<TOut>> packetAckManager;
		private readonly RingQueue<Packet<TIn>> receiveQueue;
		private readonly RingQueue<Packet<TIn>> receiveQueueLow;
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
			receiveQueue = new RingQueue<Packet<TIn>>(ReceivePacketWindowSize, ushort.MaxValue + 1);
			receiveQueueLow = new RingQueue<Packet<TIn>>(ReceivePacketWindowSize, ushort.MaxValue + 1);
			NetworkStats = new NetworkStats();

			packetCounter = new ushort[9];
			generationCounter = new uint[9];
			this.ts3Crypt = ts3Crypt;
			resendThreadId = -1;
		}

		public void Connect(IPEndPoint address)
		{
			Initialize(address, true);
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

				initPacketCheck = null;
				packetAckManager.Clear();
				receiveQueue.Clear();
				receiveQueueLow.Clear();
				Array.Clear(packetCounter, 0, packetCounter.Length);
				Array.Clear(generationCounter, 0, generationCounter.Length);
				NetworkStats.Reset();

				((IDisposable)udpClient)?.Dispose();
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

			resendThread.Start();
		}

		public void Stop(Reason closeReason = Reason.LeftServer)
		{
			resendThreadId = -1;
			lock (sendLoopLock)
			{
				((IDisposable)udpClient)?.Dispose();
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
						return "Voice packet too big"; // Exception maybe ??? This happens when a voice packet is bigger than the allowed size

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
				SendOutgoingData(packet, packetType, addFlags);
				return R.Ok;
			}
		}

		private E<string> AddOutgoingSplitData(ReadOnlySpan<byte> rawData, PacketType packetType, PacketFlags addFlags = PacketFlags.None)
		{
			int pos = 0;
			bool first = true;
			bool last;

			// TODO check if "packBuffer.FreeSlots >= packetSplit.Count"

			const int maxContent = MaxPacketSize - HeaderSize;
			do
			{
				int blockSize = Math.Min(maxContent, rawData.Length - pos);
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

				SendOutgoingData(rawData.Slice(pos, blockSize), packetType, flags);
				pos += blockSize;
			} while (!last);

			return R.Ok;
		}

		// is always locked on 'sendLoopLock' from a higher call
		private E<string> SendOutgoingData(ReadOnlySpan<byte> data, PacketType packetType, PacketFlags flags = PacketFlags.None)
		{
			var ids = GetPacketCounter(packetType);
			if (ts3Crypt.CryptoInitComplete)
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
			case PacketType.AckLow:
				LoggerRaw.Debug("[O] Acking {1}: {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data), packet.PacketType);
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

		public void IncPacketCounter(PacketType packetType)
		{
			unchecked { packetCounter[(int)packetType]++; }
			if (packetCounter[(int)packetType] == 0)
				generationCounter[(int)packetType]++;
		}

		public void CryptoInitDone()
		{
			if (!ts3Crypt.CryptoInitComplete)
				throw new InvalidOperationException($"{nameof(CryptoInitDone)} was called although it isn't initialized");
			IncPacketCounter(PacketType.Command);
		}

		private static bool NeedsSplitting(int dataSize) => dataSize + HeaderSize > MaxPacketSize;

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
				if (optpacket == null)
				{
					LoggerRaw.Debug("Dropping invalid packet: {0}", DebugUtil.DebugToHex(buffer));
					continue;
				}
				var packet = optpacket.Value;

				GenerateGenerationId(ref packet);
				if (!ts3Crypt.Decrypt(ref packet))
					continue;

				NetworkStats.LogInPacket(ref packet);

				bool passPacketToEvent = true;
				switch (packet.PacketType)
				{
				case PacketType.Voice:
				case PacketType.VoiceWhisper:
					LoggerRawVoice.Trace("[I] {0}", packet);
					break;
				case PacketType.Command:
					LoggerRaw.Debug("[I] {0}", packet);
					passPacketToEvent = ReceiveCommand(ref packet, receiveQueue, PacketType.Ack);
					break;
				case PacketType.CommandLow:
					LoggerRaw.Debug("[I] {0}", packet);
					passPacketToEvent = ReceiveCommand(ref packet, receiveQueueLow, PacketType.AckLow);
					break;
				case PacketType.Ping:
					LoggerRaw.Trace("[I] Ping {0}", packet.PacketId);
					ReceivePing(ref packet);
					break;
				case PacketType.Pong:
					LoggerRaw.Trace("[I] Pong {0}", BinaryPrimitives.ReadUInt16BigEndian(packet.Data));
					ReceivePong(ref packet);
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

		private void GenerateGenerationId(ref Packet<TIn> packet)
		{
			// TODO rework this for all packet types
			RingQueue<Packet<TIn>> packetQueue;
			switch (packet.PacketType)
			{
			case PacketType.Command: packetQueue = receiveQueue; break;
			case PacketType.CommandLow: packetQueue = receiveQueueLow; break;
			default: return;
			}

			packet.GenerationId = packetQueue.GetGeneration(packet.PacketId);
		}

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
				catch (Exception)
				{
					Debug.WriteLine("Got invalid compressed data.");
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
			if (packet.Data.Length < 2)
				return false;
			ushort packetId = BinaryPrimitives.ReadUInt16BigEndian(packet.Data);

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

		private void ReceivePong(ref Packet<TIn> packet)
		{
			ushort answerId = BinaryPrimitives.ReadUInt16BigEndian(packet.Data);

			if (lastSentPingId == answerId)
			{
				var rtt = pingTimer.Elapsed;
				UpdateRto(rtt);
				NetworkStats.AddPing(rtt);
			}
		}

		public void ReceivedFinalInitAck()
		{
			initPacketCheck = null;
		}

		private bool ReceiveInitAck(ref Packet<TIn> packet)
		{
			lock (sendLoopLock)
			{
				if (initPacketCheck == null)
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

					if ((packetAckManager.Count > 0 && ResendPackets(packetAckManager.Values)) ||
						(initPacketCheck != null && ResendPacket(initPacketCheck)))
					{
						Stop(Reason.Timeout);
						return;
					}
				}

				var nextTest = pingCheck - now + PingInterval;
				// we need to check if CryptoInitComplete because while false packet ids won't be incremented
				if (nextTest < TimeSpan.Zero && ts3Crypt.CryptoInitComplete)
				{
					pingCheck += PingInterval;
					SendPing();
				}
				// TODO implement ping-timeout here
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
			LoggerRaw.Trace("[O] Raw: {0}", DebugUtil.DebugToHex(packet.Raw));
			try
			{
				udpClient.Send(packet.Raw, packet.Raw.Length); // , remoteAddress // TODO
				return R.Ok;
			}
			catch (SocketException ex)
			{
				LoggerRaw.Warn(ex, "Failes to deliver packet (Err:{0})", ex.SocketErrorCode);
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
		protected static readonly TimeSpan PacketTimeout = TimeSpan.FromSeconds(30);
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
