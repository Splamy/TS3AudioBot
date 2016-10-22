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
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;
	using Helper;

	public sealed class SoundcloudFactory : IResourceFactory
	{
		public string SubCommandName => "soundcloud";
		public AudioType FactoryFor => AudioType.Soundcloud;
		public string SoundcloudClientID { get; private set; }

		public SoundcloudFactory()
		{
			SoundcloudClientID = "a9dd3403f858e105d7e266edc162a0c5";
		}

		public bool MatchLink(string link) => Regex.IsMatch(link, @"^https?\:\/\/(www\.)?soundcloud\.");

		public R<PlayResource> GetResource(string link)
		{
			string jsonResponse;
			var uri = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(link)}&client_id={SoundcloudClientID}");
			if (!WebWrapper.DownloadString(out jsonResponse, uri))
				return RResultCode.ScInvalidLink.ToString();
			var parsedDict = ParseJson(jsonResponse);
			int id = (int)parsedDict["id"];
			string title = (string)parsedDict["title"];
			return GetResourceById(new AudioResource(id.ToString(CultureInfo.InvariantCulture), title, AudioType.Soundcloud));
		}

		public R<PlayResource> GetResourceById(AudioResource resource)
		{
			if (resource.ResourceTitle == null)
			{
				string link = RestoreLink(resource.ResourceId);
				return GetResource(link); // TODO: rework recursive request call here (care endless loop)
			}

			string finalRequest = $"https://api.soundcloud.com/tracks/{resource.ResourceId}/stream?client_id={SoundcloudClientID}";
			return new PlayResource(finalRequest, resource);
		}

		public string RestoreLink(string id)
		{
			string jsonResponse;
			var uri = new Uri($"https://api.soundcloud.com/tracks/{id}?client_id={SoundcloudClientID}");
			if (!WebWrapper.DownloadString(out jsonResponse, uri))
				return null;
			var parsedDict = ParseJson(jsonResponse);
			return (string)parsedDict["permalink_url"];
		}

		private Dictionary<string, object> ParseJson(string jsonResponse) => (Dictionary<string, object>)Util.Serializer.DeserializeObject(jsonResponse);

		public void Dispose() { }
	}
}
