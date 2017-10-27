namespace TS3Client
{
	using System;
	using System.Diagnostics;
	using Full;

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
		public static void WriteLine(string type, string text)
		{
			WriteType(type);
		}

		[Conditional("COLOG_RTT")]
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
		public static void WritePkgOut(OutgoingPacket packet)
		{
			if (packet.PacketType == PacketType.Ping || packet.PacketType == PacketType.Pong)
				return;
			WriteType("[O]");
			switch (packet.PacketType)
			{
			case PacketType.Init1:
				Console.Write("ID: ");
				Write(packet.Data[4].ToString(), ConsoleColor.Magenta);
				break;
			default:
				Console.Write(packet);
				break;
			}
			Console.WriteLine();
		}

		[Conditional("COLOG_RAWPKG")]
		public static void WritePkgIn(IncomingPacket packet)
		{
			if (packet.PacketType == PacketType.Ping || packet.PacketType == PacketType.Pong)
				return;
			WriteType("[I]");
			switch (packet.PacketType)
			{
			case PacketType.Init1:
				Console.Write("ID: ");
				Write(packet.Data[0].ToString(), ConsoleColor.Magenta);
				break;
			default:
				Console.Write(packet);
				break;
			}
			Console.WriteLine();
		}

		[Conditional("COLOG_TIMEOUT")]
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
