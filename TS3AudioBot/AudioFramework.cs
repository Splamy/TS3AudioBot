// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot
{
	using System;
	using Helper;
	using ResourceFactories;

	public sealed class AudioFramework : IDisposable
	{
		public int MaxUserVolume => audioFrameworkData.MaxUserVolume;
		public const int MaxVolume = 100;

		private AudioFrameworkData audioFrameworkData;

		private IPlayerConnection playerConnection;

		internal event EventHandler<SongEndEventArgs> OnPlaybackStopped;

		// Playerproperties

		/// <summary>Loop state for the current song.</summary>
		public bool Repeat
		{
			get { return playerConnection.Repeated; }
			set { playerConnection.Repeated = value; }
		}
		/// <summary>Gets or sets the volume for the current song.
		/// Value between 0 and MaxVolume. 40 Is usually pretty loud already :).</summary>
		public int Volume
		{
			get { return playerConnection.Volume; }
			set
			{
				if (value < 0 || value > MaxVolume)
					throw new ArgumentOutOfRangeException(nameof(value));
				playerConnection.Volume = value;
			}
		}
		/// <summary>Starts or resumes the current song.</summary>
		public bool Pause
		{
			get { return playerConnection.Paused; }
			set { playerConnection.Paused = value; }
		}
		/// <summary>Length of the current song.</summary>
		public TimeSpan Length => playerConnection.Length;
		/// <summary>Gets or sets the play position of the current song.</summary>
		public TimeSpan Position
		{
			get { return playerConnection.Position; }
			set
			{
				if (value < TimeSpan.Zero || value > Length)
					throw new ArgumentOutOfRangeException(nameof(value));
				playerConnection.Position = value;
			}
		}

		// Audioframework

		/// <summary>Creates a new AudioFramework</summary>
		/// <param name="afd">Required initialization data from a ConfigFile interpreter.</param>
		public AudioFramework(AudioFrameworkData afd, IPlayerConnection audioBackEnd)
		{
			if (audioBackEnd == null)
				throw new ArgumentNullException(nameof(audioBackEnd));

			audioBackEnd.OnSongEnd += (s, e) => OnResourceEnd(true);

			audioFrameworkData = afd;
			playerConnection = audioBackEnd;
			playerConnection.Initialize();
		}

		private void OnResourceEnd(bool restart) => OnPlaybackStopped?.Invoke(this, new SongEndEventArgs(restart));

		/// <summary>
		/// <para>Do NOT call this method directly! Use the <see cref="PlayManager"/> instead.</para>
		/// <para>Stops the old resource and starts the new one.</para>
		/// <para>The volume gets resetted and the OnStartEvent gets triggered.</para>
		/// </summary>
		/// <param name="playData">The info struct containing the PlayResource to start.</param>
		internal R StartResource(PlayResource playResource, MetaData config)
		{
			if (playResource == null)
			{
				Log.Write(Log.Level.Debug, "AF audioResource is null");
				return "No new resource";
			}

			Stop(true);

			if (string.IsNullOrWhiteSpace(playResource.PlayUri))
				return "Internal resource error: link is empty";

			Log.Write(Log.Level.Debug, "AF ar start: {0}", playResource);
			var result = playerConnection.AudioStart(playResource.PlayUri);
			if (!result)
			{
				Log.Write(Log.Level.Error, "Error return from player: {0}", result.Message);
				return $"Internal player error ({result.Message})";
			}

			Volume = config.Volume ?? audioFrameworkData.DefaultVolume;
			Log.Write(Log.Level.Debug, "AF set volume: {0}", Volume);

			return R.OkR;
		}

		public void Stop() => Stop(false);

		/// <summary>Stops the currently played song.</summary>
		private void Stop(bool restart)
		{
			Log.Write(Log.Level.Debug, "AF stop old");

			playerConnection.AudioStop();
			if (!restart)
				OnResourceEnd(false);
		}

		public void Dispose()
		{
			Log.Write(Log.Level.Info, "Closing Mediaplayer...");
			
			if (playerConnection != null)
			{
				Stop(false);
				playerConnection.Dispose();
				playerConnection = null;
			}
		}
	}

	public class SongEndEventArgs : EventArgs
	{
		public bool SongEndedByCallback { get; }
		public SongEndEventArgs(bool songEndedByCallback) { SongEndedByCallback = songEndedByCallback; }
	}

	public class AudioFrameworkData : ConfigData
	{
		[Info("The default volume a song should start with", "10")]
		public int DefaultVolume { get; set; }
		[Info("The maximum volume a normal user can request", "30")]
		public int MaxUserVolume { get; set; }
	}

	public enum AudioType
	{
		MediaLink,
		Youtube,
		Soundcloud,
		Twitch,
	}
}
