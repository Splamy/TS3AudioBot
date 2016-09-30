using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TS3Client.Full;
using System.Diagnostics;
using System.Net;
using static System.Console;

namespace TS3Client
{
	class DebugTests
	{
		static void Main(string[] args)
		{
			var test = new OutgoingPacket(new byte[5], PacketType.Command);

			const int tests = 100000;
			var rnd = new Random();
			Stopwatch sw = new Stopwatch();
			const int len = 1;
			byte[] bufferA = new byte[len];
			byte[] bufferB = new byte[len];
			byte[] bufferC = new byte[len];

			sw.Restart();
			for (int i = 0; i < tests; i++)
			{
				rnd.NextBytes(bufferA);
				rnd.NextBytes(bufferB);
				TS3Crypt.XorBinary(bufferA, bufferB, len, bufferC);
			}
			sw.Stop();
			WriteLine("Norm: {0}", sw.ElapsedMilliseconds);

			sw.Restart();
			for (int i = 0; i < tests; i++)
			{
				rnd.NextBytes(bufferA);
				rnd.NextBytes(bufferB);
				//XorBinaryOpt(bufferA, bufferB, len, bufferC);
			}
			sw.Stop();
			WriteLine("Opt: {0}", sw.ElapsedMilliseconds);


			ReadLine();
		}
	}
}
