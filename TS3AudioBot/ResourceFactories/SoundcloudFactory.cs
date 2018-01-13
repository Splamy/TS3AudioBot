// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Linq;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;
	using Helper;
	using System.Drawing;

	public sealed class SoundcloudFactory : IResourceFactory, IPlaylistFactory, IThumbnailFactory
	{
		private static readonly Regex SoundcloudLink = new Regex(@"^https?\:\/\/(www\.)?soundcloud\.", Util.DefaultRegexConfig);
		private const string SoundcloudClientId = "a9dd3403f858e105d7e266edc162a0c5";

		public string FactoryFor => "soundcloud";

		public MatchCertainty MatchResource(string uri) => SoundcloudLink.IsMatch(uri).ToMatchCertainty();

		public MatchCertainty MatchPlaylist(string uri) => MatchResource(uri);

		public R<PlayResource> GetResource(string link)
		{
			var uri = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(link)}&client_id={SoundcloudClientId}");
			if (!WebWrapper.DownloadString(out string jsonResponse, uri))
			{
				if (!SoundcloudLink.IsMatch(link))
					return "Not a valid soundcloud link. Please pass the full link";
				return YoutubeDlWrapped(link);
			}
			var parsedDict = ParseJson(jsonResponse);
			var resource = ParseDictToResource(parsedDict);
			if (resource == null)
				return "Empty or missing response parts (parsedDict)";
			return GetResourceById(resource, false);
		}

		public R<PlayResource> GetResourceById(AudioResource resource) => GetResourceById(resource, true);

		private R<PlayResource> GetResourceById(AudioResource resource, bool allowNullName)
		{
			if (SoundcloudLink.IsMatch(resource.ResourceId))
				return GetResource(resource.ResourceId);

			if (resource.ResourceTitle == null)
			{
				if (!allowNullName) return "Could not restore null title.";
				string link = RestoreLink(resource.ResourceId);
				if (link == null) return "Could not restore link from id";
				return GetResource(link);
			}

			string finalRequest = $"https://api.soundcloud.com/tracks/{resource.ResourceId}/stream?client_id={SoundcloudClientId}";
			return new PlayResource(finalRequest, resource);
		}

		public string RestoreLink(string id)
		{
			if (SoundcloudLink.IsMatch(id))
				return id;

			var uri = new Uri($"https://api.soundcloud.com/tracks/{id}?client_id={SoundcloudClientId}");
			if (!WebWrapper.DownloadString(out string jsonResponse, uri))
				return null;
			var parsedDict = ParseJson(jsonResponse);
			return parsedDict?["permalink_url"] as string;
		}

		private static Dictionary<string, object> ParseJson(string jsonResponse)
			=> (Dictionary<string, object>)Util.Serializer.DeserializeObject(jsonResponse);

		private AudioResource ParseDictToResource(Dictionary<string, object> dict)
		{
			if (dict == null) return null;
			if (!(dict["id"] is int id)) return null;
			if (!(dict["title"] is string title)) return null;
			return new AudioResource(id.ToString(CultureInfo.InvariantCulture), title, FactoryFor);
		}

		private R<PlayResource> YoutubeDlWrapped(string link)
		{
			Log.Write(Log.Level.Debug, "SC Ruined!");

			var result = YoutubeDlHelper.FindAndRunYoutubeDl(link);
			if (!result.Ok)
				return result.Message;

			var response = result.Value;
			string title = response.Item1;
			string url = response.Item2.FirstOrDefault();
			if (response.Item2.Count == 0 || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url))
				return "No youtube-dl response";

			Log.Write(Log.Level.Debug, "SC Saved!");

			return new PlayResource(url, new AudioResource(link, title, FactoryFor));
		}

		public R<Playlist> GetPlaylist(string url)
		{
			var uri = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(url)}&client_id={SoundcloudClientId}");
			if (!WebWrapper.DownloadString(out string jsonResponse, uri))
				return RResultCode.ScInvalidLink.ToString();

			var parsedDict = ParseJson(jsonResponse);
			if (parsedDict == null)
				return "Empty or missing response parts (parsedDict)";

			string name = PlaylistManager.CleanseName(parsedDict["title"] as string);
			var plist = new Playlist(name);

			if (!(parsedDict["tracks"] is object[] tracks))
				return "Empty or missing response parts (tracks)";

			foreach (var track in tracks.OfType<Dictionary<string, object>>())
			{
				var resource = ParseDictToResource(track);
				if (resource == null)
					continue;

				plist.AddItem(new PlaylistItem(resource));
			}

			return plist;
		}

		public R<Image> GetThumbnail(PlayResource playResource)
		{
			var uri = new Uri($"https://api.soundcloud.com/tracks/{playResource.BaseData.ResourceId}?client_id={SoundcloudClientId}");
			if (!WebWrapper.DownloadString(out string jsonResponse, uri))
				return "Error or no response by soundcloud";

			var parsedDict = ParseJson(jsonResponse);
			if (parsedDict == null)
				return "Empty or missing response parts (parsedDict)";

			if (!(parsedDict["artwork_url"] is string imgUrl))
				return "Empty or missing response parts (artwork_url)";

			// t500x500: 500px×500px
			// crop    : 400px×400px
			// t300x300: 300px×300px
			// large   : 100px×100px 
			imgUrl = imgUrl.Replace("-large", "-t300x300");

			var imgurl = new Uri(imgUrl);
			Image img = null;
			var resresult = WebWrapper.GetResponse(imgurl, (webresp) =>
			{
				using (var stream = webresp.GetResponseStream())
				{
					if (stream != null)
						img = Image.FromStream(stream);
				}
			});
			if (resresult != ValidateCode.Ok)
				return "Error while reading image";
			return img;
		}

		public void Dispose() { }
	}
}
