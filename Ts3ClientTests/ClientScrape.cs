using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using TS3Client;
using TS3Client.Commands;
using TS3Client.Full;
using TS3Client.Helper;
using TS3Client.Messages;
using IOFileInfo = System.IO.FileInfo;

namespace Ts3ClientTests
{
	public static class ClientScrape
	{
		#region versions
		private static readonly string[] vers = new string[] {
			"1326378143",
			"1326794905",
			"1326886516",
			"1327056547",
			"1327583302",
			"1327924980",
			"1328110407",
			"1328254851",
			"1328701600",
			"1328791207",
			"1328874614",
			"1329129765",
			"1329301801",
			"1331895694",
			"1332929507",
			"1333537992",
			"1334822484",
			"1334902755",
			"1334913258",
			"1337926405",
			"1337934326",
			"1337956928",
			"1339508041",
			"1339686180",
			"1340260499",
			"1341992313",
			"1342099233",
			"1342421813",
			"1343649206",
			"1343657352",
			"1349851829",
			"1350549414",
			"1350973218",
			"1351090895",
			"1351504843",
			"1354873317",
			"1357824174",
			"1361527354",
			"1361977727",
			"1363937305",
			"1363960354",
			"1365064384",
			"1374563791",
			"1374830986",
			"1375083581",
			"1375367141",
			"1375773286",
			"1378199876",
			"1378301061",
			"1378461722",
			"1378715177",
			"1380008864",
			"1380283653",
			"1382530211",
		};

		private static readonly string[] vers_new = new string[] {
			"1375083581",
			"1375773286",
			"1378199876",
			"1378301061",
			"1378461722",
			"1378715177",
			"1380008864",
			"1380283653",
			"1382530211",
			"1387444094",
			"1392643117",
			"1393597517",
			"1394114560",
			"1394624943",
			"1401808190",
			"1402646489",
			"1403250090",
			"1405341092",
			"1406898538",
			"1407159763",
			"1437491062",
			"1437730067",
			"1438160323",
			"1438246387",
			"1438673913",
			"1441371697",
			"1442498553",
			"1442913547",
			"1442998335",
			"1444491275",
			"1445263695",
			"1445512488",
			"1455611032",
			"1457598290",
			"1459504131",
			"1461588969",
			"1466597785",
			"1466672534",
			"1468491418",
			"1471417187",
			"1472203002",
			"1475158080",
			"1476372595",
			"1476720122",
			"1478701553",
			"1480583762",
			"1481795005",
			"1484223040",
			"1486051051",
			"1486485240",
			"1486712038",
			"1487668590",
			"1489662774",
			"1490279472",
			"1491993378",
			"1496989945",
			"1497432760",
			"1498644101",
			"1498740787",
			"1499699899",
			"1500360741",
			"1500537355",
			"1502264952",
			"1502873983",
			"1512391926",
			"1513163251",
			"1516099541",
			"1516349129",
			"1516614607",
		};
		#endregion

		public static void Main()
		{
			foreach (var ver in vers.Skip(0))
			{
				Ts3Server serv = null;
				Process ts3 = null;
				try
				{
					serv = new Ts3Server();
					serv.Listen(new IPEndPoint(IPAddress.Any, 9987));
					// http://ftp.4players.de/pub/hosted/ts3/updater-images/client/
					// .\CAnydate.exe C:\TS\Any http://files.teamspeak-services.com/updater-images/client/1516349129 win32

					Process anyd = new Process()
					{
						StartInfo = new ProcessStartInfo
						{
							FileName = @"C:\TS\CAnydate.exe",
							Arguments = $@"C:\TS\Any http://ftp.4players.de/pub/hosted/ts3/updater-images/client/{ver} win32"
						}
					};
					anyd.Start();
					anyd.WaitForExit();

					ts3 = new Process()
					{
						StartInfo = new ProcessStartInfo
						{
							FileName = @"C:\TS\Any\ts3client_win32.exe",
						}
					};
					ts3.Start();

					for (int i = 0; i < 240; i++)
					{
						if (serv.Init)
							break;
						Thread.Sleep(1000);
					}

					if (!serv.Init)
					{
						Console.WriteLine("ERR! {0}", ver);
						File.WriteAllText("sign.out", $"ERR! {ver}");
						continue;
					}
				}
				catch (Exception)
				{

				}
				finally
				{
					try
					{
						ts3?.Kill();
					}
					catch { }
					serv?.Dispose();
				}
			}

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
				client.OnEachTextMessage += Client_OnTextMessageReceived;
				var data = Ts3Crypt.LoadIdentity("MCkDAgbAAgEgAiBPKKMIrHtAH/FBKchbm4iRWZybdRTk/ZiehtH0gQRg+A==", 64, 0);
				var con = new ConnectionDataFull() { Address = "127.0.0.1", Username = "TestClient", Identity = data.Unwrap(), ServerPassword = "123", VersionSign = VersionSign.VER_WIN_3_1_8 };
				client.Connect(con);
				clients.Add(client);
			}

			Console.WriteLine("End");
			Console.ReadLine();
		}

		private static void Client_OnDisconnected(object sender, DisconnectEventArgs e)
		{
			var client = (Ts3FullClient)sender;
			if (e.Error != null)
				Console.WriteLine(e.Error.ErrorFormat());
			Console.WriteLine("Disconnected id {0}", client.ClientId);
		}

		private static void Client_OnConnected(object sender, EventArgs e)
		{
			var client = (Ts3FullClient)sender;
			Console.WriteLine("Connected id {0}", client.ClientId);
			var data = client.ClientInfo(client.ClientId);

			//var channel = client.

			var folder = client.FileTransferGetFileList(1, "/");
			var resultDlX = client.FileTransferManager.DownloadFile(new IOFileInfo("test.toml"), 1, "/conf.toml");

			folder = client.FileTransferGetFileList(0, "/icons");

			var result = client.SendNotifyCommand(new Ts3Command("servergrouplist"), NotificationType.ServerGroupList).Unwrap();
			foreach (var group in result.Notifications.Cast<ServerGroupList>())
			{
				var icon = group.IconId;
				string fileName = "icon_" + icon;
				using (var fs = new IOFileInfo(fileName).Open(FileMode.OpenOrCreate, FileAccess.ReadWrite))
				{
					var resultDl = client.FileTransferManager.DownloadFile(fs, 0, "/" + fileName);
					if (resultDl.Ok)
					{
						var token = resultDl.Value;
						token.Wait();
					}
				}
			}

			// warmup
			/*for (int i = 0; i < 100; i++)
			{
				var err = client.SendChannelMessage("Hi" + i);
			}

			var sw = Stopwatch.StartNew();
			const int amnt = 1000;
			for (int i = 0; i < amnt; i++)
			{
				var err = client.SendChannelMessage("Hi" + i);
			}
			sw.Start();
			var elap = (sw.ElapsedTicks / (float)Stopwatch.Frequency);
			Console.WriteLine("{0} messages in {1}s", amnt, elap);
			Console.WriteLine("{0:0.000}ms per message", (elap / amnt) * 1000);*/

			//client.Disconnect();
			//client.Connect(con);
		}

		private static void Client_OnTextMessageReceived(object sender, TextMessage msg)
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

				var token = client.FileTransferManager.UploadFile(new IOFileInfo("img.png"), 0, "/avatar", overwrite: true);
				if (!token.Ok)
					return;
				token.Value.Wait();
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
