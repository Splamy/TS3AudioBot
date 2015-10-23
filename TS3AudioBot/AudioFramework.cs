using System;
using System.Threading.Tasks;
using System.Threading;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot
{
	class AudioFramework : IDisposable
	{
		public const int MAXUSERVOLUME = 200;
		public const int MAXVOLUME = 512;
		private const int TIMEOUT_MS = 30000;
		private const int TIMEOUT_INTERVAL_MS = 100;

		private AudioFrameworkData audioFrameworkData;
		private Task ressourceEndTask;
		/// <summary>
		/// This token is used to cancel a WaitNotifyEnd task, don't change it while the task is running!
		/// </summary>
		private CancellationTokenSource ressourceEndTokenSource;
		private CancellationToken ressourceEndToken;

		public AudioRessource currentRessource { get; protected set; }
		private IPlayerConnection playerConnection;

		public delegate void RessourceStartedDelegate(AudioRessource audioRessource);
		public delegate void RessourceStoppedDelegate(bool restart);
		public event RessourceStartedDelegate OnRessourceStarted;
		public event RessourceStoppedDelegate OnRessourceStopped;

		// Playerproperties

		private bool loop = false;
		/// <summary>Loop state for the entire playlist.</summary>
		public bool Loop
		{
			get { return loop; }
			set { playerConnection.SetLoop(value); loop = value; }
		}

		private bool repeat = false;
		/// <summary>Loop state for the current song.</summary>
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

		/// <summary>Jumps to the position in the audiostream if available.</summary>
		/// <param name="pos">Position in seconds from the start.</param>
		/// <returns>True if the seek request was valid, false otherwise.</returns>
		public bool Seek(int pos)
		{
			if (pos < 0 || pos > playerConnection.GetLength())
				return false;
			playerConnection.SetPosition(pos);
			return true;
		}

		/// <summary>Plays the next song in the playlist.</summary>
		public void Next()
		{
			playerConnection.AudioNext();
		}

		/// <summary>Plays the previous song in the playlist.</summary>
		public void Previous()
		{
			playerConnection.AudioPrevious();
		}

		/// <summary>Clears the current playlist</summary>
		public void Clear()
		{
			playerConnection.AudioClear();
		}

		/// <summary>Starts or resumes the current song.</summary>
		public void Play()
		{
			playerConnection.AudioPlay();
		}

		// Audioframework

		/// <summary>Creates a new AudioFramework</summary>
		/// <param name="afd">Required initialization data from a ConfigFile interpreter.</param>
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
		/// <para>Gets started at the beginning of a new ressource.</para>
		/// <para>It calls the stop event when a ressource is finished.</para>
		/// <para>This task can be cancelled by cancelling ressourceEndToken.</para>
		/// </summary>
		private async void WaitNotifyEnd()
		{
			try
			{
				Log.Write(Log.Level.Debug, "AF Wait for start");
				const int timeoutmax = TIMEOUT_MS / TIMEOUT_INTERVAL_MS;
				int timeoutcur = timeoutmax;

				while (timeoutcur-- > 0 && currentRessource != null && !ressourceEndToken.IsCancellationRequested)
				{
					if (playerConnection.IsPlaying())
					{
						timeoutcur = timeoutmax;
						await Task.Delay(TIMEOUT_MS, ressourceEndToken);
					}
					else
					{
						await Task.Delay(TIMEOUT_INTERVAL_MS, ressourceEndToken);
					}
				}
				Log.Write(Log.Level.Debug, "AF Timeout or stopped (IsPlaying:{0})", timeoutcur);
				if (!ressourceEndToken.IsCancellationRequested)
					Stop(false);
			}
			catch (TaskCanceledException) { }
			catch (AggregateException) { }
		}

		/// <summary>
		/// <para>Stops the old ressource and starts the new one.</para>
		/// <para>The volume gets resetted and the OnStartEvent gets triggered.</para>
		/// </summary>
		/// <param name="audioRessource">The audio ressource to start.</param>
		/// <returns>True if the audio ressource started successfully, false otherwise.</returns>
		public AudioResultCode StartRessource(AudioRessource audioRessource, GetClientsInfo invoker)
		{
			if (audioRessource == null)
			{
				Log.Write(Log.Level.Debug, "AF audioRessource is null");
				return AudioResultCode.NoNewRessource;
			}

			audioRessource.InvokingUser = invoker;

			Stop(true);

			playerConnection.AudioClear();

			if (audioRessource.Volume == -1)
				audioRessource.Volume = audioFrameworkData.defaultVolume;
			if (audioRessource.Enqueue)
			{
				if (!audioRessource.Play(playerConnection.AudioAdd))
					return AudioResultCode.RessouceInternalError;
				audioRessource.Enqueue = false;
			}
			else
			{
				Log.Write(Log.Level.Debug, "AF ar start: {0}", audioRessource.RessourceURL);
				if (!audioRessource.Play(playerConnection.AudioStart))
					return AudioResultCode.RessouceInternalError;

				Volume = audioRessource.Volume;
				Log.Write(Log.Level.Debug, "AF set volume: {0}", Volume);
			}

			if (OnRessourceStarted != null)
				OnRessourceStarted(audioRessource);

			currentRessource = audioRessource;
			if (ressourceEndTask == null || ressourceEndTask.IsCompleted || ressourceEndTask.IsCanceled || ressourceEndTask.IsFaulted)
			{
				if (ressourceEndTask != null)
					ressourceEndTask.Dispose();
				if (ressourceEndTokenSource != null)
					ressourceEndTokenSource.Dispose();
				ressourceEndTokenSource = new CancellationTokenSource();
				ressourceEndToken = ressourceEndTokenSource.Token;
				ressourceEndTask = Task.Run((Action)WaitNotifyEnd);
			}
			return AudioResultCode.Success;
		}

		public void Stop()
		{
			Stop(false);
		}

		/// <summary>Stops the currently played song.</summary>
		/// <param name="restart">When set to true, the AudioBob won't be notified aubout the stop.
		/// Use this parameter to prevent fast off-on switching.</param>
		private void Stop(bool restart)
		{
			Log.Write(Log.Level.Debug, "AF stop old (restart:{0})", restart);
			if (currentRessource != null)
			{
				currentRessource = null;
				playerConnection.AudioStop();
				if (!ressourceEndTask.IsCompleted)
					ressourceEndTokenSource.Cancel();
				if (OnRessourceStopped != null)
					OnRessourceStopped(restart);
			}
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
		public abstract AudioType AudioType { get; }
		public string RessourceTitle { get; private set; }
		public string RessourceURL { get; private set; }

		public int Volume { get; set; }
		public bool Enqueue { get; set; }
		public GetClientsInfo InvokingUser { get; set; }

		protected AudioRessource(string ressourceURL, string ressourceTitle)
		{
			RessourceURL = ressourceURL;
			RessourceTitle = ressourceTitle;

			Volume = -1;
			Enqueue = false;
			InvokingUser = null;
		}

		public abstract bool Play(Action<string> setMedia);

		public override string ToString()
		{
			return string.Format("{0} (@{1}) - {2}", RessourceTitle, Volume, AudioType);
		}
	}

	class MediaRessource : AudioRessource
	{
		public override AudioType AudioType { get { return AudioType.MediaLink; } }

		public MediaRessource(string path, string name)
			: base(path, name)
		{ }

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

	enum AudioResultCode
	{
		Success,
		NoNewRessource,
		RessouceInternalError,
	}
}
