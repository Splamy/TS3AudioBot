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
	using Newtonsoft.Json.Linq;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;

	public sealed class TwitchFactory : IResourceFactory
	{
		private static readonly Regex TwitchMatch = new Regex(@"^(https?://)?(www\.)?twitch\.tv/(\w+)", Util.DefaultRegexConfig);
		private static readonly Regex M3U8ExtMatch = new Regex(@"#([\w-]+)(:(([\w-]+)=(""[^""]*""|[^,]+),?)*)?", Util.DefaultRegexConfig);
		private const string TwitchClientId = "t9nlhlxnfux3gk2d6z1p093rj2c71i3";

		public string FactoryFor => "twitch";

		public MatchCertainty MatchResource(string uri) => TwitchMatch.IsMatch(uri).ToMatchCertainty();

		public R<PlayResource> GetResource(string url)
		{
			var match = TwitchMatch.Match(url);
			if (!match.Success)
				return RResultCode.TwitchInvalidUrl.ToString();
			return GetResourceById(new AudioResource(match.Groups[3].Value, null, FactoryFor));
		}

		public R<PlayResource> GetResourceById(AudioResource resource)
		{
			var channel = resource.ResourceId;

			// request api token
			if (!WebWrapper.DownloadString(out string jsonResponse, new Uri($"http://api.twitch.tv/api/channels/{channel}/access_token"), ("Client-ID", TwitchClientId)))
				return RResultCode.NoConnection.ToString();

			var jObj = JObject.Parse(jsonResponse);

			// request m3u8 file
			var tokenResult = jObj.TryCast<string>("token");
			var sigResult = jObj.TryCast<string>("sig");
			if (!tokenResult.Ok || !sigResult.Ok)
				return "Invalid api response";
			var token = Uri.EscapeUriString(tokenResult.Value);
			var sig = sigResult.Value;
			// guaranteed to be random, chosen by fair dice roll.
			const int random = 4;
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
								streamData.QualityType = Enum.TryParse(TextUtil.StripQuotes(value), out StreamQuality quality)
								  ? quality
								  : StreamQuality.unknown; break;
							}
						}

						streamData.Url = reader.ReadLine();
						dataList.Add(streamData);
						break;
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

		private static int SelectStream(List<StreamData> list) => list.FindIndex(s => s.QualityType == StreamQuality.audio_only);

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
