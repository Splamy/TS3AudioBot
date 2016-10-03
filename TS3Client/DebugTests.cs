using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

			const int tests = 10000000;
			var rnd = new Random();
			Stopwatch sw = new Stopwatch();

			TestA(1);
			TestB(1);

			sw.Restart();
			TestA(tests);
			sw.Stop();
			WriteLine("Norm: {0}", sw.ElapsedMilliseconds);
			
			sw.Restart();
			TestB(tests);
			sw.Stop();
			WriteLine("Opt: {0}", sw.ElapsedMilliseconds);


			ReadLine();
		}

		static void TestA(int runs)
		{
			ICollection<ushort> col = null;
			for (int i = 0; i < runs; i++)
			{
				col = new LinkedList<ushort>();
				col.Add(42);
			}
			col?.Clear();
		}

		static void TestB(int runs)
		{
			ICollection<ushort> col = null;
			for (int i = 0; i < runs; i++)
			{
				col = new List<ushort>();
				col.Add(42);
			}
			col?.Clear();
		}
	}
}
