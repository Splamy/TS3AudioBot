// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using TSLib.Commands;
using TSLib.Helper;

namespace TSLib.Full
{
	// TODO include udp/ip header size to get correct values
	/// <summary>Provides connection stats by logging packets.</summary>
	public sealed class NetworkStats
	{
		private readonly long[] outPackets = new long[3];
		private readonly long[] inPackets = new long[3];
		private readonly long[] outBytes = new long[3];
		private readonly long[] inBytes = new long[3];
		private readonly Queue<PacketData> outBytesTime = new Queue<PacketData>();
		private readonly Queue<PacketData> inBytesTime = new Queue<PacketData>();
		private readonly Queue<TimeSpan> pingTimes = new Queue<TimeSpan>(60);
		private static readonly TimeSpan TimeSecond = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan TimeMinute = TimeSpan.FromMinutes(1);
		private readonly object queueLock = new object();

		internal void LogOutPacket<TDir>(ref Packet<TDir> packet)
		{
			var kind = TypeToKind(packet.PacketType);
			outPackets[(int)kind]++;
			outBytes[(int)kind] += packet.Raw.Length;
			lock (queueLock)
			{
				DropOver(outBytesTime, TimeMinute);
				outBytesTime.Enqueue(new PacketData((ushort)packet.Raw.Length, Tools.Now, kind));
			}
		}

		internal void LogInPacket<TDir>(ref Packet<TDir> packet)
		{
			var kind = TypeToKind(packet.PacketType);
			inPackets[(int)kind]++;
			inBytes[(int)kind] += packet.Raw.Length;
			lock (queueLock)
			{
				DropOver(inBytesTime, TimeMinute);
				inBytesTime.Enqueue(new PacketData((ushort)packet.Raw.Length, Tools.Now, kind));
			}
		}

		public void LogLostPings(int count)
		{
			// TODO
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
			var now = Tools.Now;
			var bandwidth = new long[3];
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
			var now = Tools.Now;
			while (queue.Count > 0 && now - queue.Peek().SendPoint > time)
				queue.Dequeue();
		}

		public TsCommand GenerateStatusAnswer()
		{
			long[] lastSecondIn;
			long[] lastSecondOut;
			long[] lastMinuteIn;
			long[] lastMinuteOut;
			double lastPing;
			double deviationPing;
			lock (queueLock)
			{
				lastSecondIn = GetWithin(inBytesTime, TimeSecond);
				lastSecondOut = GetWithin(outBytesTime, TimeSecond);
				lastMinuteIn = GetWithin(inBytesTime, TimeMinute);
				lastMinuteOut = GetWithin(outBytesTime, TimeMinute);
				if (pingTimes.Count > 0)
				{
					lastPing = pingTimes.Last().TotalMilliseconds;
					deviationPing = StdDev(pingTimes.Select(ts => ts.TotalMilliseconds));
				}
				else
				{
					lastPing = deviationPing = 0;
				}
			}

			return new TsCommand("setconnectioninfo") {
				{ "connection_ping", Math.Round(lastPing, 0) },
				{ "connection_ping_deviation", deviationPing },
				{ "connection_packets_sent_speech", outPackets[(int)PacketKind.Speech] },
				{ "connection_packets_sent_keepalive", outPackets[(int)PacketKind.Keepalive] },
				{ "connection_packets_sent_control", outPackets[(int)PacketKind.Control] },
				{ "connection_bytes_sent_speech", outBytes[(int)PacketKind.Speech] },
				{ "connection_bytes_sent_keepalive", outBytes[(int)PacketKind.Keepalive] },
				{ "connection_bytes_sent_control", outBytes[(int)PacketKind.Control] },
				{ "connection_packets_received_speech", inPackets[(int)PacketKind.Speech] },
				{ "connection_packets_received_keepalive", inPackets[(int)PacketKind.Keepalive] },
				{ "connection_packets_received_control", inPackets[(int)PacketKind.Control] },
				{ "connection_bytes_received_speech", inBytes[(int)PacketKind.Speech] },
				{ "connection_bytes_received_keepalive", inBytes[(int)PacketKind.Keepalive] },
				{ "connection_bytes_received_control", inBytes[(int)PacketKind.Control] },
				{ "connection_server2client_packetloss_speech", 42.0000f },
				{ "connection_server2client_packetloss_keepalive", 1.0000f },
				{ "connection_server2client_packetloss_control", 0.5000f },
				{ "connection_server2client_packetloss_total", 0.0000f },
				{ "connection_bandwidth_sent_last_second_speech", lastSecondOut[(int)PacketKind.Speech] },
				{ "connection_bandwidth_sent_last_second_keepalive", lastSecondOut[(int)PacketKind.Keepalive] },
				{ "connection_bandwidth_sent_last_second_control", lastSecondOut[(int)PacketKind.Control] },
				{ "connection_bandwidth_sent_last_minute_speech", lastMinuteOut[(int)PacketKind.Speech] },
				{ "connection_bandwidth_sent_last_minute_keepalive", lastMinuteOut[(int)PacketKind.Keepalive] },
				{ "connection_bandwidth_sent_last_minute_control", lastMinuteOut[(int)PacketKind.Control] },
				{ "connection_bandwidth_received_last_second_speech", lastSecondIn[(int)PacketKind.Speech] },
				{ "connection_bandwidth_received_last_second_keepalive", lastSecondIn[(int)PacketKind.Keepalive] },
				{ "connection_bandwidth_received_last_second_control", lastSecondIn[(int)PacketKind.Control] },
				{ "connection_bandwidth_received_last_minute_speech", lastMinuteIn[(int)PacketKind.Speech] },
				{ "connection_bandwidth_received_last_minute_keepalive", lastMinuteIn[(int)PacketKind.Keepalive] },
				{ "connection_bandwidth_received_last_minute_control", lastMinuteIn[(int)PacketKind.Control] },
			};
		}

		private static double StdDev(IEnumerable<double> values)
		{
			double avg = values.Average();
			double sum = 0;
			int n = 0;
			foreach (double val in values)
			{
				n++;
				sum += (val - avg) * (val - avg);
			}
			if (n > 1)
				return Math.Sqrt(sum / (n - 1));
			return 0;
		}

		public void Reset()
		{
			Array.Clear(outPackets, 0, outPackets.Length);
			Array.Clear(inPackets, 0, inPackets.Length);
			Array.Clear(outBytes, 0, outBytes.Length);
			Array.Clear(inBytes, 0, inBytes.Length);
			lock (queueLock)
			{
				outBytesTime.Clear();
				inBytesTime.Clear();
				pingTimes.Clear();
			}
		}

		private enum PacketKind : ushort
		{
			Speech,
			Keepalive,
			Control,
		}

		private readonly struct PacketData
		{
			public DateTime SendPoint { get; }
			public ushort Size { get; }
			public PacketKind Kind { get; }

			public PacketData(ushort size, DateTime sendPoint, PacketKind kind) { Size = size; SendPoint = sendPoint; Kind = kind; }
		}
	}
}
