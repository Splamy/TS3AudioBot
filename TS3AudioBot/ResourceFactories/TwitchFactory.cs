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

	public sealed class TwitchFactory : IResourceFactory
	{
		private static readonly Regex TwitchMatch = new Regex(@"^(https?://)?(www\.)?twitch\.tv/(\w+)", Util.DefaultRegexConfig);
		private static readonly Regex M3U8ExtMatch = new Regex(@"#([\w-]+)(:(([\w-]+)=(""[^""]*""|[^,]+),?)*)?", Util.DefaultRegexConfig);

		public string SubCommandName => "twitch";
		public AudioType FactoryFor => AudioType.Twitch;
		private readonly string twitchClientId;

		public TwitchFactory()
		{
			twitchClientId = "t9nlhlxnfux3gk2d6z1p093rj2c71i3";
		}

		public R<PlayResource> GetResource(string url)
		{
			var match = TwitchMatch.Match(url);
			if (!match.Success)
				return RResultCode.TwitchInvalidUrl.ToString();
			return GetResourceById(new AudioResource(match.Groups[3].Value, null, AudioType.Twitch));
		}

		public R<PlayResource> GetResourceById(AudioResource resource)
		{
			var channel = resource.ResourceId;

			// request api token
			if (!WebWrapper.DownloadString(out string jsonResponse, new Uri($"http://api.twitch.tv/api/channels/{channel}/access_token"), new Tuple<string, string>("Client-ID", twitchClientId)))
				return RResultCode.NoConnection.ToString();

			var jsonDict = (Dictionary<string, object>)Util.Serializer.DeserializeObject(jsonResponse);

			// request m3u8 file
			var token = Uri.EscapeUriString(jsonDict["token"].ToString());
			var sig = jsonDict["sig"];
			// guaranteed to be random, chosen by fair dice roll.
			var random = 4;
			if (!WebWrapper.DownloadString(out string m3u8, new Uri($"http://usher.twitch.tv/api/channel/hls/{channel}.m3u8?player=twitchweb&&token={token}&sig={sig}&allow_audio_only=true&allow_source=true&type=any&p={random}")))
				return RResultCode.NoConnection.ToString();

			// parse m3u8 file
			var dataList = new List<StreamData>();
			using (var reader = new System.IO.StringReader(m3u8))
			{
				var header = reader.ReadLine();
				if (string.IsNullOrEmpty(header) || header != "#EXTM3U")
					return RResultCode.TwitchMalformedM3u8File.ToString();

				while (true)
				{
					var blockInfo = reader.ReadLine();
					if (string.IsNullOrEmpty(blockInfo))
						break;

					var match = M3U8ExtMatch.Match(blockInfo);
					if (!match.Success)
						continue;

					switch (match.Groups[1].Value)
					{
						case "EXT-X-TWITCH-INFO": break; // Ignore twitch info line
						case "EXT-X-MEDIA":
							string streamInfo = reader.ReadLine();
							Match infoMatch;
							if (string.IsNullOrEmpty(streamInfo) ||
								 !(infoMatch = M3U8ExtMatch.Match(streamInfo)).Success ||
								 infoMatch.Groups[1].Value != "EXT-X-STREAM-INF")
								return RResultCode.TwitchMalformedM3u8File.ToString();

							var streamData = new StreamData();
							// #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=128000,CODECS="mp4a.40.2",VIDEO="audio_only"
							for (int i = 0; i < infoMatch.Groups[3].Captures.Count; i++)
							{
								string key = infoMatch.Groups[4].Captures[i].Value.ToUpper(CultureInfo.InvariantCulture);
								string value = infoMatch.Groups[5].Captures[i].Value;

								switch (key)
								{
									case "BANDWIDTH": streamData.Bandwidth = int.Parse(value, CultureInfo.InvariantCulture); break;
									case "CODECS": streamData.Codec = TextUtil.StripQuotes(value); break;
									case "VIDEO":
										StreamQuality quality;
										if (Enum.TryParse(TextUtil.StripQuotes(value), out quality))
											streamData.QualityType = quality;
										else
											streamData.QualityType = StreamQuality.unknown;
										break;
								}
							}

							streamData.Url = reader.ReadLine();
							dataList.Add(streamData);
							break;
						default: break;
					}
				}
			}

			// Validation Process

			if (dataList.Count <= 0)
				return RResultCode.TwitchNoStreamsExtracted.ToString();

			int codec = SelectStream(dataList);
			if (codec < 0)
				return "The stream has no audio_only version.";

			return new PlayResource(dataList[codec].Url, resource.ResourceTitle != null ? resource : resource.WithName($"Twitch channel: {channel}"));
		}

		public bool MatchLink(string uri) => TwitchMatch.IsMatch(uri);

		private int SelectStream(List<StreamData> list)
		{
			int autoselectIndex = list.FindIndex(s => s.QualityType == StreamQuality.audio_only);
			return autoselectIndex;
		}

		public string RestoreLink(string id) => "http://www.twitch.tv/" + id;

		public void Dispose() { }
	}

	public sealed class StreamData
	{
		public StreamQuality QualityType { get; set; }
		public int Bandwidth { get; set; }
		public string Codec { get; set; }
		public string Url { get; set; }
	}

	public enum StreamQuality
	{
		unknown,
		chunked,
		high,
		medium,
		low,
		mobile,
		audio_only,
	}
}
