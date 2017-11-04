namespace TS3Client
{
	using System;
	using System.Diagnostics;
	using Full;
	using System.Runtime.CompilerServices;

	internal static class ColorDbg
	{
		private static void WriteType(string type, ConsoleColor typeColor = ConsoleColor.Cyan)
		{
			Console.ForegroundColor = typeColor;
			Console.Write("{0} ", type);
			Console.ResetColor();
		}

		private static void Write(string text, ConsoleColor color)
		{
			Console.ForegroundColor = color;
			Console.Write(text);
			Console.ResetColor();
		}

		[Conditional("COLOG")]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void WriteLine(string type, string text)
		{
			WriteType(type);
			Write(text, ConsoleColor.Gray);
			Console.WriteLine();
		}

		[Conditional("COLOG_RTT")]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void WriteRtt(TimeSpan smoothedRtt, TimeSpan smoothedRttVar, TimeSpan currentRto)
		{
			WriteType("RTT");
			Console.Write("SRTT:");
			Write(smoothedRtt.ToString(), ConsoleColor.Cyan);
			Console.Write("RTTVAR:");
			Write(smoothedRttVar.ToString(), ConsoleColor.Cyan);
			Console.Write("RTO:");
			Write(currentRto.ToString(), ConsoleColor.Cyan);
			Console.WriteLine();
		}

		[Conditional("COLOG_RAWPKG")]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void WritePkgOut(OutgoingPacket packet)
		{
			if (packet.PacketType == PacketType.Ping || packet.PacketType == PacketType.Pong)
				return;
			WriteType("[O]");
			switch (packet.PacketType)
			{
			case PacketType.Init1:
				Console.Write("InitID: ");
				Write(packet.Data[4].ToString(), ConsoleColor.Magenta);
				break;
			case PacketType.Ack:
			case PacketType.AckLow:
				Console.Write("Acking: ");
				Write(NetUtil.N2Hushort(packet.Data, 0).ToString(), ConsoleColor.Magenta);
				break;
			default:
				Console.Write(packet);
				break;
			}
			Console.WriteLine();
		}

		[Conditional("COLOG_RAWPKG")]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void WritePkgIn(IncomingPacket packet)
		{
			if (packet.PacketType == PacketType.Ping || packet.PacketType == PacketType.Pong)
				return;
			WriteType("[I]");
			switch (packet.PacketType)
			{
			case PacketType.Init1:
				Console.Write("InitID: ");
				Write(packet.Data[0].ToString(), ConsoleColor.Magenta);
				break;
			case PacketType.Ack:
			case PacketType.AckLow:
				Console.Write("Acking: ");
				Write(NetUtil.N2Hushort(packet.Data, 0).ToString(), ConsoleColor.Magenta);
				break;
			default:
				Console.Write(packet);
				break;
			}
			Console.WriteLine();
		}

		[Conditional("COLOG_RAWPKG")]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void WritePkgRaw(byte[] data, string op)
		{
			WriteType("[I]");
			switch (op)
			{
				case "DROPPING": Write("DROPPING ", ConsoleColor.DarkRed); break;
				case "RAW": Write("RAW ", ConsoleColor.Cyan); break;
			}
			//Console.WriteLine(Encoding.ASCII.GetString(data));
			Console.WriteLine(DebugUtil.DebugToHex(data));
		}

		[Conditional("COLOG_TIMEOUT")]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void WriteResend(BasePacket packet, string op = "")
		{
			WriteType("PKG");
			switch (op)
			{
			case "RESEND": Write("RESEND ", ConsoleColor.Yellow); break;
			case "TIMEOUT": Write("TIMEOUT ", ConsoleColor.Red); break;
			case "QUEUE": Write("IN QUEUE ", ConsoleColor.Green); break;
			}
			Console.WriteLine(packet);
		}

		[Conditional("COLOG_DETAILED")]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void WriteDetail(string detail, string plus = "")
		{
			WriteType("+++");
			switch (plus)
			{
			case "INIT": Write("INIT ", ConsoleColor.Magenta); break;
			}
			Console.WriteLine(detail);
		}
	}
}
