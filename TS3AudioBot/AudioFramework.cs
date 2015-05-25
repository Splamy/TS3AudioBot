using System;
using System.Collections.Generic;

namespace TS3AudioBot
{
	class AudioFramework
	{
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

		public bool StartRessource(AudioRessource audioRessource)
		{
			if (audioRessource == null)
				return false;

			LogAudioRessource(audioRessource);

			Stop();

			if (!audioRessource.Play(playerConnection))
				return false;

			currentRessource = audioRessource;
			return true;
		}

		public void Stop()
		{
			if (currentRessource != null)
			{
				playerConnection.AudioStop();
			}
		}

		public void Close()
		{
			Console.WriteLine("Closing Mediaplayer...");
			playerConnection.Close();
		}

		private void LogAudioRessource(AudioRessource ar)
		{
			if (ressourceLog == null)
				ressourceLog = new List<AudioRessource>();
			ressourceLog.Add(ar);
		}
	}

	abstract class AudioRessource
	{
		public abstract AudioType AudioType { get; }

		public string RessourceName { get; private set; }

		public abstract bool Play(IPlayerConnection mediaPlayer);

		public AudioRessource(string ressourceName)
		{
			RessourceName = ressourceName;
		}
	}

	class MediaRessource : AudioRessource
	{
		public override AudioType AudioType { get { return AudioType.MediaLink; } }

		public MediaRessource(string path)
			: base(path)
		{
		}

		public override bool Play(IPlayerConnection mediaPlayer)
		{
			mediaPlayer.AudioPlay(RessourceName);
			return true;
		}
	}

	enum AudioType
	{
		MediaLink,
		Youtube,
	}
}
