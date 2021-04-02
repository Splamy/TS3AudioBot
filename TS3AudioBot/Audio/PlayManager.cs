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
using System.Threading.Tasks;
using TS3AudioBot.Config;
using TS3AudioBot.Environment;
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
		private readonly ResolveContext resourceResolver;
		private readonly Stats stats;

		public PlayInfoEventArgs? CurrentPlayData { get; private set; }
		public bool IsPlaying => CurrentPlayData != null;

		public event AsyncEventHandler<PlayInfoEventArgs>? OnResourceUpdated;
		public event AsyncEventHandler<PlayInfoEventArgs>? BeforeResourceStarted;
		public event AsyncEventHandler<PlayInfoEventArgs>? AfterResourceStarted;
		public event AsyncEventHandler<SongEndEventArgs>? ResourceStopped;
		public event AsyncEventHandler? PlaybackStopped;

		public PlayManager(ConfBot config, Player playerConnection, PlaylistManager playlistManager, ResolveContext resourceResolver, Stats stats)
		{
			confBot = config;
			this.playerConnection = playerConnection;
			this.playlistManager = playlistManager;
			this.resourceResolver = resourceResolver;
			this.stats = stats;
		}

		public Task Enqueue(InvokerData invoker, AudioResource ar, PlayInfo? meta = null) => Enqueue(invoker, new PlaylistItem(ar, meta));
		public async Task Enqueue(InvokerData invoker, string message, string? audioType = null, PlayInfo? meta = null)
		{
			PlayResource playResource;
			try { playResource = await resourceResolver.Load(message, audioType); }
			catch
			{
				stats.TrackSongLoad(audioType, false, true);
				throw;
			}
			await Enqueue(invoker, PlaylistItem.From(playResource).MergeMeta(meta));
		}
		public Task Enqueue(InvokerData invoker, IEnumerable<PlaylistItem> items)
		{
			var startOff = playlistManager.CurrentList.Items.Count;
			playlistManager.Queue(items.Select(x => UpdateItem(invoker, x)));
			return PostEnqueue(invoker, startOff);
		}
		public Task Enqueue(InvokerData invoker, PlaylistItem item)
		{
			var startOff = playlistManager.CurrentList.Items.Count;
			playlistManager.Queue(UpdateItem(invoker, item));
			return PostEnqueue(invoker, startOff);
		}

		private static PlaylistItem UpdateItem(InvokerData invoker, PlaylistItem item)
		{
			item.PlayInfo ??= new PlayInfo();
			item.PlayInfo.ResourceOwnerUid = invoker.ClientUid;
			return item;
		}

		private async Task PostEnqueue(InvokerData invoker, int startIndex)
		{
			if (IsPlaying)
				return;
			playlistManager.Index = startIndex;
			await StartCurrent(invoker);
		}

		/// <summary>Tries to play the passed <see cref="AudioResource"/></summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="ar">The resource to load and play.</param>
		/// <param name="meta">Allows overriding certain settings for the resource. Can be null.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public async Task Play(InvokerData invoker, AudioResource ar, PlayInfo? meta = null)
		{
			if (ar is null)
				throw new ArgumentNullException(nameof(ar));

			PlayResource playResource;
			try { playResource = await resourceResolver.Load(ar); }
			catch
			{
				stats.TrackSongLoad(ar.AudioType, false, true);
				throw;
			}
			await Play(invoker, playResource.MergeMeta(meta));
		}

		/// <summary>Tries to play the passed link.</summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="link">The link to resolve, load and play.</param>
		/// <param name="audioType">The associated resource type string to a factory.</param>
		/// <param name="meta">Allows overriding certain settings for the resource. Can be null.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public async Task Play(InvokerData invoker, string link, string? audioType = null, PlayInfo? meta = null)
		{
			PlayResource playResource;
			try { playResource = await resourceResolver.Load(link, audioType); }
			catch
			{
				stats.TrackSongLoad(audioType, false, true);
				throw;
			}
			await Play(invoker, playResource.MergeMeta(meta));
		}

		public Task Play(InvokerData invoker, IEnumerable<PlaylistItem> items, int index = 0)
		{
			playlistManager.Clear();
			playlistManager.Queue(items.Select(x => UpdateItem(invoker, x)));
			playlistManager.Index = index;
			return StartCurrent(invoker);
		}

		public Task Play(InvokerData invoker, PlaylistItem item)
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

		public Task Play(InvokerData invoker) => StartCurrent(invoker);

		/// <summary>Plays the passed <see cref="PlayResource"/></summary>
		/// <param name="invoker">The invoker of this resource. Used for responses and association.</param>
		/// <param name="play">The associated resource type string to a factory.</param>
		/// <param name="meta">Allows overriding certain settings for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public Task Play(InvokerData invoker, PlayResource play)
		{
			playlistManager.Clear();
			playlistManager.Queue(PlaylistItem.From(play));
			playlistManager.Index = 0;
			stats.TrackSongLoad(play.AudioResource.AudioType, true, true);
			return StartResource(invoker, play);
		}

		private async Task StartResource(InvokerData invoker, PlaylistItem item)
		{
			PlayResource playResource;
			try { playResource = await resourceResolver.Load(item.AudioResource); }
			catch
			{
				stats.TrackSongLoad(item.AudioResource.AudioType, false, false);
				throw;
			}
			stats.TrackSongLoad(item.AudioResource.AudioType, true, false);
			await StartResource(invoker, playResource.MergeMeta(item.PlayInfo));
		}

		private async Task StartResource(InvokerData invoker, PlayResource play)
		{
			var sourceLink = resourceResolver.RestoreLink(play.AudioResource);
			var playInfo = new PlayInfoEventArgs(invoker, play, sourceLink);
			await BeforeResourceStarted.InvokeAsync(this, playInfo);

			if (string.IsNullOrWhiteSpace(play.PlayUri))
			{
				Log.Error("Internal resource error: link is empty (resource:{0})", play);
				throw Error.LocalStr(strings.error_playmgr_internal_error);
			}

			Log.Debug("AudioResource start: {0}", play);
			try { await playerConnection.Play(play); }
			catch (AudioBotException ex)
			{
				Log.Error("Error return from player: {0}", ex.Message);
				throw Error.Exception(ex).LocalStr(strings.error_playmgr_internal_error);
			}

			playerConnection.Volume = Tools.Clamp(playerConnection.Volume, confBot.Audio.Volume.Min, confBot.Audio.Volume.Max);
			CurrentPlayData = playInfo; // TODO meta as readonly
			await AfterResourceStarted.InvokeAsync(this, playInfo);
		}

		private async Task StartCurrent(InvokerData invoker, bool manually = true)
		{
			var pli = playlistManager.Current;
			if (pli is null)
				throw Error.LocalStr(strings.error_playlist_is_empty);
			try
			{
				await StartResource(invoker, pli);
			}
			catch (AudioBotException ex)
			{
				Log.Warn("Skipping: {0} because {1}", pli, ex.Message);
				await Next(invoker, manually);
			}
		}

		public async Task Next(InvokerData invoker, bool manually = true)
		{
			PlaylistItem? pli = null;
			for (int i = 0; i < 10; i++)
			{
				pli = playlistManager.Next(manually);
				if (pli is null) break;
				try
				{
					await StartResource(invoker, pli);
					return;
				}
				catch (AudioBotException ex) { Log.Warn("Skipping: {0} because {1}", pli, ex.Message); }
			}
			if (pli is null)
				throw Error.LocalStr(strings.info_playmgr_no_next_song);
			else
				throw Error.LocalStr(string.Format(strings.error_playmgr_many_songs_failed, "!next"));
		}

		public async Task Previous(InvokerData invoker, bool manually = true)
		{
			PlaylistItem? pli = null;
			for (int i = 0; i < 10; i++)
			{
				pli = playlistManager.Previous(manually);
				if (pli is null) break;
				try
				{
					await StartResource(invoker, pli);
					return;
				}
				catch (AudioBotException ex) { Log.Warn("Skipping: {0} because {1}", pli, ex.Message); }
			}
			if (pli is null)
				throw Error.LocalStr(strings.info_playmgr_no_previous_song);
			else
				throw Error.LocalStr(string.Format(strings.error_playmgr_many_songs_failed, "!previous"));
		}

		public async Task SongStoppedEvent(object? sender, EventArgs e) => await StopInternal(true);

		public Task Stop() => StopInternal(false);

		private async Task StopInternal(bool songEndedByCallback)
		{
			await ResourceStopped.InvokeAsync(this, new SongEndEventArgs(songEndedByCallback));

			if (songEndedByCallback)
			{
				try
				{
					await Next(CurrentPlayData?.Invoker ?? InvokerData.Anonymous, false);
					return;
				}
				catch (AudioBotException ex) { Log.Info("Song queue ended: {0}", ex.Message); }
			}
			else
			{
				playerConnection.Stop();
			}

			CurrentPlayData = null;
			PlaybackStopped?.Invoke(this, EventArgs.Empty);
		}

		public async Task Update(SongInfoChanged newInfo)
		{
			var data = CurrentPlayData;
			if (data is null)
				return;
			if (newInfo.Title != null)
				data.ResourceData.ResourceTitle = newInfo.Title;
			// further properties...
			try
			{
				await OnResourceUpdated.InvokeAsync(this, data);
			}
			catch (AudioBotException ex)
			{
				Log.Warn(ex, "Error in OnResourceUpdated event.");
			}
		}

		public static PlayInfo? ParseAttributes(string[] attrs)
		{
			if (attrs is null || attrs.Length == 0)
				return null;

			var meta = new PlayInfo();
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
