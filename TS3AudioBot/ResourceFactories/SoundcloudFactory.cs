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

namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Linq;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;
	using Helper;

	public sealed class SoundcloudFactory : IResourceFactory, IPlaylistFactory
	{
		public string SubCommandName => "soundcloud";
		public AudioType FactoryFor => AudioType.Soundcloud;
		private readonly string soundcloudClientId;

		public SoundcloudFactory()
		{
			soundcloudClientId = "a9dd3403f858e105d7e266edc162a0c5";
		}

		public bool MatchLink(string link) => Regex.IsMatch(link, @"^https?\:\/\/(www\.)?soundcloud\.");

		public R<PlayResource> GetResource(string link)
		{
			string jsonResponse;
			var uri = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(link)}&client_id={soundcloudClientId}");
			if (!WebWrapper.DownloadString(out jsonResponse, uri))
			{
				if (!MatchLink(link))
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
			if (MatchLink(resource.ResourceId))
				return GetResource(resource.ResourceId);

			if (resource.ResourceTitle == null)
			{
				if (!allowNullName) return "Could not restore null title.";
				string link = RestoreLink(resource.ResourceId);
				if (link == null) return "Could not restore link from id";
				return GetResource(link);
			}

			string finalRequest = $"https://api.soundcloud.com/tracks/{resource.ResourceId}/stream?client_id={soundcloudClientId}";
			return new PlayResource(finalRequest, resource);
		}

		public string RestoreLink(string id)
		{
			if (MatchLink(id))
				return id;

			string jsonResponse;
			var uri = new Uri($"https://api.soundcloud.com/tracks/{id}?client_id={soundcloudClientId}");
			if (!WebWrapper.DownloadString(out jsonResponse, uri))
				return null;
			var parsedDict = ParseJson(jsonResponse);
			return parsedDict?["permalink_url"] as string;
		}

		private static Dictionary<string, object> ParseJson(string jsonResponse)
			=> (Dictionary<string, object>)Util.Serializer.DeserializeObject(jsonResponse);

		private static AudioResource ParseDictToResource(Dictionary<string, object> dict)
		{
			if (dict == null) return null;
			var id = dict["id"] as int?;
			if (!id.HasValue) return null;
			var title = dict["title"] as string;
			if (title == null) return null;
			return new AudioResource(id.Value.ToString(CultureInfo.InvariantCulture), title, AudioType.Soundcloud);
		}

		private static R<PlayResource> YoutubeDlWrapped(string link)
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

			return new PlayResource(url, new AudioResource(link, title, AudioType.Soundcloud));
		}

		public R<Playlist> GetPlaylist(string url)
		{
			var uri = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(url)}&client_id={soundcloudClientId}");
			string jsonResponse;
			if (!WebWrapper.DownloadString(out jsonResponse, uri))
				return RResultCode.ScInvalidLink.ToString();

			var parsedDict = ParseJson(jsonResponse);
			if (parsedDict == null)
				return "Empty or missing response parts (parsedDict)";

			string name = PlaylistManager.CleanseName(parsedDict["title"] as string);
			var plist = new Playlist(name);

			var tracks = parsedDict["tracks"] as object[];
			if (tracks == null)
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

		public void Dispose() { }
	}
}
