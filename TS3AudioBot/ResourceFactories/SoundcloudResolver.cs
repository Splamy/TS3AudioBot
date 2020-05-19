// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;

namespace TS3AudioBot.ResourceFactories
{
	public sealed class SoundcloudResolver : IResourceResolver, IPlaylistResolver, IThumbnailResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex SoundcloudLink = new Regex(@"^https?\:\/\/(www\.)?soundcloud\.", Util.DefaultRegexConfig);
		private const string SoundcloudClientId = "a9dd3403f858e105d7e266edc162a0c5";

		private const string AddArtist = "artist";
		private const string AddTrack = "track";

		public string ResolverFor => "soundcloud";

		public MatchCertainty MatchResource(ResolveContext? _, string uri) => SoundcloudLink.IsMatch(uri).ToMatchCertainty();

		public MatchCertainty MatchPlaylist(ResolveContext? _, string uri) => MatchResource(null, uri);

		public async Task<PlayResource> GetResource(ResolveContext? _, string uri)
		{
			JsonTrackInfo? track = null;
			try
			{
				track = await WebWrapper
					.Request($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(uri)}&client_id={SoundcloudClientId}")
					.AsJson<JsonTrackInfo>();
			}
			catch (Exception ex) { Log.Debug(ex, "Failed to get via api"); }

			if (track is null)
			{
				if (!SoundcloudLink.IsMatch(uri))
					throw Error.LocalStr(strings.error_media_invalid_uri);
				return await YoutubeDlWrappedAsync(uri);
			}
			var resource = CheckAndGet(track);
			if (resource is null)
				throw Error.LocalStr(strings.error_media_internal_missing + " (parsedDict)");
			return await GetResourceById(resource, false);
		}

		public Task<PlayResource> GetResourceById(ResolveContext _, AudioResource resource) => GetResourceById(resource, true);

		private async Task<PlayResource> GetResourceById(AudioResource resource, bool allowNullName)
		{
			if (SoundcloudLink.IsMatch(resource.ResourceId))
				return await GetResource(null, resource.ResourceId);

			if (resource.ResourceTitle is null)
			{
				if (!allowNullName) throw Error.LocalStr(strings.error_media_internal_missing + " (title)");
				string link = RestoreLink(null, resource);
				if (link is null) throw Error.LocalStr(strings.error_media_internal_missing + " (link)");
				return await GetResource(null, link);
			}

			string finalRequest = $"https://api.soundcloud.com/tracks/{resource.ResourceId}/stream?client_id={SoundcloudClientId}";
			return new PlayResource(finalRequest, resource);
		}

		public string RestoreLink(ResolveContext? _, AudioResource resource)
		{
			var artistName = resource.Get(AddArtist);
			var trackName = resource.Get(AddTrack);

			if (artistName != null && trackName != null)
				return $"https://soundcloud.com/{artistName}/{trackName}";

			return "https://soundcloud.com";
		}

		private AudioResource? CheckAndGet(JsonTrackInfo track)
		{
			if (track == null || track.id == 0 || track.title == null
				|| track.permalink == null || track.user?.permalink == null)
			{
				Log.Debug("Parts of track response are empty: {@json}", track);
				return null;
			}

			return new AudioResource(
				track.id.ToString(CultureInfo.InvariantCulture),
				track.title,
				ResolverFor)
				.Add(AddArtist, track.user.permalink)
				.Add(AddTrack, track.permalink);
		}

		private async Task<PlayResource> YoutubeDlWrappedAsync(string link)
		{
			Log.Debug("Falling back to youtube-dl!");

			var response = await YoutubeDlHelper.GetSingleVideo(link);
			var title = response.title ?? $"Soundcloud-{link}";
			var format = YoutubeDlHelper.FilterBest(response.formats);
			var url = format?.url;

			if (string.IsNullOrEmpty(url))
				throw Error.LocalStr(strings.error_ytdl_empty_response);

			Log.Debug("youtube-dl succeeded!");

			return new PlayResource(url, new AudioResource(link, title, ResolverFor));
		}

		public async Task<Playlist> GetPlaylist(ResolveContext _, string url)
		{
			var playlist = await WebWrapper
				.Request($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(url)}&client_id={SoundcloudClientId}")
				.AsJson<JsonPlaylist>();

			if (playlist is null || playlist.title is null || playlist.tracks is null)
			{
				Log.Debug("Parts of playlist response are empty: {@json}", playlist);
				throw Error.LocalStr(strings.error_media_internal_missing + " (playlist)");
			}

			var plist = new Playlist().SetTitle(playlist.title);
			plist.AddRange(
				playlist.tracks.Select(track =>
				{
					var resource = CheckAndGet(track);
					if (resource is null)
						return null!;
					return new PlaylistItem(resource);
				})
				.Where(track => track != null)
			);

			return plist;
		}

		public async Task GetThumbnail(ResolveContext _, PlayResource playResource, Func<Stream, Task> action)
		{
			var thumb = await WebWrapper
				.Request($"https://api.soundcloud.com/tracks/{playResource.AudioResource.ResourceId}?client_id={SoundcloudClientId}")
				.AsJson<JsonTumbnailMinimal>();
			if (thumb is null)
				throw Error.LocalStr(strings.error_media_internal_missing + " (thumb)");
			if (thumb.artwork_url is null)
				throw Error.LocalStr(strings.error_media_internal_missing + " (artwork_url)");

			// t500x500: 500px×500px
			// crop    : 400px×400px
			// t300x300: 300px×300px
			// large   : 100px×100px 
			await WebWrapper.Request(thumb.artwork_url.Replace("-large", "-t300x300")).ToStream(action);
		}

		public void Dispose() { }

#pragma warning disable CS0649, CS0169, IDE1006
		// ReSharper disable ClassNeverInstantiated.Local, InconsistentNaming
		private class JsonTrackInfo
		{
			public int id { get; set; }
			public string? title { get; set; }
			public string? permalink { get; set; }
			public JsonTrackUser? user { get; set; }
		}
		private class JsonTrackUser
		{
			public string? permalink { get; set; }
		}
		private class JsonPlaylist
		{
			public string? title { get; set; }
			public JsonTrackInfo[]? tracks { get; set; }
		}
		private class JsonTumbnailMinimal
		{
			public string? artwork_url { get; set; }
		}
		// ReSharper enable ClassNeverInstantiated.Local, InconsistentNaming
#pragma warning restore CS0649, CS0169, IDE1006
	}
}
