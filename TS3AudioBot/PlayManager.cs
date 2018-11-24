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
	using Config;
	using Localization;
	using Playlists;
	using ResourceFactories;
	using System;
	using System.Collections.Generic;

	/// <summary>Provides a convenient inferface for enqueing, playing and registering song events.</summary> 
	public class PlayManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public ConfBot Config { get; set; }
		public IPlayerConnection PlayerConnection { get; set; }
		public PlaylistManager PlaylistManager { get; set; }
		public ResourceFactoryManager ResourceFactoryManager { get; set; }

		public PlayInfoEventArgs CurrentPlayData { get; private set; }
		public bool IsPlaying => CurrentPlayData != null;

		public event EventHandler<PlayInfoEventArgs> BeforeResourceStarted;
		public event EventHandler<PlayInfoEventArgs> AfterResourceStarted;
		public event EventHandler<SongEndEventArgs> BeforeResourceStopped;
		public event EventHandler AfterResourceStopped;

		public E<LocalStr> Enqueue(InvokerData invoker, AudioResource ar) => Enqueue(invoker, new PlaylistItem(ar));
		public E<LocalStr> Enqueue(InvokerData invoker, string message, string audioType = null)
		{
			var result = ResourceFactoryManager.Load(message, audioType);
			if (!result)
				return result.Error;
			return Enqueue(invoker, new PlaylistItem(result.Value.BaseData));
		}
		public E<LocalStr> Enqueue(InvokerData invoker, IEnumerable<PlaylistItem> pli)
		{
			foreach (var item in pli)
				EnqueueInternal(invoker, item);
			return PostEnqueue(invoker);
		}
		public E<LocalStr> Enqueue(InvokerData invoker, PlaylistItem item)
		{
			EnqueueInternal(invoker, item);
			return PostEnqueue(invoker);
		}

		private void EnqueueInternal(InvokerData invoker, PlaylistItem item)
		{
			item.Meta.ResourceOwnerUid = invoker.ClientUid;
			item.Meta.From = PlaySource.FromQueue;
			PlaylistManager.QueueItem(item);
		}

		private E<LocalStr> PostEnqueue(InvokerData invoker)
		{
			if (IsPlaying)
				return R.Ok;
			return Next(invoker);
		}

		public E<LocalStr> Play(InvokerData invoker, PlaylistItem item)
		{
			if (item is null)
				throw new ArgumentNullException(nameof(item));

			return Play(invoker, item.Resource, item.Meta);
		}
		/// <summary>Tries to play the passed <see cref="AudioResource"/></summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="ar">The resource to load and play.</param>
		/// <param name="meta">Allows overriding certain settings for the resource. Can be null.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public E<LocalStr> Play(InvokerData invoker, AudioResource ar, MetaData meta = null)
		{
			if (ar is null)
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
		public E<LocalStr> Play(InvokerData invoker, string link, string audioType = null, MetaData meta = null)
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
		public E<LocalStr> Play(InvokerData invoker, PlayResource play, MetaData meta)
		{
			if (meta.From != PlaySource.FromPlaylist)
				meta.ResourceOwnerUid = invoker.ClientUid;

			var sourceLink = ResourceFactoryManager.RestoreLink(play.BaseData);
			var playInfo = new PlayInfoEventArgs(invoker, play, meta, sourceLink);
			BeforeResourceStarted?.Invoke(this, playInfo);

			var result = StartResource(play, meta);
			if (!result) return result;

			CurrentPlayData = playInfo; // TODO meta as readonly
			AfterResourceStarted?.Invoke(this, playInfo);

			return R.Ok;
		}

		private E<LocalStr> StartResource(PlayResource playResource, MetaData meta)
		{
			if (string.IsNullOrWhiteSpace(playResource.PlayUri))
			{
				Log.Error("Internal resource error: link is empty (resource:{0})", playResource);
				return new LocalStr(strings.error_playmgr_internal_error);
			}

			Log.Debug("AudioResource start: {0}", playResource);
			var result = PlayerConnection.AudioStart(playResource.PlayUri);
			if (!result)
			{
				Log.Error("Error return from player: {0}", result.Error);
				return new LocalStr(strings.error_playmgr_internal_error);
			}

			PlayerConnection.Volume = meta.Volume
				?? Math.Min(Math.Max(PlayerConnection.Volume, Config.Audio.Volume.Min), Config.Audio.Volume.Max);

			return R.Ok;
		}

		public E<LocalStr> Next(InvokerData invoker, bool manually = true)
		{
			PlaylistItem pli = null;
			for (int i = 0; i < 10; i++)
			{
				if ((pli = PlaylistManager.Next(manually)) is null) break;
				var result = Play(invoker, pli);
				if (result.Ok)
					return result;
				Log.Warn("Skipping: {0} because {1}", pli.DisplayString, result.Error.Str);
			}
			if (pli is null)
				return new LocalStr(strings.info_playmgr_no_next_song);
			else
				return new LocalStr(string.Format(strings.error_playmgr_many_songs_failed, "!next"));
		}

		public E<LocalStr> Previous(InvokerData invoker, bool manually = true)
		{
			bool skipPrev = CurrentPlayData?.MetaData.From != PlaySource.FromPlaylist;
			PlaylistItem pli = null;
			for (int i = 0; i < 10; i++)
			{
				if (skipPrev)
				{
					pli = PlaylistManager.Current;
					skipPrev = false;
				}
				else
				{
					pli = PlaylistManager.Previous(manually);
				}
				if (pli is null) break;

				var result = Play(invoker, pli);
				if (result.Ok)
					return result;
				Log.Warn("Skipping: {0} because {1}", pli.DisplayString, result.Error.Str);
			}
			if (pli is null)
				return new LocalStr(strings.info_playmgr_no_previous_song);
			else
				return new LocalStr(string.Format(strings.error_playmgr_many_songs_failed, "!previous"));
		}

		public void SongStoppedHook(object sender, EventArgs e) => StopInternal(true);

		public void Stop() => StopInternal(false);

		private void StopInternal(bool songEndedByCallback)
		{
			BeforeResourceStopped?.Invoke(this, new SongEndEventArgs(songEndedByCallback));

			if (songEndedByCallback)
			{
				var result = Next(CurrentPlayData?.Invoker ?? InvokerData.Anonymous, false);
				if (result.Ok)
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
		/// <summary>Defaults to: invoker.Uid - Can be set if the owner of a song differs from the invoker.</summary>
		public string ResourceOwnerUid { get; set; }
		/// <summary>Defaults to: AudioFramwork.Defaultvolume - Overrides the starting volume.</summary>
		public float? Volume { get; set; } = null;
		/// <summary>Default: false - Indicates whether the song has been requested from a playlist.</summary>
		public PlaySource From { get; set; } = PlaySource.PlayRequest;

		public MetaData Clone() => new MetaData
		{
			ResourceOwnerUid = ResourceOwnerUid,
			From = From,
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
		public string SourceLink { get; }

		public PlayInfoEventArgs(InvokerData invoker, PlayResource playResource, MetaData meta, string sourceLink)
		{
			Invoker = invoker;
			PlayResource = playResource;
			MetaData = meta;
			SourceLink = sourceLink;
		}
	}

	public sealed class InvokerData
	{
		public string ClientUid { get; }
		public ulong? DatabaseId { get; }
		public ulong? ChannelId { get; }
		public ushort? ClientId { get; }
		public string NickName { get; }
		public string Token { get; }
		public TS3Client.TextMessageTargetMode? Visibiliy { get; internal set; }
		// Lazy
		public ulong[] ServerGroups { get; internal set; }
		public bool IsAnonymous => ClientUid == AnonymousUid;

		private const string AnonymousUid = "Anonymous";
		public static readonly InvokerData Anonymous = new InvokerData(AnonymousUid);

		public InvokerData(string clientUid, ulong? databaseId = null, ulong? channelId = null,
			ushort? clientId = null, string nickName = null, string token = null,
			TS3Client.TextMessageTargetMode? visibiliy = null)
		{
			ClientUid = clientUid ?? throw new ArgumentNullException(nameof(ClientUid));
			DatabaseId = databaseId;
			ChannelId = channelId;
			ClientId = clientId;
			NickName = nickName;
			Token = token;
			Visibiliy = visibiliy;
		}

		public override int GetHashCode() => ClientUid.GetHashCode();

		public override bool Equals(object obj)
		{
			if (!(obj is InvokerData other))
				return false;

			return ClientUid == other.ClientUid;
		}
	}

	public static class AudioValues
	{
		public const float MaxVolume = 100;
	}

	public enum PlaySource
	{
		PlayRequest,
		FromQueue,
		FromPlaylist,
	}
}
