using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TS3Client;
using TS3Client.Helper;
using TS3Client.Full;
using TS3Client.Messages;

// ReSharper disable All
namespace Ts3ClientTests
{
	static class Program
	{
		static ConnectionDataFull con;

		public static string ToHex(this IEnumerable<byte> seq) => string.Join(" ", seq.Select(x => x.ToString("X2")));
		public static byte[] FromHex(this string hex) => hex.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray();

		static void Main()
		{
			//var crypt = new Ts3Crypt();
			//crypt.Test();
			//return;

			var clients = new List<Ts3FullClient>();

			//for (int i = 0; i < 50; i++)
			{
				var client = new Ts3FullClient(EventDispatchType.AutoThreadPooled);
				client.OnConnected += Client_OnConnected;
				client.OnDisconnected += Client_OnDisconnected;
				client.OnErrorEvent += Client_OnErrorEvent;
				client.OnTextMessageReceived += Client_OnTextMessageReceived;
				var data = Ts3Crypt.LoadIdentity("MCkDAgbAAgEgAiBPKKMIrHtAH/FBKchbm4iRWZybdRTk/ZiehtH0gQRg+A==", 64, 0);
				con = new ConnectionDataFull() { Address = "127.0.0.1", Username = "TestClient", Identity = data.Unwrap(), ServerPassword = "123", VersionSign = VersionSign.VER_WIN_3_1_8 };
				client.Connect(con);
				clients.Add(client);
			}

			Console.WriteLine("End");
			Console.ReadLine();
		}

		private static void Client_OnDisconnected(object sender, DisconnectEventArgs e)
		{
			var client = (Ts3FullClient)sender;
			if(e.Error!= null)
				Console.WriteLine(e.Error.ErrorFormat());
			Console.WriteLine("Disconnected id {0}", client.ClientId);
		}

		private static void Client_OnConnected(object sender, EventArgs e)
		{
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Connected id {0}", client.ClientId);
			var data = client.ClientInfo(client.ClientId);

			/*var sw = System.Diagnostics.Stopwatch.StartNew();
			const int amnt = 1000;
			for (int i = 0; i < amnt; i++)
			{
				client.SendChannelMessage("Hi" + i);
			}
			sw.Start();
			var elap = (sw.ElapsedTicks / (float)System.Diagnostics.Stopwatch.Frequency);
			Console.WriteLine("{0} messages in {1}s", amnt, elap);
			Console.WriteLine("{0:0.000}ms per message", elap / amnt * 1000);*/

			//client.Disconnect();
			//client.Connect(con);
		}

		private static void Client_OnTextMessageReceived(object sender, IEnumerable<TextMessage> e)
		{
			foreach (var msg in e)
			{
				if (msg.Message == "Hi")
					Console.WriteLine("Hi" + msg.InvokerName);
				else if (msg.Message == "Exit")
				{
					var client = (Ts3FullClient)sender;
					var id = client.ClientId;
					Console.WriteLine("Exiting... {0}", id);
					client.Disconnect();
					Console.WriteLine("Exited... {0}", id);
				}
				else if (msg.Message == "upl")
				{
					var client = (Ts3FullClient)sender;

					var token = client.FileTransferManager.UploadFile(new FileInfo("img.png"), 0, "/avatar", true);
					if (!token.Ok)
						return;
					token.Value.Wait();
				}
			}
		}

		private static void Client_OnErrorEvent(object sender, CommandError e)
		{
			//var client = (Ts3FullClient)sender;
			Console.WriteLine(e.ErrorFormat());
			//if (!client.Connected)
			//{
			//	client.Connect(con);
			//}
		}
	}
}
