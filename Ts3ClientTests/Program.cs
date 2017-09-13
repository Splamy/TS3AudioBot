using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TS3Client;
using TS3Client.Messages;
using TS3Client.Commands;
using TS3Client.Full;
using System.IO;

namespace Ts3ClientTests
{
	static class Program
	{
		static ConnectionDataFull con;

		static void Main()
		{
			var clients = new List<Ts3FullClient>();

			for (int i = 0; i < 50; i++)
			{
				var client = new Ts3FullClient(EventDispatchType.AutoThreadPooled);
				client.OnConnected += Client_OnConnected;
				client.OnDisconnected += Client_OnDisconnected;
				client.OnErrorEvent += Client_OnErrorEvent;
				client.OnTextMessageReceived += Client_OnTextMessageReceived;
				var data = Ts3Crypt.LoadIdentity("MG8DAgeAAgEgAiEAqNonGuL0w/8kLlgLbl4UkH8DQQJ7fEu1tLt+mx1E+XkCIQDgQoIGcBVvAvNoiDT37iWbPQb2kYe0+VKLg67OH2eQQwIgTyijCKx7QB/xQSnIW5uIkVmcm3UU5P2YnobR9IEEYPg=", 64, 0);
				con = new ConnectionDataFull() { Address = "127.0.0.1", Username = "TestClient", Identity = data, Password = "qwer" };
				client.Connect(con);
				clients.Add(client);
			}

			Console.WriteLine("End");
			Console.ReadLine();
		}

		private static void Client_OnDisconnected(object sender, DisconnectEventArgs e)
		{
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Disconnected id {0}", client.ClientId);
		}

		private static void Client_OnConnected(object sender, EventArgs e)
		{
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Connected id {0}", client.ClientId);
			var data = client.ClientInfo(client.ClientId);
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

					var tok = client.FileTransferManager.UploadFile(new FileInfo("img.png"), 0, "/avatar", true);
					client.FileTransferManager.Wait(tok);
				}
			}
		}

		private static void Client_OnErrorEvent(object sender, CommandError e)
		{
			//var client = (Ts3FullClient)sender;
			//Console.WriteLine(e.ErrorFormat());
			//if (!client.Connected)
			//{
			//	client.Connect(con);
			//}
		}
	}
}
