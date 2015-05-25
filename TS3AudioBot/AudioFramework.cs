using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace TS3AudioBot
{
	class AudioFramework
	{
		private bool ts3clientrunning = false;
		private AudioRessource currentRessource = null;
		private List<AudioRessource> ressourceLog = null;
		public IPlayerConnection playerConnection;

		public bool SuppressOutput { get; set; }
		public bool Loop { get; set; }

		public AudioFramework()
		{
			SuppressOutput = true;
			playerConnection = new VLCConnection();
			playerConnection.Start();
			playerConnection.TextCallback = ProcessVLCOutput;
		}

		private void ProcessVLCOutput(string text)
		{
			if (text.StartsWith("Password:"))
			{
				playerConnection.AudioLogin();
			}
			else if (!SuppressOutput)
			{
				if (text.StartsWith(">"))
				{
					Console.Write(text);
				}
				else
				{
					Console.WriteLine(text);
				}
			}
		}

		public bool OpenLocalVLC(string path)
		{
			AudioRessource ar = new AudioRessource()
			{
				path = path,
				audioType = AudioType.LocalVLC,
			};
			return StartRessource(ar);
		}

		public bool OpenNetworkVLC(string path)
		{
			AudioRessource ar = new AudioRessource()
			{
				path = path,
				audioType = AudioType.NetworkVLC,
			};
			return StartRessource(ar);
		}

		private bool StartRessource(AudioRessource audioRessource)
		{
			LogAudioRessource(audioRessource);

			Stop();

			switch (audioRessource.audioType)
			{
			case AudioType.LocalVLC:
			case AudioType.NetworkVLC:
				playerConnection.AudioPlay(audioRessource.path);
				break;
			}

			currentRessource = audioRessource;
			return true;
		}

		public void Stop()
		{
			if (currentRessource != null)
			{
				switch (currentRessource.audioType)
				{
				case AudioType.LocalVLC:
				case AudioType.NetworkVLC:
					playerConnection.AudioStop();
					break;
				}
			}
		}

		public void Close()
		{
			Console.WriteLine("Closing TSClient...");
			StopBotClient();
			Console.WriteLine("Closing Mediaplayer...");
			playerConnection.Close();
		}

		private void LogAudioRessource(AudioRessource ar)
		{
			if (ressourceLog == null)
				ressourceLog = new List<AudioRessource>();
			ressourceLog.Add(ar);
		}

		public void StartBotClient()
		{
			if (!ts3clientrunning)
			{
				StartScript("StartTsBot.sh");
			}
		}

		public void StopBotClient()
		{
			if (StartScript("StopTsBot.sh"))
			{
				ts3clientrunning = false;
			}
		}

		private bool StartScript(string name)
		{
			try
			{
				Process tmproc = new Process();
				ProcessStartInfo psi = new ProcessStartInfo()
				{
					FileName = name,
				};
				tmproc.StartInfo = psi;
				tmproc.Start();
				return true;
			}
			catch (Exception)
			{
				Console.WriteLine("Error! {0} couldn't be run/found", name);
				return false;
			}
		}
	}

	class AudioRessource
	{
		public AudioType audioType;
		public string path;
	}

	class YoutubeRessource : AudioRessource
	{
		public IReadOnlyList<VideoType> availableTypes;
		public int lastSelected;
	}

	enum AudioType
	{
		LocalVLC,
		NetworkVLC,
	}
}
