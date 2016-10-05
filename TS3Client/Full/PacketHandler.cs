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
			Reset();
			// TODO: check run! i think we need to recreate the thread....
			if (!resendThread.IsAlive)
				resendThread.Start();
		}

		public void AddOutgoingPacket(byte[] packet, PacketType packetType)
		{
			var addFlags = PacketFlags.None;
			if (NeedsSplitting(packet.Length))
			{
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
				packet.PacketFlags |= flags | PacketFlags.Newprotocol;
				packet.PacketId = GetPacketCounter(packet.PacketType);
				IncPacketCounter(packet.PacketType);
				packet.ClientId = ClientId;
			}

			if (packet.PacketType == PacketType.Command)
				lock (sendLoopMonitor)
					sendQueue.AddLast(packet);
			if (!ts3Crypt.Encrypt(packet))
				throw new Exception(); // TODO

			SendRaw(packet);
		}

		private ushort GetPacketCounter(PacketType packetType) => packetCounter[(int)packetType];
		private void IncPacketCounter(PacketType packetType) => packetCounter[(int)packetType]++;

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

				switch (packet.PacketType)
				{
					case PacketType.Readable: break;
					case PacketType.Voice: break;
					case PacketType.Command: ReceiveCommand(packet); break; // TODO MERGE LOGIC !
					case PacketType.CommandLow: break;
					case PacketType.Ping: break;
					case PacketType.Pong: break;
					case PacketType.Ack: ReceiveAck(packet); break;
					case PacketType.Type7Closeconnection: break;
					case PacketType.Init1: break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				return packet;
			}
		}

		#region Packet checking
		// These methods are for low level packet processing which the
		// rather high level TS3FullClient should not worry about.

		private void ReceiveCommand(IncomingPacket packet)
		{
			if (receiveQueue.IsSet(packet.PacketId))
			{
				receiveQueue.Set(packet, packet.PacketId);
			}
			SendAck(packet.PacketId);
		}

		private void SendAck(ushort ackId)
		{
			byte[] ackData = new byte[2];
			NetUtil.H2N(ackId, ackData, 0);
			AddOutgoingPacket(ackData, PacketType.Ack);
		}

		private void ReceiveAck(IncomingPacket packet)
		{
			if (packet.Data.Length < 2)
				return;
			ushort packetId = NetUtil.N2Hushort(packet.Data, 0);

			lock (sendLoopMonitor)
				for (var node = sendQueue.First; node != null; node = node.Next)
					if (node.Value.PacketId == packetId)
						sendQueue.Remove(node);
		}

		#endregion

		/// <summary>
		/// ResendLoop will regularly check if a packet has be acknowleged and trys to send it again
		/// if the timeout for a packet ran out.
		/// </summary>
		private void ResendLoop()
		{
			TimeSpan sleepSpan = PacketTimeout;

			while (true)
			{
				lock (sendLoopMonitor)
				{
					if (!sendQueue.Any())
						Monitor.Wait(sendLoopMonitor, sleepSpan);

					if (!sendQueue.Any())
						continue;

					DateTime nowTimeout = DateTime.UtcNow - PacketTimeout;

					foreach (var outgoingPacket in sendQueue)
					{
						var nextTest = nowTimeout - outgoingPacket.LastSendTime;
						if (nextTest < TimeSpan.Zero)
							SendRaw(outgoingPacket);
						else if (nextTest < sleepSpan)
							sleepSpan = nextTest;
					}
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
