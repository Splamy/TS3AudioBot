namespace TS3AudioBot
{
	using System;
	using TS3AudioBot.Helper;

	public class AudioFramework : IDisposable
	{
		public int MaxUserVolume => audioFrameworkData.maxUserVolume;
		public const int MAXVOLUME = 100;
		private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(30);
		private static readonly TimeSpan TIMEOUT_INTERVAL = TimeSpan.FromSeconds(1);

		private AudioFrameworkData audioFrameworkData;
		private TickWorker waitEndTick;
		private DateTime endTime;

		public PlayData CurrentPlayData { get; protected set; }
		private IPlayerConnection playerConnection;

		public delegate void ResourceStartedDelegate(PlayData audioResource);
		public delegate void ResourceStoppedDelegate(bool restart);
		public event ResourceStartedDelegate OnResourceStarted;
		public event ResourceStoppedDelegate OnResourceStopped;

		// Playerproperties

		/// <summary>Loop state for the entire playlist.</summary>
		public bool Loop // TODO
		{
			get { return false; }
			set { throw new NotImplementedException(); }
		}

		/// <summary>Loop state for the current song.</summary>
		public bool Repeat { get { return playerConnection.Repeated; } set { playerConnection.Repeated = value; } }
		public int Volume { get { return playerConnection.Volume; } set { playerConnection.Volume = value; } }
		/// <summary>Starts or resumes the current song.</summary>
		public bool Pause { get { return playerConnection.Pause; } set { playerConnection.Pause = value; } }

		// Playermethods

		/// <summary>Jumps to the position in the audiostream if available.</summary>
		/// <param name="pos">Position in seconds from the start.</param>
		/// <returns>True if the seek request was valid, false otherwise.</returns>
		public bool Seek(int pos)
		{
			if (pos < 0 || pos > playerConnection.Length)
				return false;
			playerConnection.Position = pos;
			return true;
		}

		/// <summary>Plays the next song in the playlist.</summary>
		public void Next()
		{

		}

		/// <summary>Plays the previous song in the playlist.</summary>
		public void Previous()
		{

		}

		/// <summary>Clears the current playlist</summary>
		public void Clear()
		{

		}

		public void SongEnd()
		{
			// if playlistmanager is off:
			Stop(false);
			// else:
			// Stop(true);
			// next song
		}

		// Audioframework

		/// <summary>Creates a new AudioFramework</summary>
		/// <param name="afd">Required initialization data from a ConfigFile interpreter.</param>
		public AudioFramework(AudioFrameworkData afd, IPlayerConnection audioBackend)
		{
			if (audioBackend.SupportsEndCallback)
				audioBackend.OnSongEnd += (s, e) => SongEnd();
			else
				waitEndTick = TickPool.RegisterTick(NotifyEnd, TIMEOUT_INTERVAL, false);
			audioFrameworkData = afd;
			playerConnection = audioBackend;
			playerConnection.Initialize();
		}

		/// <summary>
		/// <para>Gets started at the beginning of a new resource.</para>
		/// <para>It calls the stop event when a resource is finished.</para>
		/// <para>Is used for player backends which are not supporting an end callback.</para>
		/// </summary>
		private void NotifyEnd()
		{
			if (endTime < DateTime.Now)
			{
				if (playerConnection.IsPlaying)
				{
					int playtime = playerConnection.Length;
					int position = playerConnection.Position;

					int endspan = playtime - position;
					endTime = DateTime.Now.AddSeconds(endspan);
				}
				else if (endTime + TIMEOUT < DateTime.Now)
				{
					Log.Write(Log.Level.Debug, "AF Song ended with default timeout");
					SongEnd();
					waitEndTick.Active = false;
				}
			}
		}

		/// <summary>
		/// <para>Do NOT call this method directly! Use the FactoryManager instead.</para>
		/// <para>Stops the old resource and starts the new one.</para>
		/// <para>The volume gets resetted and the OnStartEvent gets triggered.</para>
		/// </summary>
		/// <param name="playData">The info struct contaiting the AudioResource to start.</param>
		/// <returns>An infocode on what happened.</returns>
		internal AudioResultCode StartResource(PlayData playData)
		{
			if (playData == null || playData.Resource == null)
			{
				Log.Write(Log.Level.Debug, "AF audioResource is null");
				return AudioResultCode.NoNewResource;
			}

			Stop(true);

			string resourceLink = playData.Resource.Play();
			if (string.IsNullOrWhiteSpace(resourceLink))
				return AudioResultCode.ResouceInternalError;

			Log.Write(Log.Level.Debug, "AF ar start: {0}", playData.Resource);
			playerConnection.AudioStart(resourceLink);

			if (playData.Volume == -1)
				Volume = audioFrameworkData.defaultVolume;
			else
				Volume = playData.Volume;
			Log.Write(Log.Level.Debug, "AF set volume: {0}", Volume);

			if (OnResourceStarted != null)
				OnResourceStarted(playData);

			CurrentPlayData = playData;
			endTime = DateTime.Now;
			if (!playerConnection.SupportsEndCallback)
				waitEndTick.Active = true;
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
			if (CurrentPlayData != null)
			{
				CurrentPlayData = null;
				playerConnection.AudioStop();
				if (OnResourceStopped != null)
					OnResourceStopped(restart);
			}
		}

		public void Dispose()
		{
			Log.Write(Log.Level.Info, "Closing Mediaplayer...");

			Stop(false);

			if (playerConnection != null)
			{
				playerConnection.Dispose();
				playerConnection = null;
				Log.Write(Log.Level.Debug, "AF playerConnection disposed");
			}
		}
	}

	public struct AudioFrameworkData
	{
		//[InfoAttribute("the absolute or relative path to the local music folder")]
		//public string localAudioPath;
		[Info("the default volume a song should start with")]
		public int defaultVolume;
		[Info("the maximum volume a normal user can request")]
		public int maxUserVolume;
		[Info("the location of the vlc player (if the vlc backend is used)", "vlc")]
		public string vlcLocation;
	}

	public enum AudioType
	{
		MediaLink,
		Youtube,
		Soundcloud,
		Twitch,
	}

	enum AudioResultCode
	{
		Success,
		NoNewResource,
		ResouceInternalError,
	}
}
