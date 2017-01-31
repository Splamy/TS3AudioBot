namespace TS3Client.Full
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;

	internal class PacketHandler
	{
		/// <summary>Greatest allowed packet size, including the complete heder.</summary>
		private const int MaxPacketSize = 500;
		private const int HeaderSize = 13;

		private const int PacketBufferSize = 50;
		private const int RetryTimeout = 5;
		private static readonly TimeSpan[] PacketTimeouts = new[]
		{
			TimeSpan.FromMilliseconds(200),
			TimeSpan.FromMilliseconds(200),
			TimeSpan.FromMilliseconds(500),
			TimeSpan.FromMilliseconds(1000),
		};

		private readonly ushort[] packetCounter;
		private readonly LinkedList<OutgoingPacket> sendQueue;
		private readonly RingQueue<IncomingPacket> receiveQueue;
		private readonly RingQueue<IncomingPacket> receiveQueueLow;
		private readonly object sendLoopMonitor = new object();
		private readonly Ts3Crypt ts3Crypt;
		private UdpClient udpClient;
		private Thread resendThread;
		private int resendThreadId;


		public ushort ClientId { get; set; }
		public IPEndPoint RemoteAddress { get; set; }
		public MoveReason? ExitReason { get; private set; }

		public PacketHandler(Ts3Crypt ts3Crypt)
		{
			sendQueue = new LinkedList<OutgoingPacket>();
			receiveQueue = new RingQueue<IncomingPacket>(PacketBufferSize);
			receiveQueueLow = new RingQueue<IncomingPacket>(PacketBufferSize);

			packetCounter = new ushort[9];
			this.ts3Crypt = ts3Crypt;
			resendThreadId = -1;
		}

		public void Start(UdpClient udpClient)
		{
			resendThread = new Thread(ResendLoop) { Name = "PacketHandler" };
			resendThreadId = resendThread.ManagedThreadId;

			ClientId = 0;
			lock (sendLoopMonitor)
			{
				ExitReason = null;
				this.udpClient = udpClient;
				sendQueue.Clear();
			}
			receiveQueue.Clear();
			Array.Clear(packetCounter, 0, packetCounter.Length);

			resendThread.Start();
		}

		public void Stop()
		{
			resendThreadId = -1;
			if (Monitor.TryEnter(sendLoopMonitor))
			{
				udpClient?.Close();
				Monitor.Pulse(sendLoopMonitor);
				Monitor.Exit(sendLoopMonitor);
			}
		}

		public void AddOutgoingPacket(byte[] packet, PacketType packetType)
		{
			var addFlags = PacketFlags.None;
			if (NeedsSplitting(packet.Length))
			{
				if (packetType == PacketType.Readable || packetType == PacketType.Voice)
					return; // Exception maybe ??? This happens when a voice packet is bigger then the allowed size

				packet = QuickLZ.compress(packet, 1);
				addFlags |= PacketFlags.Compressed;

				if (NeedsSplitting(packet.Length))
				{
					foreach (var splitPacket in BuildSplitList(packet, packetType))
						AddOutgoingPacket(splitPacket, addFlags);
					return;
				}
			}
			AddOutgoingPacket(new OutgoingPacket(packet, packetType), addFlags);
		}

		private void AddOutgoingPacket(OutgoingPacket packet, PacketFlags flags = PacketFlags.None)
		{
			if (packet.PacketType == PacketType.Init1)
			{
				packet.PacketFlags |= flags | PacketFlags.Unencrypted;
				packet.PacketId = 101;
				packet.ClientId = 0;
			}
			else
			{
				if (packet.PacketType == PacketType.Pong || packet.PacketType == PacketType.Readable || packet.PacketType == PacketType.Voice)
					packet.PacketFlags |= flags | PacketFlags.Unencrypted;
				else if (packet.PacketType == PacketType.Ack)
					packet.PacketFlags |= flags;
				else
					packet.PacketFlags |= flags | PacketFlags.Newprotocol;
				packet.PacketId = GetPacketCounter(packet.PacketType);
				if (packet.PacketType == PacketType.Readable || packet.PacketType == PacketType.Voice)
					NetUtil.H2N(packet.PacketId, packet.Data, 0);
				if (ts3Crypt.CryptoInitComplete)
					IncPacketCounter(packet.PacketType);
				packet.ClientId = ClientId;
			}

			if (!ts3Crypt.Encrypt(packet))
				throw new Ts3Exception("Internal encryption error.");

			if (packet.PacketType == PacketType.Command
				|| packet.PacketType == PacketType.CommandLow
				|| packet.PacketType == PacketType.Init1)
				lock (sendLoopMonitor)
					sendQueue.AddLast(packet);

			SendRaw(packet);
		}

		private ushort GetPacketCounter(PacketType packetType) => packetCounter[(int)packetType];
		private void IncPacketCounter(PacketType packetType) => packetCounter[(int)packetType]++;

		public void CryptoInitDone()
		{
			if (!ts3Crypt.CryptoInitComplete)
				throw new InvalidOperationException($"{nameof(CryptoInitDone)} was called although it isn't initialized");
			IncPacketCounter(PacketType.Command);
		}

		private static IEnumerable<OutgoingPacket> BuildSplitList(byte[] rawData, PacketType packetType)
		{
			int pos = 0;
			bool first = true;
			bool last;

			const int maxContent = MaxPacketSize - HeaderSize;
			do
			{
				int blockSize = Math.Min(maxContent, rawData.Length - pos);
				if (blockSize <= 0) break;

				var tmpBuffer = new byte[blockSize];
				Array.Copy(rawData, pos, tmpBuffer, 0, blockSize);
				var packet = new OutgoingPacket(tmpBuffer, packetType);

				last = pos + blockSize == rawData.Length;
				if (first ^ last)
					packet.FragmentedFlag = true;
				if (first)
					first = false;

				yield return packet;
				pos += blockSize;

			} while (!last);
		}

		private static bool NeedsSplitting(int dataSize) => dataSize + HeaderSize > MaxPacketSize;

		public IncomingPacket FetchPacket()
		{
			while (true)
			{
				if (ExitReason.HasValue)
					return null;
				if ((!udpClient?.Client?.Connected) ?? true)
				{
					Thread.Sleep(1);
					continue;
				}

				var dummy = new IPEndPoint(IPAddress.Any, 0);
				byte[] buffer;
				try { buffer = udpClient.Receive(ref dummy); }
				catch (IOException) { return null; }
				if (dummy.Address.Equals(RemoteAddress.Address) && dummy.Port != RemoteAddress.Port)
					continue;

				var packet = ts3Crypt.Decrypt(buffer);
				if (packet == null)
					continue;

				bool passToReturn = true;
				switch (packet.PacketType)
				{
					case PacketType.Readable: break;
					case PacketType.Voice: break;
					case PacketType.Command: passToReturn = ReceiveCommand(packet); break;
					case PacketType.CommandLow: passToReturn = ReceiveCommand(packet); break;
					case PacketType.Ping: passToReturn = ReceivePing(packet); break;
					case PacketType.Pong: break;
					case PacketType.Ack: passToReturn = ReceiveAck(packet); break;
					case PacketType.AckLow: break;
					case PacketType.Init1: ReceiveInitAck(); break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				if (passToReturn)
					return packet;
			}
		}

		#region Packet checking
		// These methods are for low level packet processing which the
		// rather high level TS3FullClient should not worry about.

#if DEBUG
		Dictionary<ushort, int> multiGetPackCount = new Dictionary<ushort, int>();
#endif

		private bool ReceiveCommand(IncomingPacket packet)
		{
			RingQueue<IncomingPacket> packetQueue;
			if (packet.PacketType == PacketType.Command)
			{
				SendAck(packet.PacketId, PacketType.Ack);
				packetQueue = receiveQueue;
			}
			else if (packet.PacketType == PacketType.CommandLow)
			{
				SendAck(packet.PacketId, PacketType.AckLow);
				packetQueue = receiveQueueLow;
			}
			else
				throw new InvalidOperationException("The packet is not a command");

			if (!packetQueue.IsSet(packet.PacketId))
			{
				packetQueue.Set(packet, packet.PacketId);
				int take = 0;
				int takeLen = 0;
				bool hasStart = false;
				bool hasEnd = false;
				for (int i = 0; i < packetQueue.Count; i++)
				{
					IncomingPacket peekPacket;
					if (packetQueue.TryPeek(packetQueue.StartIndex + i, out peekPacket))
					{
						take++;
						takeLen += peekPacket.Size;
						if (peekPacket.FragmentedFlag)
						{
							if (!hasStart) { hasStart = true; }
							else if (!hasEnd) { hasEnd = true; break; }
						}
						else
						{
							if (!hasStart) { hasStart = true; hasEnd = true; break; }
						}
					}
					else
						break;
				}
				if (hasStart && hasEnd)
				{
					IncomingPacket preFinalPacket = null;
					if (take == 1)
					{
						// GET & (MERGE, skip with only 1)
						if (!packetQueue.TryDequeue(out preFinalPacket))
							throw new InvalidOperationException();
						// DECOMPRESS
						if (preFinalPacket.CompressedFlag)
							packet.Data = QuickLZ.decompress(preFinalPacket.Data);
						return true;
					}
					else // take > 1
					{
						// GET & MERGE
						var preFinalArray = new byte[takeLen];
						int curCopyPos = 0;
						bool firstSet = false;
						bool isCompressed = false;
						for (int i = 0; i < take; i++)
						{
							if (!packetQueue.TryDequeue(out preFinalPacket))
								throw new InvalidOperationException();
							if (!firstSet)
							{
								isCompressed = preFinalPacket.CompressedFlag;
								firstSet = true;
							}
							Array.Copy(preFinalPacket.Data, 0, preFinalArray, curCopyPos, preFinalPacket.Size);
							curCopyPos += preFinalPacket.Size;
						}
						// DECOMPRESS
						if (isCompressed)
							packet.Data = QuickLZ.decompress(preFinalArray);
						else
							packet.Data = preFinalArray;
					}

					return true;
				}
				else
					return false;
			}
#if DEBUG
			else
			{
				if (!multiGetPackCount.ContainsKey(packet.PacketId))
					multiGetPackCount.Add(packet.PacketId, 0);
				var cnt = ++multiGetPackCount[packet.PacketId];
				if (cnt > 3)
				{
					Console.WriteLine("Non-get-able packet id {0} DATA: {1}", packet.PacketId, string.Join(" ", packet.Raw.Select(x => x.ToString("X2"))));
				}
			}
#endif
			return false;
		}

		private void SendAck(ushort ackId, PacketType ackType)
		{
			byte[] ackData = new byte[2];
			NetUtil.H2N(ackId, ackData, 0);
			if (ackType == PacketType.Ack || ackType == PacketType.AckLow)
				AddOutgoingPacket(ackData, ackType);
			else
				throw new InvalidOperationException("Packet type is not an Ack-type");
		}

#if DEBUG
		Dictionary<ushort, int> multiGetAckCount = new Dictionary<ushort, int>();
#endif

		private bool ReceiveAck(IncomingPacket packet)
		{
			if (packet.Data.Length < 2)
				return false;
			ushort packetId = NetUtil.N2Hushort(packet.Data, 0);

#if DEBUG
			lock (sendLoopMonitor)
			{
				bool hasRemoved = false;
				for (var node = sendQueue.First; node != null; node = node.Next)
					if (node.Value.PacketId == packetId)
					{
						sendQueue.Remove(node);
						hasRemoved = true;
					}
				if (!hasRemoved)
				{
					if (!multiGetAckCount.ContainsKey(packetId))
						multiGetAckCount.Add(packetId, 0);
					var cnt = ++multiGetAckCount[packetId];
					if (cnt > 3)
					{
						Console.WriteLine("Non-ack-able packet id {0} DATA: {1}", packetId, string.Join(" ", sendQueue.First(x => x.PacketId == packetId).Raw.Select(x => x.ToString("X2"))));
					}
				}
			}
#else
			lock (sendLoopMonitor)
				for (var node = sendQueue.First; node != null; node = node.Next)
					if (node.Value.PacketId == packetId)
						sendQueue.Remove(node);
#endif
			return true;
		}

		private bool ReceivePing(IncomingPacket packet)
		{
			byte[] pongData = new byte[2];
			NetUtil.H2N(packet.PacketId, pongData, 0);
			AddOutgoingPacket(pongData, PacketType.Pong);
			return true;
		}

		public void ReceiveInitAck()
		{
			// this method is a bit hacky since it removes ALL Init1 packets
			// from the sendQueue instead of the one with the preceding
			// init step id (see Ts3Crypt.ProcessInit1).
			// But usually this should be no problem since the init order is linear
			lock (sendLoopMonitor)
				for (var n = sendQueue.First; n != null; n = n.Next)
				{
					if (n.Value.PacketType == PacketType.Init1)
						sendQueue.Remove(n);
				}
		}

		#endregion

		private TimeSpan GetTimeout(int step) => PacketTimeouts[Math.Min(PacketTimeouts.Length - 1, step)];

		/// <summary>
		/// ResendLoop will regularly check if a packet has be acknowleged and trys to send it again
		/// if the timeout for a packet ran out.
		/// </summary>
		private void ResendLoop()
		{
			while (Thread.CurrentThread.ManagedThreadId == resendThreadId
				&& udpClient?.Client != null)
			{
				TimeSpan sleepSpan = GetTimeout(PacketTimeouts.Length);

				lock (sendLoopMonitor)
				{
					if (!sendQueue.Any())
					{
						Monitor.Wait(sendLoopMonitor, sleepSpan);
						if (!sendQueue.Any())
							continue;
					}

					foreach (var outgoingPacket in sendQueue)
					{
						var nextTest = (outgoingPacket.LastSendTime - DateTime.UtcNow) + GetTimeout(outgoingPacket.ResendCount);
						if (nextTest < TimeSpan.Zero)
						{
							if (++outgoingPacket.ResendCount > RetryTimeout)
							{
								ExitReason = MoveReason.Timeout;
								Stop();
							}
							SendRaw(outgoingPacket);
						}
						else if (nextTest < sleepSpan)
							sleepSpan = nextTest;
					}

					Monitor.Wait(sendLoopMonitor, sleepSpan);
				}
			}
		}

		private void SendRaw(OutgoingPacket packet)
		{
			packet.LastSendTime = DateTime.UtcNow;
			udpClient.Send(packet.Raw, packet.Raw.Length);
		}
	}
}
