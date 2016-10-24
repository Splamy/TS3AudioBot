namespace TS3Client.Full
{
	using System;
	using System.Collections.Generic;
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
		private static readonly TimeSpan PacketTimeout = TimeSpan.FromSeconds(1);

		private readonly ushort[] packetCounter;
		private readonly LinkedList<OutgoingPacket> sendQueue;
		private readonly RingQueue<IncomingPacket> receiveQueue;
		private readonly Thread resendThread;
		private readonly object sendLoopMonitor = new object();
		private readonly TS3Crypt ts3Crypt;
		private readonly UdpClient udpClient;

		public ushort ClientId { get; set; }

		public PacketHandler(TS3Crypt ts3Crypt, UdpClient udpClient)
		{
			sendQueue = new LinkedList<OutgoingPacket>();
			receiveQueue = new RingQueue<IncomingPacket>(PacketBufferSize);
			resendThread = new Thread(ResendLoop);
			packetCounter = new ushort[9];
			this.ts3Crypt = ts3Crypt;
			this.udpClient = udpClient;
		}

		public void Start()
		{
			// TODO: check run! i think we need to recreate the thread....
			if (!resendThread.IsAlive)
				resendThread.Start();
		}

		public void AddOutgoingPacket(byte[] packet, PacketType packetType)
		{
			var addFlags = PacketFlags.None;
			if (NeedsSplitting(packet.Length))
			{
				if (packetType == PacketType.Readable)
					return; // Exception maybe ??? This happens when a voice packet is bigger then the allowed size

				packet = QuickLZ.compress(packet, 3);
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
				if (packet.PacketType == PacketType.Pong || packet.PacketType == PacketType.Readable)
					packet.PacketFlags |= flags | PacketFlags.Unencrypted;
				else if (packet.PacketType == PacketType.Ack)
					packet.PacketFlags |= flags;
				else
					packet.PacketFlags |= flags | PacketFlags.Newprotocol;
				packet.PacketId = GetPacketCounter(packet.PacketType);
				if (packet.PacketType == PacketType.Readable)
					NetUtil.H2N(packet.PacketId, packet.Data, 0);
				if (ts3Crypt.CryptoInitComplete)
					IncPacketCounter(packet.PacketType);
				packet.ClientId = ClientId;
			}

			if (!ts3Crypt.Encrypt(packet))
				throw new Exception(); // TODO

			if (packet.PacketType == PacketType.Command)
				lock (sendLoopMonitor)
					sendQueue.AddLast(packet);

			SendRaw(packet);
		}

		private ushort GetPacketCounter(PacketType packetType) => packetCounter[(int)packetType];
		private void IncPacketCounter(PacketType packetType) => packetCounter[(int)packetType]++;

		public void CryptoInitDone()
		{
			if (!ts3Crypt.CryptoInitComplete)
				throw new Exception("No it's not >:(");
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
				var dummy = new IPEndPoint(IPAddress.Any, 0);
				byte[] buffer = udpClient.Receive(ref dummy);
				if (/*dummy.Address.Equals(remoteIpAddress) &&*/ dummy.Port != 9987) // todo
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
					case PacketType.CommandLow: break;
					case PacketType.Ping: passToReturn = ReceivePing(packet); break;
					case PacketType.Pong: break;
					case PacketType.Ack: passToReturn = ReceiveAck(packet); break;
					case PacketType.Type7Closeconnection: break;
					case PacketType.Init1: break;
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

		private bool ReceiveCommand(IncomingPacket packet)
		{
			SendAck(packet.PacketId);
			if (!receiveQueue.IsSet(packet.PacketId))
			{
				receiveQueue.Set(packet, packet.PacketId);
				int take = 0;
				int takeLen = 0;
				bool hasStart = false;
				bool hasEnd = false;
				for (int i = 0; i < receiveQueue.Count; i++)
				{
					IncomingPacket peekPacket;
					if (receiveQueue.TryPeek(receiveQueue.StartIndex, out peekPacket))
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
						// MERGE (skip with only 1)
						if (!receiveQueue.TryDequeue(out preFinalPacket))
							throw new InvalidOperationException();
						// DECOMPRESS
						if (preFinalPacket.CompressedFlag)
							packet.Data = QuickLZ.decompress(preFinalPacket.Data);
						return true;
					}
					else // take > 1
					{
						// MERGE
						var preFinalArray = new byte[takeLen];
						int curCopyPos = 0;
						bool firstSet = false;
						bool isCompressed = false;
						for (int i = 0; i < take; i++)
						{
							if (!receiveQueue.TryDequeue(out preFinalPacket))
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
			return false;
		}

		private void SendAck(ushort ackId)
		{
			byte[] ackData = new byte[2];
			NetUtil.H2N(ackId, ackData, 0);
			AddOutgoingPacket(ackData, PacketType.Ack);
		}

		private bool ReceiveAck(IncomingPacket packet)
		{
			if (packet.Data.Length < 2)
				return false;
			ushort packetId = NetUtil.N2Hushort(packet.Data, 0);

			lock (sendLoopMonitor)
				for (var node = sendQueue.First; node != null; node = node.Next)
					if (node.Value.PacketId == packetId)
						sendQueue.Remove(node);
			return true;
		}

		private bool ReceivePing(IncomingPacket packet)
		{
			byte[] pongData = new byte[2];
			NetUtil.H2N(packet.PacketId, pongData, 0);
			AddOutgoingPacket(pongData, PacketType.Pong);
			return true;
		}

		#endregion

		/// <summary>
		/// ResendLoop will regularly check if a packet has be acknowleged and trys to send it again
		/// if the timeout for a packet ran out.
		/// </summary>
		private void ResendLoop()
		{
			while (true)
			{
				TimeSpan sleepSpan = PacketTimeout;

				lock (sendLoopMonitor)
				{
					if (!sendQueue.Any())
						Monitor.Wait(sendLoopMonitor, sleepSpan);

					if (!sendQueue.Any())
						continue;

					foreach (var outgoingPacket in sendQueue)
					{
						var nextTest = (outgoingPacket.LastSendTime - DateTime.UtcNow) + PacketTimeout;
						if (nextTest < TimeSpan.Zero)
							SendRaw(outgoingPacket);
						else if (nextTest < sleepSpan)
							sleepSpan = nextTest;
					}

					Thread.Sleep(sleepSpan);
				}
			}
		}

		private void SendRaw(OutgoingPacket packet)
		{
			packet.LastSendTime = DateTime.UtcNow;
			udpClient.Send(packet.Raw, packet.Raw.Length);
		}

		public void Reset()
		{
			ClientId = 0;
			sendQueue.Clear();
			receiveQueue.Clear();
			Array.Clear(packetCounter, 0, packetCounter.Length);
		}
	}
}
