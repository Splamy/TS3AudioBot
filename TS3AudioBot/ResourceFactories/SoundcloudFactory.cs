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
	using Helper;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using System;
	using System.Drawing;
	using System.Globalization;
	using System.Text.RegularExpressions;

	public sealed class SoundcloudFactory : IResourceFactory, IPlaylistFactory, IThumbnailFactory
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
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
			var resource = ParseJObjectToResource(parsedDict);
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
			var jobj = ParseJson(jsonResponse);
			return jobj.TryCast<string>("permalink_url").OkOr(null);
		}

		private static JToken ParseJson(string jsonResponse)
		{
			try { return JToken.Parse(jsonResponse); }
			catch (JsonReaderException) { return null; }
		}

		private AudioResource ParseJObjectToResource(JToken jobj)
		{
			if (jobj == null) return null;
			var id = jobj.TryCast<int>("id");
			if (!id.Ok) return null;
			var title = jobj.TryCast<string>("title");
			if (!title.Ok) return null;
			return new AudioResource(id.Value.ToString(CultureInfo.InvariantCulture), title.Value, FactoryFor);
		}

		private R<PlayResource> YoutubeDlWrapped(string link)
		{
			Log.Debug("Falling back to youtube-dl!");

			var result = YoutubeDlHelper.FindAndRunYoutubeDl(link);
			if (!result.Ok)
				return result.Error;

			var (title, urls) = result.Value;
			if (urls.Count == 0 || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(urls[0]))
				return "No youtube-dl response";

			Log.Debug("youtube-dl succeeded!");

			return new PlayResource(urls[0], new AudioResource(link, title, FactoryFor));
		}

		public R<Playlist> GetPlaylist(string url)
		{
			var uri = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(url)}&client_id={SoundcloudClientId}");
			if (!WebWrapper.DownloadString(out string jsonResponse, uri))
				return RResultCode.ScInvalidLink.ToString();

			var parsedDict = ParseJson(jsonResponse);
			if (parsedDict == null)
				return "Empty or missing response parts (parsedDict)";

			string name = PlaylistManager.CleanseName(parsedDict.TryCast<string>("title").OkOr(null));
			var plist = new Playlist(name);

			var tracksJobj = parsedDict["tracks"];
			if (tracksJobj == null)
				return "Empty or missing response parts (tracks)";

			foreach (var track in tracksJobj)
			{
				var resource = ParseJObjectToResource(track);
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

			var imgUrl = parsedDict.TryCast<string>("artwork_url").OkOr(null);
			if (imgUrl == null)
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
