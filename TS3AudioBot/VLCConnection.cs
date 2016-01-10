using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Diagnostics;
using System.ComponentModel;

namespace TS3AudioBot
{
	class VLCConnection : IPlayerConnection
	{
		private string botLocation;
		private string vlcLocation;
		private bool connected;
		private string password;
		private Process vlcproc = null;
		private TcpClient vlcInterface;
		private NetworkStream netStream;
		private Task textCallbackTask;

		private readonly object attributeLock = new object();
		private readonly object responseLock = new object();

		private AwaitingResponse currentResponse = AwaitingResponse.None;
		private bool isPlaying = false;
		private int getLength = -1;
		private int getPosition = -1;

		public VLCConnection(string vlcLocation)
		{
			connected = false;
			this.vlcLocation = vlcLocation;
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

		// VLC Commands

		int volume = -1;
		public int Volume
		{
			get
			{
				return volume;
			}
			set
			{
				volume = value;
				SendCommandLocked("volume " + value);
			}
		}

		public int Position
		{
			get
			{
				SendResponseLocked(AwaitingResponse.GetPosition, "get_time");
				return getPosition;
			}
			set
			{
				SendCommandLocked("seek " + value);
			}
		}

		bool repeated = false;
		public bool Repeated
		{
			get
			{
				return repeated;
			}
			set
			{
				repeated = value;
				SendCommandLocked("repeat " + (value ? "on" : "off"));
			}
		}

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

		public bool IsPlaying()
		{
			SendResponseLocked(AwaitingResponse.IsPlaing, "is_playing");
			return isPlaying;
		}

		public void SetLoop(bool enabled)
		{
			SendCommandLocked("loop " + (enabled ? "on" : "off"));
		}

		// Lock and textsend methods
		private void SendResponseLocked(AwaitingResponse resp, string msg)
		{
			if (!connected) return;
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

		private void SendCommandLocked(string msg)
		{
			if (!connected) return;
			lock (attributeLock)
			{
				SendTextRaw(msg);
			}
		}

		private void SendTextRaw(string msg)
		{
			if (!connected) return;
			try
			{
				byte[] cmd = Encoding.ASCII.GetBytes(msg + "\n");
				netStream.Write(cmd, 0, cmd.Length);
			}
			catch (EncoderFallbackException ex)
			{
				Log.Write(Log.Level.Warning, "VLCConnection: invalid message \"{0}\", {1}", msg, ex);
			}
			catch (IOException ex)
			{
				connected = false;
				Log.Write(Log.Level.Warning, "VLCConnection: Unexpected write failure ({0})", ex);
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
					catch (SocketException)
					{
						Task.Delay(1000).Wait();
						Log.Write(Log.Level.Warning, "Retry: Connect to VLC");
					}
				}

				if (!connected)
				{
					Log.Write(Log.Level.Error, "Could not connect to VLC...");
					return;
				}
				else
				{
					Log.Write(Log.Level.Info, "Connected to VLC");
				}

				netStream = vlcInterface.GetStream();

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
				catch (IOException ex) { Log.Write(Log.Level.Warning, "Disconnected ({0})...", ex.Message); }
				catch (ObjectDisposedException) { }
			});
		}

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
					int get_length;
					getLength = int.TryParse(msg, out get_length) ? get_length : -1;
					break;
				case AwaitingResponse.GetPosition:
					int get_position;
					getPosition = int.TryParse(msg, out get_position) ? get_position : -1;
					break;
				case AwaitingResponse.IsPlaing:
					int is_plaing;
					isPlaying = int.TryParse(msg, out is_plaing) && is_plaing != 0;
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
					FileName = vlcLocation,
					Arguments = "--intf telnet"
								+ " --telnet-password " + password
								+ " --vout none",
					WorkingDirectory = botLocation,
				};
				tmproc.StartInfo = psi;
				tmproc.Start();
			}
			catch (Win32Exception ex)
			{
				Log.Write(Log.Level.Error, "Could not start VLC: " + ex.Message);
				tmproc = null;
			}

			return tmproc;
		}

		private static string generatePassword(int lenght = 5)
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

		public void Dispose()
		{
			Log.Write(Log.Level.Info, "Closing VLC...");
			if (netStream != null)
			{
				netStream.Dispose();
			}
			if (textCallbackTask != null)
			{
				textCallbackTask.Wait();
				textCallbackTask = null;
			}
			netStream = null;
			if (vlcInterface != null)
			{
				if (vlcInterface.Connected)
					vlcInterface.Close();
				vlcInterface = null;
			}
			if (vlcproc != null)
			{
				if (!vlcproc.HasExited)
					vlcproc.Kill();
				vlcproc = null;
			}
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
