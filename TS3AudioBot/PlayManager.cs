// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	using Helper;
	using ResourceFactories;
	using System;
	using System.Collections.Generic;

	/// <summary>Provides a convenient inferface for enqueing, playing and registering song events.</summary> 
	public class PlayManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public IPlayerConnection PlayerConnection { get; set; }
		public PlaylistManager PlaylistManager { get; set; }
		public ResourceFactoryManager ResourceFactoryManager { get; set; }

		public PlayInfoEventArgs CurrentPlayData { get; private set; }
		public bool IsPlaying => CurrentPlayData != null;

		public event EventHandler<PlayInfoEventArgs> BeforeResourceStarted;
		public event EventHandler<PlayInfoEventArgs> AfterResourceStarted;
		public event EventHandler<SongEndEventArgs> BeforeResourceStopped;
		public event EventHandler AfterResourceStopped;

		public R Enqueue(InvokerData invoker, AudioResource ar) => EnqueueInternal(invoker, new PlaylistItem(ar));
		public R Enqueue(InvokerData invoker, string message, string audioType = null)
		{
			var result = ResourceFactoryManager.Load(message, audioType);
			if (!result)
				return result.Error;
			return EnqueueInternal(invoker, new PlaylistItem(result.Value.BaseData));
		}
		public R Enqueue(IEnumerable<PlaylistItem> pli)
		{
			PlaylistManager.AddToFreelist(pli);
			return R.OkR;
		}

		private R EnqueueInternal(InvokerData invoker, PlaylistItem pli)
		{
			pli.Meta.ResourceOwnerDbId = invoker.DatabaseId;
			PlaylistManager.AddToFreelist(pli);

			return R.OkR;
		}

		public R Play(InvokerData invoker, PlaylistItem item)
		{
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			return Play(invoker, item.Resource, item.Meta);
		}
		/// <summary>Tries to play the passed <see cref="AudioResource"/></summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="ar">The resource to load and play.</param>
		/// <param name="meta">Allows overriding certain settings for the resource. Can be null.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R Play(InvokerData invoker, AudioResource ar, MetaData meta = null)
		{
			if (ar == null)
				throw new ArgumentNullException(nameof(ar));

			var result = ResourceFactoryManager.Load(ar);
			if (!result)
				return result.Error;
			return Play(invoker, result.Value, meta ?? new MetaData());
		}
		/// <summary>Tries to play the passed link.</summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="link">The link to resolve, load and play.</param>
		/// <param name="audioType">The associated resource type string to a factory.</param>
		/// <param name="meta">Allows overriding certain settings for the resource. Can be null.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R Play(InvokerData invoker, string link, string audioType = null, MetaData meta = null)
		{
			var result = ResourceFactoryManager.Load(link, audioType);
			if (!result)
				return result.Error;
			return Play(invoker, result.Value, meta ?? new MetaData());
		}
		/// <summary>Plays the passed <see cref="PlayResource"/></summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="play">The associated resource type string to a factory.</param>
		/// <param name="meta">Allows overriding certain settings for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R Play(InvokerData invoker, PlayResource play, MetaData meta)
		{
			if (!meta.FromPlaylist)
				meta.ResourceOwnerDbId = invoker.DatabaseId;

			var playInfo = new PlayInfoEventArgs(invoker, play, meta);
			BeforeResourceStarted?.Invoke(this, playInfo);

			// pass the song to the AF to start it
			var result = StartResource(play, meta);
			if (!result) return result;

			// add it to our freelist for comfort
			if (!meta.FromPlaylist)
			{
				int index = PlaylistManager.InsertToFreelist(new PlaylistItem(play.BaseData, meta));
				PlaylistManager.Index = index;
			}

			CurrentPlayData = playInfo; // TODO meta as readonly
			AfterResourceStarted?.Invoke(this, CurrentPlayData);

			return R.OkR;
		}

		private R StartResource(PlayResource playResource, MetaData config)
		{
			//PlayerConnection.AudioStop();

			if (string.IsNullOrWhiteSpace(playResource.PlayUri))
				return "Internal resource error: link is empty";

			Log.Debug("AudioResource start: {0}", playResource);
			var result = PlayerConnection.AudioStart(playResource.PlayUri);
			if (!result)
			{
				Log.Error("Error return from player: {0}", result.Error);
				return $"Internal player error ({result.Error})";
			}

			PlayerConnection.Volume = config.Volume ?? AudioValues.DefaultVolume;

			return R.OkR;
		}

		public R Next(InvokerData invoker)
		{
			PlaylistItem pli = null;
			for (int i = 0; i < 10; i++)
			{
				if ((pli = PlaylistManager.Next()) == null) break;
				var result = Play(invoker, pli);
				if (result.Ok)
					return result;
				Log.Warn("Skipping: {0} because {1}", pli.DisplayString, result.Error);
			}
			if (pli == null)
				return "No next song could be played";
			else
				return "A few songs failed to start, use !next to continue";
		}
		public R Previous(InvokerData invoker)
		{
			PlaylistItem pli = null;
			for (int i = 0; i < 10; i++)
			{
				if ((pli = PlaylistManager.Previous()) == null) break;
				if (Play(invoker, pli))
					return R.OkR;
				// optional message here that playlist entry has been skipped
			}
			if (pli == null)
				return "No previous song could be played";
			else
				return "A few songs failed to start, use !previous to continue";
		}

		public void SongStoppedHook(object sender, EventArgs e) => StopInternal(true);

		public void Stop() => StopInternal(false);

		private void StopInternal(bool songEndedByCallback)
		{
			BeforeResourceStopped?.Invoke(this, new SongEndEventArgs(songEndedByCallback));

			if (songEndedByCallback && CurrentPlayData != null)
			{
				var result = Next(CurrentPlayData.Invoker);
				if (result)
					return;
				Log.Info("Song queue ended: {0}", result.Error);
			}
			else
			{
				PlayerConnection.AudioStop();
			}

			CurrentPlayData = null;
			AfterResourceStopped?.Invoke(this, EventArgs.Empty);
		}
	}

	public sealed class MetaData
	{
		/// <summary>Defaults to: invoker.DbId - Can be set if the owner of a song differs from the invoker.</summary>
		public ulong? ResourceOwnerDbId { get; set; }
		/// <summary>Defaults to: AudioFramwork.Defaultvolume - Overrides the starting volume.</summary>
		public float? Volume { get; set; } = null;
		/// <summary>Default: false - Indicates whether the song has been requested from a playlist.</summary>
		public bool FromPlaylist { get; set; }

		public MetaData Clone() => new MetaData
		{
			ResourceOwnerDbId = ResourceOwnerDbId,
			FromPlaylist = FromPlaylist,
			Volume = Volume
		};
	}

	public class SongEndEventArgs : EventArgs
	{
		public bool SongEndedByCallback { get; }
		public SongEndEventArgs(bool songEndedByCallback) { SongEndedByCallback = songEndedByCallback; }
	}

	public sealed class PlayInfoEventArgs : EventArgs
	{
		public InvokerData Invoker { get; }
		public PlayResource PlayResource { get; }
		public AudioResource ResourceData => PlayResource.BaseData;
		public MetaData MetaData { get; }
		public ulong? Owner => MetaData.ResourceOwnerDbId ?? Invoker.DatabaseId;

		public PlayInfoEventArgs(InvokerData invoker, PlayResource playResource, MetaData meta)
		{
			Invoker = invoker;
			PlayResource = playResource;
			MetaData = meta;
		}
	}

	public sealed class InvokerData
	{
		public string ClientUid { get; }
		public ulong? DatabaseId { get; internal set; }
		public ulong? ChannelId { get; internal set; }
		public ushort? ClientId { get; internal set; }
		public string NickName { get; internal set; }
		public string Token { get; internal set; }
		public TS3Client.TextMessageTargetMode? Visibiliy { get; internal set; }

		public InvokerData(string clientUid, ulong? databaseId = null, ulong? channelId = null,
			ushort? clientId = null, string nickName = null, string token = null,
			TS3Client.TextMessageTargetMode? visibiliy = null)
		{
			ClientUid = clientUid;
			DatabaseId = databaseId;
			ChannelId = channelId;
			ClientId = clientId;
			NickName = nickName;
			Token = token;
			Visibiliy = visibiliy;
		}

		public override int GetHashCode()
		{
			return ClientUid?.GetHashCode() ?? 0;
		}

		public override bool Equals(object obj)
		{
			if (ClientUid == null)
				return false;
			if (!(obj is InvokerData other) || other.ClientUid == null)
				return false;

			return ClientUid == other.ClientUid;
		}
	}

	public static class AudioValues
	{
		public const float MaxVolume = 100;

		internal static AudioFrameworkData audioFrameworkData;

		public static float MaxUserVolume => audioFrameworkData.MaxUserVolume;
		public static float DefaultVolume => audioFrameworkData.DefaultVolume;
	}

	public class AudioFrameworkData : ConfigData
	{
		[Info("The default volume a song should start with", "10")]
		public float DefaultVolume { get; set; }
		[Info("The maximum volume a normal user can request", "30")]
		public float MaxUserVolume { get; set; }
		[Info("How the bot should play music. Options are: whisper, voice, (!...)", "whisper")]
		public string AudioMode { get; set; }
	}
}
