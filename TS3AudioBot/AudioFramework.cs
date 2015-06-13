using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace TS3AudioBot
{
	class AudioFramework : IDisposable
	{
		public const int MAXVOLUME = 200;
		private const int TIMEOUT_MS = 30000;
		private const int TIMEOUT_INTERVAL_MS = 100;

		private AudioFrameworkData audioFrameworkData;
		private Task ressourceEndTask;
		/// <summary>
		/// This token is used to cancel a WaitNotifyEnd task, don't change it while the task is running!
		/// </summary>
		private CancellationTokenSource ressourceEndTokenSource;
		private CancellationToken ressourceEndToken;

		private AudioRessource currentRessource = null;
		private List<AudioRessource> ressourceLog = null;
		private IPlayerConnection playerConnection;

		public delegate void RessourceStartedDelegate(AudioRessource audioRessource);
		public delegate void RessourceStoppedDelegate();
		public event RessourceStartedDelegate OnRessourceStarted;
		public event RessourceStoppedDelegate OnRessourceStopped;

		// Playerproperties

		private bool loop = false;
		public bool Loop
		{
			get { return loop; }
			set { playerConnection.SetLoop(value); loop = value; }
		}

		private bool repeat = false;
		public bool Repeat
		{
			get { return repeat; }
			set { playerConnection.SetRepeat(value); repeat = value; }
		}

		private int volume = -1;
		public int Volume
		{
			get { return volume; }
			set { if (value != volume) { playerConnection.SetVolume(value); volume = value; } }
		}

		// Playermethods

		public bool Seek(int pos)
		{
			if (pos < 0 || pos > playerConnection.GetLength())
				return false;
			playerConnection.SetPosition(pos);
			return true;
		}

		public void Next()
		{
			playerConnection.AudioNext();
		}

		public void Previous()
		{
			playerConnection.AudioPrevious();
		}

		public void Clear()
		{
			playerConnection.AudioClear();
		}

		public void Play()
		{
			playerConnection.AudioPlay();
		}

		// Audioframework

		public AudioFramework(AudioFrameworkData afd)
		{
			audioFrameworkData = afd;
			playerConnection = new VLCConnection();
			playerConnection.Start();
		}

		private void GetHistory(int index, int backcount)
		{
			// TODO
		}

		/// <summary>
		/// Gets started at the beginning of a new ressource.
		/// It calls the stop event when a ressource is finished.
		/// This task can be cancelled by cancelling ressourceEndToken.
		/// </summary>
		private void WaitNotifyEnd()
		{
			try
			{
				Log.Write(Log.Level.Debug, "AF Wait for start");
				int timeoutmax = TIMEOUT_MS / TIMEOUT_INTERVAL_MS;
				int timeoutcur = timeoutmax;

				while (timeoutcur-- > 0 && currentRessource != null && !ressourceEndToken.IsCancellationRequested)
				{
					if (playerConnection.IsPlaying())
					{
						timeoutcur = timeoutmax;
						Task.Delay(TIMEOUT_MS, ressourceEndToken).Wait();
					}
					else
					{
						Task.Delay(TIMEOUT_INTERVAL_MS, ressourceEndToken).Wait();
						Log.Write(Log.Level.Debug, "AF Timeout running: {0}/{1}", timeoutcur, timeoutmax);
					}
				}
				Log.Write(Log.Level.Debug, "AF Timeout or stopped (IsPlaying:{0})", timeoutcur);
				if (!ressourceEndToken.IsCancellationRequested)
					Stop();
			}
			catch (TaskCanceledException) { }
			catch (AggregateException) { }
		}

		public bool StartRessource(AudioRessource audioRessource)
		{
			if (audioRessource == null)
			{
				Log.Write(Log.Level.Debug, "AF audioRessource is null");
				return false;
			}

			LogAudioRessource(audioRessource);

			Log.Write(Log.Level.Debug, "AF stop old");
			Stop();

			playerConnection.AudioClear();

			if (audioRessource.Volume == -1)
				audioRessource.Volume = audioFrameworkData.defaultVolume;
			Volume = audioRessource.Volume;
			Log.Write(Log.Level.Debug, "AF set volume: {0}", Volume);
			if (audioRessource.Enqueue)
			{
				if (!audioRessource.Play(ar => playerConnection.AudioAdd(ar)))
					return false;
				audioRessource.Enqueue = false;
			}
			else
			{
				Log.Write(Log.Level.Debug, "AF ar start: {0}", audioRessource.RessourceURL);
				if (!audioRessource.Play(ar => playerConnection.AudioStart(ar)))
					return false;
			}

			if (OnRessourceStarted != null)
				OnRessourceStarted(audioRessource);

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
				if (OnRessourceStopped != null)
					OnRessourceStopped();
			}
		}

		private void LogAudioRessource(AudioRessource ar)
		{
			if (ressourceLog == null)
				ressourceLog = new List<AudioRessource>();
			ressourceLog.Add(ar);
		}

		public void Dispose()
		{
			Log.Write(Log.Level.Info, "Closing Mediaplayer...");
			if (playerConnection != null)
			{
				playerConnection.Dispose();
				playerConnection = null;
				Log.Write(Log.Level.Debug, "AF playerConnection disposed");
			}
			if (ressourceEndTokenSource != null)
			{
				ressourceEndTokenSource.Dispose();
				ressourceEndTokenSource = null;
				Log.Write(Log.Level.Debug, "AF rETS disposed");
			}
		}
	}

	abstract class AudioRessource
	{
		public int Volume { get; set; }
		public abstract AudioType AudioType { get; }
		public string RessourceTitle { get; private set; }
		public string RessourceURL { get; private set; }
		public bool Enqueue { get; set; }

		public abstract bool Play(Action<string> setMedia);

		public AudioRessource(string ressourceURL, string ressourceTitle)
		{
			Volume = -1;
			RessourceURL = ressourceURL;
			RessourceTitle = ressourceTitle;
		}

		public override string ToString()
		{
			return string.Format("{0} (@{1}) - {2}", RessourceTitle, Volume, AudioType);
		}
	}

	class MediaRessource : AudioRessource
	{
		public override AudioType AudioType { get { return AudioType.MediaLink; } }

		public MediaRessource(string path, string name)
			: base(path, name) { }

		public override bool Play(Action<string> setMedia)
		{
			setMedia(RessourceURL);
			return true;
		}
	}

	public struct AudioFrameworkData
	{
		//[InfoAttribute("the absolute or relative path to the local music folder")]
		//public string localAudioPath;
		[InfoAttribute("the default volume a song should start with")]
		public int defaultVolume;
	}

	enum AudioType
	{
		MediaLink,
		Youtube,
	}
}
