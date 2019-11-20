// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories;
using TSLib.Helper;

namespace TS3AudioBot.Audio
{
	/// <summary>Provides a convenient inferface for enqueing, playing and registering song events.</summary>
	public class PlayManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly ConfBot confBot;
		private readonly Player playerConnection;
		private readonly PlaylistManager playlistManager;
		private readonly ResourceResolver resourceResolver;

		public PlayInfoEventArgs CurrentPlayData { get; private set; }
		public bool IsPlaying => CurrentPlayData != null;

		public event EventHandler<PlayInfoEventArgs> OnResourceUpdated;
		public event EventHandler<PlayInfoEventArgs> BeforeResourceStarted;
		public event EventHandler<PlayInfoEventArgs> AfterResourceStarted;
		public event EventHandler<SongEndEventArgs> BeforeResourceStopped;
		public event EventHandler AfterResourceStopped;

		public PlayManager(ConfBot config, Player playerConnection, PlaylistManager playlistManager, ResourceResolver resourceResolver)
		{
			confBot = config;
			this.playerConnection = playerConnection;
			this.playlistManager = playlistManager;
			this.resourceResolver = resourceResolver;
		}

		public E<LocalStr> Enqueue(InvokerData invoker, AudioResource ar, MetaData meta = null) => Enqueue(invoker, new PlaylistItem(ar, meta));
		public E<LocalStr> Enqueue(InvokerData invoker, string message, string audioType = null, MetaData meta = null)
		{
			var result = resourceResolver.Load(message, audioType);
			if (!result)
				return result.Error;
			return Enqueue(invoker, new PlaylistItem(result.Value.BaseData, meta));
		}
		public E<LocalStr> Enqueue(InvokerData invoker, IEnumerable<PlaylistItem> items)
		{
			var startOff = playlistManager.CurrentList.Items.Count;
			playlistManager.Queue(items.Select(x => UpdateItem(invoker, x)));
			return PostEnqueue(invoker, startOff);
		}
		public E<LocalStr> Enqueue(InvokerData invoker, PlaylistItem item)
		{
			var startOff = playlistManager.CurrentList.Items.Count;
			playlistManager.Queue(UpdateItem(invoker, item));
			return PostEnqueue(invoker, startOff);
		}

		private static PlaylistItem UpdateItem(InvokerData invoker, PlaylistItem item)
		{
			item.Meta = item.Meta ?? new MetaData();
			item.Meta.ResourceOwnerUid = invoker.ClientUid;
			return item;
		}

		private E<LocalStr> PostEnqueue(InvokerData invoker, int startIndex)
		{
			if (IsPlaying)
				return R.Ok;
			playlistManager.Index = startIndex;
			return StartCurrent(invoker);
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

			var result = resourceResolver.Load(ar);
			if (!result)
				return result.Error;
			return Play(invoker, result.Value, meta);
		}

		/// <summary>Tries to play the passed link.</summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="link">The link to resolve, load and play.</param>
		/// <param name="audioType">The associated resource type string to a factory.</param>
		/// <param name="meta">Allows overriding certain settings for the resource. Can be null.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public E<LocalStr> Play(InvokerData invoker, string link, string audioType = null, MetaData meta = null)
		{
			var result = resourceResolver.Load(link, audioType);
			if (!result)
				return result.Error;
			return Play(invoker, result.Value, meta);
		}

		public E<LocalStr> Play(InvokerData invoker, IEnumerable<PlaylistItem> items, int index = 0)
		{
			playlistManager.Clear();
			playlistManager.Queue(items.Select(x => UpdateItem(invoker, x)));
			playlistManager.Index = index;
			return StartCurrent(invoker);
		}

		public E<LocalStr> Play(InvokerData invoker, PlaylistItem item)
		{
			if (item is null)
				throw new ArgumentNullException(nameof(item));

			if (item.AudioResource is null)
				throw new Exception("Invalid playlist item");
			playlistManager.Clear();
			playlistManager.Queue(item);
			playlistManager.Index = 0;
			return StartResource(invoker, item);
		}

		public E<LocalStr> Play(InvokerData invoker) => StartCurrent(invoker);

