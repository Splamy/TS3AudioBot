// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3Client.Full
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;

	public sealed class PacketHandler
	{
		/// <summary>Greatest allowed packet size, including the complete header.</summary>
		private const int MaxPacketSize = 500;
		private const int HeaderSize = 13;
		private const int MaxDecompressedSize = 40000;

		private const int PacketBufferSize = 50;
		private const int RetryTimeout = 5;
		private static readonly TimeSpan[] PacketTimeouts = new[]
		{
			TimeSpan.FromMilliseconds(200),
			TimeSpan.FromMilliseconds(200),
			TimeSpan.FromMilliseconds(500),
			TimeSpan.FromMilliseconds(1000),
		};
		private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan MaxLastPingDistance = TimeSpan.FromSeconds(3);

		private readonly ushort[] packetCounter;
		private readonly uint[] generationCounter;
		private readonly Dictionary<ushort, OutgoingPacket> packetAckManager;
		private readonly Dictionary<ushort, OutgoingPacket> packetPingManager;
		private readonly RingQueue<IncomingPacket> receiveQueue;
		private readonly RingQueue<IncomingPacket> receiveQueueLow;
		private readonly object sendLoopLock = new object();
		private readonly AutoResetEvent sendLoopPulse = new AutoResetEvent(false);
		private readonly Ts3Crypt ts3Crypt;
		private UdpClient udpClient;
		private Thread resendThread;
		private int resendThreadId;

		public NetworkStats NetworkStats { get; }

		public ushort ClientId { get; set; }
		private IPEndPoint remoteAddress;
		public MoveReason? ExitReason { get; set; }
		private bool Closed => ExitReason != null;

		public PacketHandler(Ts3Crypt ts3Crypt)
		{
			packetAckManager = new Dictionary<ushort, OutgoingPacket>();
			packetPingManager = new Dictionary<ushort, OutgoingPacket>();
			receiveQueue = new RingQueue<IncomingPacket>(PacketBufferSize, ushort.MaxValue + 1);
			receiveQueueLow = new RingQueue<IncomingPacket>(PacketBufferSize, ushort.MaxValue + 1);
			NetworkStats = new NetworkStats();

			packetCounter = new ushort[9];
			generationCounter = new uint[9];
			this.ts3Crypt = ts3Crypt;
			resendThreadId = -1;
		}

		public void Connect(string host, ushort port)
		{
			resendThread = new Thread(ResendLoop) { Name = "PacketHandler" };
			resendThreadId = resendThread.ManagedThreadId;

			lock (sendLoopLock)
			{
				ClientId = 0;
				ExitReason = null;

				ConnectUdpClient(host, port);

				packetAckManager.Clear();
				packetPingManager.Clear();
				receiveQueue.Clear();
				receiveQueueLow.Clear();
				Array.Clear(packetCounter, 0, packetCounter.Length);
				Array.Clear(generationCounter, 0, generationCounter.Length);
			}

			resendThread.Start();

			AddOutgoingPacket(ts3Crypt.ProcessInit1(null), PacketType.Init1);
		}

		private void ConnectUdpClient(string host, ushort port)
		{
			((IDisposable)udpClient)?.Dispose();

			try
			{
				IPAddress ipAddr;
				if (!IPAddress.TryParse(host, out ipAddr))
				{
					var hostEntry = Dns.GetHostEntry(host);
					ipAddr = hostEntry.AddressList.FirstOrDefault();
					if (ipAddr == null) throw new Ts3Exception("Could not resove DNS.");
				}

				remoteAddress = new IPEndPoint(ipAddr, port);

				udpClient = new UdpClient();
				udpClient.Connect(remoteAddress);
			}
			catch (SocketException ex) { throw new Ts3Exception("Could not connect", ex); }
		}

		public void Stop(MoveReason closeReason = MoveReason.LeftServer)
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

		public void AddOutgoingPacket(byte[] packet, PacketType packetType)
		{
			lock (sendLoopLock)
			{
				if (Closed)
					return;

				var addFlags = PacketFlags.None;
				if (NeedsSplitting(packet.Length))
				{
					if (packetType == PacketType.Voice || packetType == PacketType.VoiceWhisper)
						return; // Exception maybe ??? This happens when a voice packet is bigger then the allowed size

					packet = QuickLZ.Compress(packet, 1);
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
		}

		private void AddOutgoingPacket(OutgoingPacket packet, PacketFlags flags = PacketFlags.None)
		{
			lock (sendLoopLock)
			{
				if (packet.PacketType == PacketType.Init1)
				{
					packet.PacketFlags |= flags | PacketFlags.Unencrypted;
					packet.PacketId = 101;
					packet.ClientId = 0;
				}
				else
				{
					if (packet.PacketType == PacketType.Ping
						|| packet.PacketType == PacketType.Pong
						|| packet.PacketType == PacketType.Voice
						|| packet.PacketType == PacketType.VoiceWhisper)
						packet.PacketFlags |= flags | PacketFlags.Unencrypted;
					else if (packet.PacketType == PacketType.Ack)
						packet.PacketFlags |= flags;
					else
						packet.PacketFlags |= flags | PacketFlags.Newprotocol;
					var ids = GetPacketCounter(packet.PacketType);
					packet.PacketId = ids.Item1;
					packet.GenerationId = ids.Item2;
					if (packet.PacketType == PacketType.Voice || packet.PacketType == PacketType.VoiceWhisper)
						NetUtil.H2N(packet.PacketId, packet.Data, 0);
					if (ts3Crypt.CryptoInitComplete)
						IncPacketCounter(packet.PacketType);
					packet.ClientId = ClientId;
				}

				ts3Crypt.Encrypt(packet);

				if (packet.PacketType == PacketType.Command
					|| packet.PacketType == PacketType.CommandLow
					|| packet.PacketType == PacketType.Init1)
					packetAckManager.Add(packet.PacketId, packet);
				else if (packet.PacketType == PacketType.Ping)
					packetPingManager.Add(packet.PacketId, packet);

				SendRaw(packet);
			}
		}

		private Tuple<ushort, uint> GetPacketCounter(PacketType packetType)
			=> new Tuple<ushort, uint>(packetCounter[(int)packetType], generationCounter[(int)packetType]);
		private void IncPacketCounter(PacketType packetType)
		{
			packetCounter[(int)packetType]++;
			if (packetCounter[(int)packetType] == 0)
				generationCounter[(int)packetType]++;
		}

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
				if (Closed)
					return null;

				IncomingPacket packet = null;
				if (TryFetchPacket(receiveQueue, out packet))
					return packet;
				if (TryFetchPacket(receiveQueueLow, out packet))
					return packet;

				var dummy = new IPEndPoint(IPAddress.Any, 0);
				byte[] buffer;

				try { buffer = udpClient.Receive(ref dummy); }
				catch (IOException) { return null; }
				catch (SocketException) { return null; }
				if (dummy.Address.Equals(remoteAddress.Address) && dummy.Port != remoteAddress.Port)
					continue;

				packet = Ts3Crypt.GetIncommingPacket(buffer);
				if (IsCommandPacketSet(packet))
					continue;
				
				if (!ts3Crypt.Decrypt(packet))
					continue;

				NetworkStats.LogInPacket(packet);

				switch (packet.PacketType)
				{
				case PacketType.Voice: break;
				case PacketType.VoiceWhisper: break;
				case PacketType.Command: packet = ReceiveCommand(packet); break;
				case PacketType.CommandLow: packet = ReceiveCommand(packet); break;
				case PacketType.Ping: ReceivePing(packet); break;
				case PacketType.Pong: ReceivePong(packet); break;
				case PacketType.Ack: packet = ReceiveAck(packet); break;
				case PacketType.AckLow: break;
				case PacketType.Init1: ReceiveInitAck(); break;
				default:
					throw new ArgumentOutOfRangeException();
				}

				if (packet != null)
					return packet;
			}
		}

		#region Packet checking
		// These methods are for low level packet processing which the
		// rather high level TS3FullClient should not worry about.

		private bool IsCommandPacketSet(IncomingPacket packet)
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
			{
				return false;
			}

			packet.GenerationId = packetQueue.GetGeneration(packet.PacketId);
			return packetQueue.IsSet(packet.PacketId);
		}

		private IncomingPacket ReceiveCommand(IncomingPacket packet)
		{
			RingQueue<IncomingPacket> packetQueue;
			if (packet.PacketType == PacketType.Command)
				packetQueue = receiveQueue;
			else if (packet.PacketType == PacketType.CommandLow)
				packetQueue = receiveQueueLow;
			else
				throw new InvalidOperationException("The packet is not a command");

			packetQueue.Set(packet.PacketId, packet);

			IncomingPacket retPacket;
			return TryFetchPacket(packetQueue, out retPacket) ? retPacket : null;
		}

		private static bool TryFetchPacket(RingQueue<IncomingPacket> packetQueue, out IncomingPacket packet)
		{
			if (packetQueue.Count <= 0) { packet = null; return false; }

			int take = 0;
			int takeLen = 0;
			bool hasStart = false;
			bool hasEnd = false;
			for (int i = 0; i < packetQueue.Count; i++)
			{
				IncomingPacket peekPacket;
				if (packetQueue.TryPeekStart(i, out peekPacket))
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
				{
					break;
				}
			}

			if (!hasStart || !hasEnd) { packet = null; return false; }

			// GET
			if (!packetQueue.TryDequeue(out packet))
				throw new InvalidOperationException("Packet in queue got missing (?)");

			if (take > 1) // MERGE
			{
				var preFinalArray = new byte[takeLen];

				// for loop at 0th element
				int curCopyPos = packet.Size;
				Array.Copy(packet.Data, 0, preFinalArray, 0, packet.Size);

				for (int i = 1; i < take; i++)
				{
					IncomingPacket nextPacket = null;
					if (!packetQueue.TryDequeue(out nextPacket))
						throw new InvalidOperationException("Packet in queue got missing (?)");

					Array.Copy(nextPacket.Data, 0, preFinalArray, curCopyPos, nextPacket.Size);
					curCopyPos += nextPacket.Size;
				}
				packet.Data = preFinalArray;
			}

			// DECOMPRESS
			if (packet.CompressedFlag)
			{
				if (QuickLZ.SizeDecompressed(packet.Data) > MaxDecompressedSize)
					throw new InvalidOperationException("Compressed packet is too large");
				packet.Data = QuickLZ.Decompress(packet.Data);
			}
			return true;
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

		private IncomingPacket ReceiveAck(IncomingPacket packet)
		{
			if (packet.Data.Length < 2)
				return null;
			ushort packetId = NetUtil.N2Hushort(packet.Data, 0);

			lock (sendLoopLock)
				packetAckManager.Remove(packetId);
			return packet;
		}

		private void SendPing()
		{
			AddOutgoingPacket(new byte[0], PacketType.Ping);
		}

		private void ReceivePing(IncomingPacket packet)
		{
			byte[] pongData = new byte[2];
			NetUtil.H2N(packet.PacketId, pongData, 0);
			AddOutgoingPacket(pongData, PacketType.Pong);
		}

		private void ReceivePong(IncomingPacket packet)
		{
			ushort answerId = NetUtil.N2Hushort(packet.Data, 0);
			OutgoingPacket sendPing;
			lock (sendLoopLock)
			{
				if (!packetPingManager.TryGetValue(answerId, out sendPing))
					return;
				packetPingManager.Remove(answerId);
			}
			NetworkStats.AddPing(Util.Now - sendPing.LastSendTime);
		}

		public void ReceiveInitAck()
		{
			// this method is a bit hacky since it removes ALL Init1 packets
			// from the sendQueue instead of the one with the preceding
			// init step id (see Ts3Crypt.ProcessInit1).
			// But usually this should be no problem since the init order is linear
			lock (sendLoopLock)
			{
				var remPacket = packetAckManager.Values.Where(x => x.PacketType == PacketType.Init1).ToArray();
				foreach (var packet in remPacket)
					packetAckManager.Remove(packet.PacketId);
			}
		}

		#endregion

		private static TimeSpan GetTimeout(int step) => PacketTimeouts[Math.Min(PacketTimeouts.Length - 1, step)];

		/// <summary>
		/// ResendLoop will regularly check if a packet has be acknowleged and trys to send it again
		/// if the timeout for a packet ran out.
		/// </summary>
		private void ResendLoop()
		{
			DateTime pingCheck = Util.Now;

			while (Thread.CurrentThread.ManagedThreadId == resendThreadId)
			{
				TimeSpan sleepSpan = GetTimeout(PacketTimeouts.Length);

				lock (sendLoopLock)
				{
					if (Closed)
						break;

					if ((packetAckManager.Any() && ResendPackages(packetAckManager.Values, ref sleepSpan))
						|| (packetPingManager.Any() && ResendPackages(packetPingManager.Values, ref sleepSpan)))
					{
						Stop(MoveReason.Timeout);
						return;
					}
				}

				var now = Util.Now;
				var nextTest = pingCheck - now + PingInterval;
				if (nextTest < TimeSpan.Zero)
				{
					if (nextTest < -MaxLastPingDistance)
						pingCheck = now;
					else
						pingCheck += PingInterval;
					SendPing();
				}
				sleepSpan = Util.Min(sleepSpan, nextTest);

				if (sleepSpan > TimeSpan.Zero)
					sendLoopPulse.WaitOne(sleepSpan);
			}
		}

		private bool ResendPackages(IEnumerable<OutgoingPacket> packetList, ref TimeSpan sleepSpan)
		{
			TimeSpan maxTimeout = GetTimeout(PacketTimeouts.Length);
			foreach (var outgoingPacket in packetList)
			{
				var nextTest = (outgoingPacket.LastSendTime - Util.Now) + GetTimeout(outgoingPacket.ResendCount);
				if (nextTest < TimeSpan.Zero)
				{
					if (++outgoingPacket.ResendCount > RetryTimeout)
					{
#if DEBUG
						Console.WriteLine("TIMEOUT: " + DebugUtil.DebugToHex(outgoingPacket.Raw));
#endif
						return true;
					}
					SendRaw(outgoingPacket);
				}
				else
					sleepSpan = Util.Min(sleepSpan, nextTest);
			}
			return false;
		}

		private void SendRaw(OutgoingPacket packet)
		{
			packet.LastSendTime = Util.Now;
			NetworkStats.LogOutPacket(packet);
			udpClient.Send(packet.Raw, packet.Raw.Length);
		}
	}
}
