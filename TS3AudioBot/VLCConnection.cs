namespace TS3AudioBot
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Net.Sockets;
	using System.Text;
	using System.Threading;
	using TS3AudioBot.Helper;

	[Obsolete]
	class VLCConnection : IPlayerConnection
	{
		private string botLocation;
		private string vlcLocation;
		private bool connected;
		private string password;
		private Process vlcproc = null;
		private TcpClient vlcClient;
		private NetworkStream netStream;
		private Thread textCallbackThread;

		private readonly object attributeLock = new object();
		private AutoResetEvent responseEvent = new AutoResetEvent(false);

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

		public void Initialize()
		{
			if (vlcproc == null || vlcproc.HasExited)
			{
				vlcproc = GetVlcProc();
				Connect();
			}
		}

		#region IPlayerConnection

		int volume = -1;
		bool repeated = false;
		bool paused = false;

		public bool SupportsEndCallback => false;
		public event EventHandler OnSongEnd
		{
			add { throw new NotSupportedException(); }
			remove { throw new NotSupportedException(); }
		}

		public int Volume
		{
			get { return volume; }
			set
			{
				volume = value;
				SendCommandLocked("volume " + value);
			}
		}

		public TimeSpan Position
		{
			get
			{
				SendResponseLocked(AwaitingResponse.GetPosition, "get_time");
				return TimeSpan.FromSeconds(getPosition);
			}
			set
			{
				SendCommandLocked("seek " + (int)value.TotalSeconds);
			}
		}

		public bool Repeated
		{
			get { return repeated; }
			set
			{
				repeated = value;
				SendCommandLocked("repeat " + (value ? "on" : "off"));
			}
		}

		public bool Pause
		{
			get { return paused; }
			set
			{
				paused = value;
				if (value)
					SendCommandLocked("pause");
				else
					SendCommandLocked("play");
			}
		}

		public TimeSpan Length
		{
			get
			{
				SendResponseLocked(AwaitingResponse.GetLength, "get_length");
				return TimeSpan.FromSeconds(getLength);
			}
		}

		public bool IsPlaying
		{
			get
			{
				SendResponseLocked(AwaitingResponse.IsPlaying, "is_playing");
				return isPlaying;
			}
		}


		public void AudioStart(string url)
		{
			SendCommandLocked("add " + url);
		}

		public void AudioStop()
		{
			SendCommandLocked("stop");
		}

		#endregion

		// VLC Commands

		public void AudioAdd(string url)
		{
			SendCommandLocked("enqueue " + url);
		}

		public void AudioPrevious()
		{
			SendCommandLocked("prev");
		}

		public void AudioPlay()
		{
			SendCommandLocked("play");
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
				currentResponse = resp;
				SendTextRaw(msg);
				responseEvent.WaitOne();
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

		private void Connect()
		{
			connected = false;
			textCallbackThread = new Thread(ReadMessageLoop);
			textCallbackThread.Name = "VLC Read Loop";
			textCallbackThread.Start();
		}

		private void ReadMessageLoop()
		{
			using (vlcClient = new TcpClient())
			{
				while (vlcClient != null && !connected)
				{
					try
					{
						vlcClient.Connect("localhost", 4212);
						connected = true;
					}
					catch (SocketException)
					{
						Thread.Sleep(1000);
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

				netStream = vlcClient.GetStream();

				try
				{
					while (vlcClient != null)
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
				catch (IOException) { }
				catch (ObjectDisposedException) { }
				Log.Write(Log.Level.Warning, "Disconnected from VLC");
			}
			vlcClient = null;
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
				case AwaitingResponse.IsPlaying:
					int is_plaing;
					isPlaying = int.TryParse(msg, out is_plaing) && is_plaing != 0;
					break;
				}
				currentResponse = AwaitingResponse.None;
			}
			responseEvent.Set();
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

			if (responseEvent != null)
			{
				responseEvent.Dispose();
				responseEvent = null;
			}
			if (netStream != null)
			{
				netStream.Close();
				Util.WaitOrTimeout(() => vlcClient != null, 100);
				netStream = null;
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
			IsPlaying,
		}
	}
}
