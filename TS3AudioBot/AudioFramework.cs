using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace TS3AudioBot
{
	class AudioFramework
	{
		public delegate void RessourceStartedDelegate(AudioRessource audioRessource);

		public delegate void RessourceStoppedDelegate();

		private Task ressourceEndTask;
		/// <summary>
		/// This token is used to cancel a WaitNotifyEnd task, don't change it while the task is running!
		/// </summary>
		private CancellationTokenSource ressourceEndTokenSource;
		private CancellationToken ressourceEndToken;

		private AudioRessource currentRessource = null;
		private List<AudioRessource> ressourceLog = null;
		private IPlayerConnection playerConnection;

		public event RessourceStartedDelegate RessourceStarted;
		public event RessourceStoppedDelegate RessourceStopped;

		private bool loop = false;
		public bool Loop { get { return loop; } set { playerConnection.SetLoop(value); loop = value; } }

		public bool Seek(int pos)
		{
			if (pos < 0 || pos > playerConnection.GetLength())
				return false;
			playerConnection.SetPosition(pos);
			return true;
		}

		public AudioFramework()
		{
			playerConnection = new VLCConnection();
			playerConnection.Start();
		}

		/// <summary>
		/// Gets started at the beginning of a new ressource.
		/// It calls the stop event when a ressource is finished.
		/// This task can be cancelled by cancelling ressourceEndToken.
		/// </summary>
		private void WaitNotifyEnd()
		{
			// Wait a second until the music hopefully started
			try
			{
				Task.Delay(1000, ressourceEndToken).Wait();
				if (currentRessource != null && playerConnection.IsPlaying() && !ressourceEndToken.IsCancellationRequested)
				{
					Task.Delay(playerConnection.GetLength() - 2, ressourceEndToken).Wait();
					while (currentRessource != null && playerConnection.IsPlaying() && !ressourceEndToken.IsCancellationRequested)
						Task.Delay(1000, ressourceEndToken).Wait();
					if (!ressourceEndToken.IsCancellationRequested)
						Stop();
				}
			}
			catch (TaskCanceledException)
			{
			}
			catch (AggregateException)
			{
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

			if (RessourceStarted != null)
				RessourceStarted(audioRessource);

			// Start task to get the end notified when the ressource ends
			if (ressourceEndTask != null && !ressourceEndTask.IsCompleted)
			{
				ressourceEndTokenSource.Cancel();
				ressourceEndTask.Wait();
			}
			currentRessource = audioRessource;
			ressourceEndTokenSource = new CancellationTokenSource();
			ressourceEndToken = ressourceEndTokenSource.Token;
			ressourceEndTask = Task.Run((Action)WaitNotifyEnd);
			return true;
		}

		public void Stop()
		{
			if (currentRessource != null)
			{
				currentRessource = null;
				playerConnection.AudioStop();
				if (!ressourceEndTask.IsCompleted)
					ressourceEndTokenSource.Cancel();
				if (RessourceStopped != null)
					RessourceStopped();
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
			: base(path) { }

		public override bool Play(IPlayerConnection mediaPlayer)
		{
			mediaPlayer.AudioStart(RessourceName);
			return true;
		}
	}

	enum AudioType
	{
		MediaLink,
		Youtube,
	}
}
