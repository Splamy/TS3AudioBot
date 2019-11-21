// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

		public MatchCertainty MatchResource(string uri) => SoundcloudLink.IsMatch(uri).ToMatchCertainty();

		public MatchCertainty MatchPlaylist(string uri) => MatchResource(uri);

		public R<PlayResource, LocalStr> GetResource(string uri)
		{
			var uriObj = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(uri)}&client_id={SoundcloudClientId}");
			if (!WebWrapper.DownloadString(out string jsonResponse, uriObj))
			{
				if (!SoundcloudLink.IsMatch(uri))
					return new LocalStr(strings.error_media_invalid_uri);
				return YoutubeDlWrapped(uri);
			}
			var track = JsonConvert.DeserializeObject<JsonTrackInfo>(jsonResponse);
			var resource = CheckAndGet(track);
			if (resource is null)
				return new LocalStr(strings.error_media_internal_missing + " (parsedDict)");
			return GetResourceById(resource, false);
		}

		public R<PlayResource, LocalStr> GetResourceById(AudioResource resource) => GetResourceById(resource, true);

		private R<PlayResource, LocalStr> GetResourceById(AudioResource resource, bool allowNullName)
		{
			if (SoundcloudLink.IsMatch(resource.ResourceId))
				return GetResource(resource.ResourceId);

			if (resource.ResourceTitle is null)
			{
				if (!allowNullName) return new LocalStr(strings.error_media_internal_missing + " (title)");
				string link = RestoreLink(resource);
				if (link is null) return new LocalStr(strings.error_media_internal_missing + " (link)");
				return GetResource(link);
			}

			string finalRequest = $"https://api.soundcloud.com/tracks/{resource.ResourceId}/stream?client_id={SoundcloudClientId}";
			return new PlayResource(finalRequest, resource);
		}

		public string RestoreLink(AudioResource resource)
		{
			var artistName = resource.Get(AddArtist);
			var trackName = resource.Get(AddTrack);

			if (artistName != null && trackName != null)
				return $"https://soundcloud.com/{artistName}/{trackName}";

			return "https://soundcloud.com";
		}

		private static JToken ParseJson(string jsonResponse)
		{
			try { return JToken.Parse(jsonResponse); }
			catch (JsonReaderException) { return null; }
		}

		private AudioResource CheckAndGet(JsonTrackInfo track)
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

		private R<PlayResource, LocalStr> YoutubeDlWrapped(string link)
		{
			Log.Debug("Falling back to youtube-dl!");

			var result = YoutubeDlHelper.FindAndRunYoutubeDl(link);
			if (!result.Ok)
				return result.Error;

			var (title, urls) = result.Value;
			if (urls.Count == 0 || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(urls[0]))
				return new LocalStr(strings.error_ytdl_empty_response);

			Log.Debug("youtube-dl succeeded!");

			return new PlayResource(urls[0], new AudioResource(link, title, ResolverFor));
		}

		public R<Playlist, LocalStr> GetPlaylist(string url)
		{
			var uri = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(url)}&client_id={SoundcloudClientId}");
			if (!WebWrapper.DownloadString(out string jsonResponse, uri))
				return new LocalStr(strings.error_net_no_connection);

			var playlist = JsonConvert.DeserializeObject<JsonPlaylist>(jsonResponse);
			if (playlist is null || playlist.title is null || playlist.tracks is null)
			{
				Log.Debug("Parts of playlist response are empty: {@json}", playlist);
				return new LocalStr(strings.error_media_internal_missing + " (playlist)");
			}

			var plist = new Playlist().SetTitle(playlist.title);
			plist.AddRange(
				playlist.tracks.Select(track =>
				{
					var resource = CheckAndGet(track);
					if (resource is null)
						return null;
					return new PlaylistItem(resource);
				})
				.Where(track => track != null)
			);

			return plist;
		}

		public R<Stream, LocalStr> GetThumbnail(PlayResource playResource)
		{
			var uri = new Uri($"https://api.soundcloud.com/tracks/{playResource.BaseData.ResourceId}?client_id={SoundcloudClientId}");
			if (!WebWrapper.DownloadString(out string jsonResponse, uri))
				return new LocalStr(strings.error_net_no_connection);

			var parsedDict = ParseJson(jsonResponse);
			if (parsedDict is null)
				return new LocalStr(strings.error_media_internal_missing + " (parsedDict)");

			if (!parsedDict.TryCast<string>("artwork_url", out var imgUrl))
				return new LocalStr(strings.error_media_internal_missing + " (artwork_url)");

			// t500x500: 500px×500px
			// crop    : 400px×400px
			// t300x300: 300px×300px
			// large   : 100px×100px 
			imgUrl = imgUrl.Replace("-large", "-t300x300");

			var imgurl = new Uri(imgUrl);
			return WebWrapper.GetResponseUnsafe(imgurl);
		}

		public void Dispose() { }

#pragma warning disable CS0649, CS0169, IDE1006
		// ReSharper disable ClassNeverInstantiated.Local, InconsistentNaming
		private class JsonTrackInfo
		{
			public int id;
			public string title;
			public string permalink;
			public JsonTrackUser user;
		}
		private class JsonTrackUser
		{
			public string permalink;
		}
		private class JsonPlaylist
		{
			public string title;
			public JsonTrackInfo[] tracks;
		}
		// ReSharper enable ClassNeverInstantiated.Local, InconsistentNaming
#pragma warning restore CS0649, CS0169, IDE1006
	}
}
