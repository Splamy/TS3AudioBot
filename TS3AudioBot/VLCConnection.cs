using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Diagnostics;

namespace TS3AudioBot
{
	class VLCConnection : IPlayerConnection
	{
		private string botLocation;
		private bool connected;
		private string password;
		private Process vlcproc = null;
		private TcpClient vlcInterface;
		private StreamReader streamRead;
		private NetworkStream netStream;
		private Task textCallbackTask;
		public Action<string> TextCallback { get; set; }

		public VLCConnection()
		{
			connected = false;
			botLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
		}

		public void Start()
		{
			if (vlcproc == null || vlcproc.HasExited)
			{
				vlcproc = GetVlcProc();
				Connect("localhost", 4212);
			}
		}

		public void Close()
		{
			if (streamRead != null)
				streamRead.Close();
			if (netStream != null)
				netStream.Close();
			if (vlcInterface != null)
			{
				if (vlcInterface.Connected)
					vlcInterface.Close();
				vlcInterface = null;
			}

			if (vlcproc != null && !vlcproc.HasExited)
				vlcproc.Kill();
			vlcproc = null;

			if (textCallbackTask != null)
				textCallbackTask.Wait();
		}

		// VLC Commands

		public void AudioStop()
		{
			SendCommandRaw("stop");
		}

		public void AudioPlay(string url)
		{
			SendCommandRaw("add " + url);
		}

		public void AudioLogin()
		{
			SendCommandRaw(password);
		}

		public void SendCommandRaw(string msg)
		{
			Byte[] cmd = System.Text.Encoding.ASCII.GetBytes(msg + "\n");
			netStream.Write(cmd, 0, cmd.Length);
		}

		// Internal stuff

		private void Connect(string hostname, int port)
		{
			textCallbackTask = Task.Run(() =>
			{
				connected = false;
				vlcInterface = new TcpClient();
				while (vlcInterface != null && !connected)
				{
					try
					{
						vlcInterface.Connect(hostname, port);
						connected = true;
					}
					catch (Exception)
					{
						Task.Delay(1000).Wait();
						Console.WriteLine("Retry: Connect to VLC");
					}
				}

				if (!connected)
				{
					Console.WriteLine("Could not connect to VLC...");
					return;
				}
				else
				{
					Console.WriteLine("Connected to VLC");
				}

				netStream = vlcInterface.GetStream();
				streamRead = new StreamReader(netStream);

				try
				{
					while (vlcInterface != null)
					{
						StringBuilder sb = new StringBuilder();
						do
						{
							int b = netStream.ReadByte();
							if (b < 0 || b == '\n')
								break;
							sb.Append((char)b);
						} while (netStream.DataAvailable);
						if (TextCallback != null)
							TextCallback(sb.ToString());
					}
				}
				catch (Exception ex) { Console.WriteLine("Disconnected ({0})...", ex.Message); }
			});
		}

		private Process GetVlcProc()
		{
			Process tmproc = new Process();
			try
			{
				password = generatePassword();
				ProcessStartInfo psi = new ProcessStartInfo()
				{
					FileName = Util.GetSubTaskPath(SubTask.VLC),
					Arguments = "--intf telnet"
								+ " --telnet-password " + password
								+ " --vout none"
						//+ " --rtsp-host 127.0.0.1:5554"
						//+ " --play-and-exit \"" + param + "\""
						,
					WorkingDirectory = botLocation,
				};
				tmproc.StartInfo = psi;
				tmproc.Start();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Could not start VLC: " + ex.Message);
				tmproc = null;
			}

			return tmproc;
		}

		private string generatePassword(int lenght = 5)
		{
			string alphnum = "abcdefghijklmnopqrstuvwyxzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			Random rnd = new Random();
			StringBuilder strb = new StringBuilder();
			for (int i = 0; i < lenght; i++)
			{
				strb.Append(alphnum[rnd.Next(0, alphnum.Length)]);
			}
			return strb.ToString();
		}
	}
}