		/// <summary>Plays the passed <see cref="PlayResource"/></summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="play">The associated resource type string to a factory.</param>
		/// <param name="meta">Allows overriding certain settings for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public E<LocalStr> Play(InvokerData invoker, PlayResource play, MetaData meta = null)
		{
			meta = meta ?? new MetaData();
			playlistManager.Clear();
			playlistManager.Queue(new PlaylistItem(play.BaseData, meta));
			playlistManager.Index = 0;
			return StartResource(invoker, play, meta);
		}

		private E<LocalStr> StartResource(InvokerData invoker, PlaylistItem item)
		{
			var result = resourceResolver.Load(item.AudioResource);
			if (!result)
				return result.Error;

			return StartResource(invoker, result.Value, item.Meta);
		}

		private E<LocalStr> StartResource(InvokerData invoker, PlayResource play, MetaData meta = null)
		{
			play.Meta = meta ?? play.Meta ?? new MetaData();
			var sourceLink = resourceResolver.RestoreLink(play.BaseData).OkOr(null);
			var playInfo = new PlayInfoEventArgs(invoker, play, sourceLink);
			BeforeResourceStarted?.Invoke(this, playInfo);

			if (string.IsNullOrWhiteSpace(play.PlayUri))
			{
				Log.Error("Internal resource error: link is empty (resource:{0})", play);
				return new LocalStr(strings.error_playmgr_internal_error);
			}

			Log.Debug("AudioResource start: {0}", play);
			var result = playerConnection.Play(play);
			if (!result)
			{
				Log.Error("Error return from player: {0}", result.Error);
				return new LocalStr(strings.error_playmgr_internal_error);
			}

			playerConnection.Volume = Tools.Clamp(playerConnection.Volume, confBot.Audio.Volume.Min, confBot.Audio.Volume.Max);
			CurrentPlayData = playInfo; // TODO meta as readonly
			AfterResourceStarted?.Invoke(this, playInfo);

			return R.Ok;
		}

		private E<LocalStr> StartCurrent(InvokerData invoker, bool manually = true)
		{
			PlaylistItem pli = playlistManager.Current;
			if (pli is null)
				return new LocalStr(strings.error_playlist_is_empty);
			var result = StartResource(invoker, pli);
			if (result.Ok)
				return result;
			Log.Warn("Skipping: {0} because {1}", pli, result.Error.Str);
			return Next(invoker, manually);
		}

		public E<LocalStr> Next(InvokerData invoker, bool manually = true)
		{
			PlaylistItem pli = null;
			for (int i = 0; i < 10; i++)
			{
				if ((pli = playlistManager.Next(manually)) is null) break;
				var result = StartResource(invoker, pli);
				if (result.Ok)
					return result;
				Log.Warn("Skipping: {0} because {1}", pli, result.Error.Str);
			}
			if (pli is null)
				return new LocalStr(strings.info_playmgr_no_next_song);
			else
				return new LocalStr(string.Format(strings.error_playmgr_many_songs_failed, "!next"));
		}

		public E<LocalStr> Previous(InvokerData invoker, bool manually = true)
		{
			PlaylistItem pli = null;
			for (int i = 0; i < 10; i++)
			{
				if ((pli = playlistManager.Previous(manually)) is null) break;
				var result = StartResource(invoker, pli);
				if (result.Ok)
					return result;
				Log.Warn("Skipping: {0} because {1}", pli, result.Error.Str);
			}
			if (pli is null)
				return new LocalStr(strings.info_playmgr_no_previous_song);
			else
				return new LocalStr(string.Format(strings.error_playmgr_many_songs_failed, "!previous"));
		}

		public void SongStoppedEvent(object sender, EventArgs e) => StopInternal(true);

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
				playerConnection.Stop();
			}

			CurrentPlayData = null;
			AfterResourceStopped?.Invoke(this, EventArgs.Empty);
		}

		public void Update(SongInfoChanged newInfo)
		{
			var data = CurrentPlayData;
			if (data is null)
				return;
			if (newInfo.Title != null)
				data.ResourceData.ResourceTitle = newInfo.Title;
			// further properties...
			OnResourceUpdated?.Invoke(this, data);
		}

		public static MetaData ParseAttributes(string[] attrs)
		{
			if (attrs is null || attrs.Length == 0)
				return null;

			var meta = new MetaData();
			foreach (var attr in attrs)
			{
				if (attr.StartsWith("@"))
				{
					meta.StartOffset = TextUtil.ParseTime(attr.Substring(1));
				}
			}
			return meta;
		}
	}
}
