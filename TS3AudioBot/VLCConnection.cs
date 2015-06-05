using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Diagnostics;
using LockCheck;

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

		private readonly object attributeLock = new object();
		private readonly object responseLock = new object();

		private AwaitingResponse currentResponse = AwaitingResponse.None;
		private bool isPlaying = false;
		private int getLength = -1;
		private int getPosition = -1;

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

		public void AudioAdd(string url)
		{
			SendCommandLocked("enqueue " + url);
		}

		public void AudioClear()
		{
			SendCommandLocked("clear");
		}

		public void AudioNext()
		{
			SendCommandLocked("next");
		}

		public void AudioPrevious()
		{
			SendCommandLocked("prev");
		}

		public void AudioStart(string url)
		{
			SendCommandLocked("add " + url);
		}

		public void AudioPlay()
		{
			SendCommandLocked("play");
		}

		public void AudioStop()
		{
			SendCommandLocked("stop");
		}

		public int GetLength()
		{
			SendResponseLocked(AwaitingResponse.GetLength, "get_length");
			return getLength;
		}

		public int GetPosition()
		{
			SendResponseLocked(AwaitingResponse.GetPosition, "get_time");
			return getPosition;
		}

		public bool IsPlaying()
		{
			SendResponseLocked(AwaitingResponse.IsPlaing, "is_playing");
			return isPlaying;
		}

		public void SetLoop(bool enabled)
		{
			SendCommandLocked("loop " + (enabled ? "on" : "off"));
		}

		public void SetPosition(int position)
		{
			SendCommandLocked("seek " + position);
		}

		public void SetRepeat(bool enabled)
		{
			SendCommandLocked("repeat " + (enabled ? "on" : "off"));
		}

		public void SetVolume(int value)
		{
			SendCommandLocked("volume " + value);
		}

		// Lock and textsend methods

		[LockCritical("attributeLock", "responseLock")]
		private void SendResponseLocked(AwaitingResponse resp, string msg)
		{
			lock (attributeLock)
			{
				lock (responseLock)
				{
					currentResponse = resp;
					SendTextRaw(msg);
					Monitor.Wait(responseLock);
				}
			}
		}

		[LockCritical("attributeLock")]
		private void SendCommandLocked(string msg)
		{
			lock (attributeLock)
			{
				SendTextRaw(msg);
			}
		}

		private void SendTextRaw(string msg)
		{
			if (connected)
			{
				try
				{
					Byte[] cmd = System.Text.Encoding.ASCII.GetBytes(msg + "\n");
					netStream.Write(cmd, 0, cmd.Length);
				}
				catch (Exception ex)
				{
					connected = false;
					Console.WriteLine("VLCConnection: Unexpected write failure ({0})", ex);
				}
			}
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
						ProcessMessage(sb.ToString());
					}
				}
				catch (Exception ex) { Console.WriteLine("Disconnected ({0})...", ex.Message); }
			});
		}

		[LockCritical("responseLock")]
		private void ProcessMessage(string msg)
		{
			if (msg.StartsWith("Password:"))
			{
				SendCommandLocked(password);
			}
			else
			{
				switch (currentResponse)
				{
				case AwaitingResponse.GetLength:
					int get_length = -1;
					if (int.TryParse(msg, out get_length))
						getLength = get_length;
					else
						getLength = -1;
					break;
				case AwaitingResponse.GetPosition:
					int get_position = -1;
					if (int.TryParse(msg, out get_position))
						getPosition = get_position;
					else
						getPosition = -1;
					break;
				case AwaitingResponse.IsPlaing:
					int is_plaing = -1;
					if (int.TryParse(msg, out is_plaing))
						isPlaying = is_plaing != 0;
					else
						isPlaying = false;
					break;
				}
				currentResponse = AwaitingResponse.None;
			}
			lock (responseLock)
			{
				Monitor.Pulse(responseLock);
			}
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
			const string alphnum = "abcdefghijklmnopqrstuvwyxzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			Random rnd = new Random();
			StringBuilder strb = new StringBuilder();
			for (int i = 0; i < lenght; i++)
			{
				strb.Append(alphnum[rnd.Next(0, alphnum.Length)]);
			}
			return strb.ToString();
		}

		private enum AwaitingResponse
		{
			None,
			GetLength,
			GetPosition,
			IsPlaing,
		}
	}
}
