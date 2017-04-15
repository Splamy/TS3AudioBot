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
	using Commands;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	internal class NetworkStats
	{
		private long[] outPackets = new long[3];
		private long[] inPackets = new long[3];
		private long[] outBytes = new long[3];
		private long[] inBytes = new long[3];
		private Queue<PacketData> outBytesTime = new Queue<PacketData>();
		private Queue<PacketData> inBytesTime = new Queue<PacketData>();
		private Queue<TimeSpan> pingTimes = new Queue<TimeSpan>(60);
		private static readonly TimeSpan TimeSecond = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan TimeMinute = TimeSpan.FromMinutes(1);
		private readonly object queueLock = new object();

		public void LogOutPacket(OutgoingPacket packet)
		{
			var kind = TypeToKind(packet.PacketType);
			outPackets[(int)kind]++;
			outBytes[(int)kind] += packet.Raw.Length;
			lock (queueLock)
			{
				DropOver(outBytesTime, TimeMinute);
				outBytesTime.Enqueue(new PacketData(packet.Raw.Length, Util.Now, kind));
			}
		}

		public void LogInPacket(IncomingPacket packet)
		{
			var kind = TypeToKind(packet.PacketType);
			inPackets[(int)kind]++;
			inBytes[(int)kind] += packet.Raw.Length;
			lock (queueLock)
			{
				DropOver(inBytesTime, TimeMinute);
				inBytesTime.Enqueue(new PacketData(packet.Raw.Length, Util.Now, kind));
			}
		}

		public void AddPing(TimeSpan ping)
		{
			lock (queueLock)
			{
				if (pingTimes.Count >= 60)
					pingTimes.Dequeue();
				pingTimes.Enqueue(ping);
			}
		}

		private static PacketKind TypeToKind(PacketType type)
		{
			switch (type)
			{
			case PacketType.Voice:
			case PacketType.VoiceWhisper:
				return PacketKind.Speech;
			case PacketType.Command:
			case PacketType.CommandLow:
			case PacketType.Ack:
			case PacketType.AckLow:
			case PacketType.Init1:
				return PacketKind.Control;
			case PacketType.Ping:
			case PacketType.Pong:
				return PacketKind.Keepalive;
			default:
				throw new ArgumentOutOfRangeException(nameof(type));
			}
		}

		private static long[] GetWithin(Queue<PacketData> queue, TimeSpan time)
		{
			var now = Util.Now;
			long[] bandwidth = new long[3];
			foreach (var pack in queue.Reverse())
				if (now - pack.SendPoint <= time)
					bandwidth[(int)pack.Kind] += pack.Size;
				else
					break;
			for (int i = 0; i < 3; i++)
				bandwidth[i] = (long)(bandwidth[i] / time.TotalSeconds);
			return bandwidth;
		}

		private static void DropOver(Queue<PacketData> queue, TimeSpan time)
		{
			var now = Util.Now;
			while (queue.Any() && now - queue.Peek().SendPoint > time)
				queue.Dequeue();
		}

		public Ts3Command GenerateStatusAnswer()
		{
			long[] lastSecondIn;
			long[] lastSecondOut;
			long[] lastMinuteIn;
			long[] lastMinuteOut;
			double lastPing;
			double avgPing;
			lock (queueLock)
			{
				lastSecondIn = GetWithin(inBytesTime, TimeSecond);
				lastSecondOut = GetWithin(outBytesTime, TimeSecond);
				lastMinuteIn = GetWithin(inBytesTime, TimeMinute);
				lastMinuteOut = GetWithin(outBytesTime, TimeMinute);
				if (pingTimes.Any())
				{
					lastPing = pingTimes.Last().Milliseconds;
					avgPing = pingTimes.Average(ts => ts.Milliseconds);
				}
				else
				{
					lastPing = avgPing = 0;
				}
			}

			return new Ts3Command("setconnectioninfo", new List<CommandParameter>()
			{
				new CommandParameter("connection_ping", lastPing),
				new CommandParameter("connection_ping_deviation", Math.Abs(lastPing - avgPing)),
				new CommandParameter("connection_packets_sent_speech", outPackets[(int)PacketKind.Speech]),
				new CommandParameter("connection_packets_sent_keepalive", outPackets[(int)PacketKind.Keepalive]),
				new CommandParameter("connection_packets_sent_control", outPackets[(int)PacketKind.Control]),
				new CommandParameter("connection_bytes_sent_speech", outBytes[(int)PacketKind.Speech]),
				new CommandParameter("connection_bytes_sent_keepalive", outBytes[(int)PacketKind.Keepalive]),
				new CommandParameter("connection_bytes_sent_control", outBytes[(int)PacketKind.Control]),
				new CommandParameter("connection_packets_received_speech", inPackets[(int)PacketKind.Speech]),
				new CommandParameter("connection_packets_received_keepalive", inPackets[(int)PacketKind.Keepalive]),
				new CommandParameter("connection_packets_received_control", inPackets[(int)PacketKind.Control]),
				new CommandParameter("connection_bytes_received_speech", inBytes[(int)PacketKind.Speech]),
				new CommandParameter("connection_bytes_received_keepalive", inBytes[(int)PacketKind.Keepalive]),
				new CommandParameter("connection_bytes_received_control", inBytes[(int)PacketKind.Control]),
				new CommandParameter("connection_server2client_packetloss_speech", 42.0000f),
				new CommandParameter("connection_server2client_packetloss_keepalive", 1.0000f),
				new CommandParameter("connection_server2client_packetloss_control", 0.5000f),
				new CommandParameter("connection_server2client_packetloss_total", 0.0000f),
				new CommandParameter("connection_bandwidth_sent_last_second_speech", lastSecondOut[(int)PacketKind.Speech]),
				new CommandParameter("connection_bandwidth_sent_last_second_keepalive", lastSecondOut[(int)PacketKind.Keepalive]),
				new CommandParameter("connection_bandwidth_sent_last_second_control", lastSecondOut[(int)PacketKind.Control]),
				new CommandParameter("connection_bandwidth_sent_last_minute_speech", lastMinuteOut[(int)PacketKind.Speech]),
				new CommandParameter("connection_bandwidth_sent_last_minute_keepalive", lastMinuteOut[(int)PacketKind.Keepalive]),
				new CommandParameter("connection_bandwidth_sent_last_minute_control", lastMinuteOut[(int)PacketKind.Control]),
				new CommandParameter("connection_bandwidth_received_last_second_speech", lastSecondIn[(int)PacketKind.Speech]),
				new CommandParameter("connection_bandwidth_received_last_second_keepalive", lastSecondIn[(int)PacketKind.Keepalive]),
				new CommandParameter("connection_bandwidth_received_last_second_control", lastSecondIn[(int)PacketKind.Control]),
				new CommandParameter("connection_bandwidth_received_last_minute_speech", lastMinuteIn[(int)PacketKind.Speech]),
				new CommandParameter("connection_bandwidth_received_last_minute_keepalive", lastMinuteIn[(int)PacketKind.Keepalive]),
				new CommandParameter("connection_bandwidth_received_last_minute_control", lastMinuteIn[(int)PacketKind.Control]),
			});
		}

		public void Reset()
		{
			Array.Clear(outPackets, 0, outPackets.Length);
			Array.Clear(inPackets, 0, inPackets.Length);
			Array.Clear(outBytes, 0, outBytes.Length);
			Array.Clear(inBytes, 0, inBytes.Length);
			outBytesTime.Clear();
			inBytesTime.Clear();
		}

		private enum PacketKind
		{
			Speech,
			Keepalive,
			Control,
		}

		private struct PacketData
		{
			public long Size { get; }
			public DateTime SendPoint { get; }
			public PacketKind Kind { get; }

			public PacketData(long size, DateTime sendPoint, PacketKind kind) { Size = size; SendPoint = sendPoint; Kind = kind; }
		}
	}
}
