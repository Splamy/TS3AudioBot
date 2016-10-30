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
	static class DebugTests
	{
		static void Main(string[] args)
		{
			Ts3FullClient fc = new Ts3FullClient(EventDispatchType.DoubleThread);
			fc.Connect(new ConnectionDataFull
			{
				Username = "HAAAX",
				Hostname = "splamy.de",
				Port = 9987,
				Identity = Ts3Crypt.LoadIdentity("MG8DAgeAAgEgAiEA76LIMLxiti7JTkl4yeNRPiApiGyIRqF9km3ByalVZd8CIQDGz9jUYZIXgkSsyCYVywl0HTKoP+0Ch8OG+ia4boW0UAIgSY/aeQNjq0ryRiaifd6SMKbG9+KuoN/oXEu/lyr+SNg=", 57451630, 57451630),
			});
			fc.EnterEventLoop();
			ReadLine();
		}
	}
}
