// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;

namespace TS3AudioBot.ResourceFactories
{
	public sealed class TwitchResolver : IResourceResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex TwitchMatch = new Regex(@"^(https?://)?(www\.)?twitch\.tv/(\w+)", Util.DefaultRegexConfig);
		private static readonly Regex M3U8ExtMatch = new Regex(@"#([\w-]+)(:(([\w-]+)=(""[^""]*""|[^,]+),?)*)?", Util.DefaultRegexConfig);
		private const string TwitchClientId = "t9nlhlxnfux3gk2d6z1p093rj2c71i3";
		// See: https://github.com/streamlink/streamlink/issues/2680
		private const string TwitchClientIdPrivate = "kimne78kx3ncx6brgo4mv6wki5h1ko";

		public string ResolverFor => "twitch";

		public MatchCertainty MatchResource(ResolveContext _, string uri) => TwitchMatch.IsMatch(uri).ToMatchCertainty();

		public R<PlayResource, LocalStr> GetResource(ResolveContext _, string uri)
		{
			var match = TwitchMatch.Match(uri);
			if (!match.Success)
				return new LocalStr(strings.error_media_invalid_uri);
			return GetResourceById(null, new AudioResource(match.Groups[3].Value, null, ResolverFor));
		}

		public R<PlayResource, LocalStr> GetResourceById(ResolveContext _, AudioResource resource)
		{
			var channel = resource.ResourceId;

			// request api token
			if (!WebWrapper.DownloadString(out string jsonResponse, new Uri($"https://api.twitch.tv/api/channels/{channel}/access_token"), ("Client-ID", TwitchClientIdPrivate)))
				return new LocalStr(strings.error_net_no_connection);

			JsonAccessToken access;
			try
			{
				access = JsonConvert.DeserializeObject<JsonAccessToken>(jsonResponse);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Failed to parse jsonResponse. (Data: {0})", jsonResponse);
				return new LocalStr(strings.error_media_internal_invalid + " (jsonResponse)");
			}

			// request m3u8 file
			if (access.token is null || access.sig is null)
				return new LocalStr(strings.error_media_internal_invalid + " (tokenResult|sigResult)");
			var token = Uri.EscapeUriString(access.token);
			var sig = access.sig;
			// guaranteed to be random, chosen by fair dice roll.
			const int random = 4;
			if (!WebWrapper.DownloadString(out string m3u8, new Uri($"http://usher.twitch.tv/api/channel/hls/{channel}.m3u8?player=twitchweb&&token={token}&sig={sig}&allow_audio_only=true&allow_source=true&type=any&p={random}")))
				return new LocalStr(strings.error_net_no_connection);

			// parse m3u8 file
			var dataList = new List<StreamData>();
			using (var reader = new System.IO.StringReader(m3u8))
			{
				var header = reader.ReadLine();
				if (string.IsNullOrEmpty(header) || header != "#EXTM3U")
					return new LocalStr(strings.error_media_internal_missing + " (m3uHeader)");

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
						if (string.IsNullOrEmpty(streamInfo)
							|| !(infoMatch = M3U8ExtMatch.Match(streamInfo)).Success
							|| infoMatch.Groups[1].Value != "EXT-X-STREAM-INF")
						{
							return new LocalStr(strings.error_media_internal_missing + " (m3uStream)");
						}

						var streamData = new StreamData();
						// #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=128000,CODECS="mp4a.40.2",VIDEO="audio_only"
						for (int i = 0; i < infoMatch.Groups[3].Captures.Count; i++)
						{
							string key = infoMatch.Groups[4].Captures[i].Value.ToUpperInvariant();
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
				return new LocalStr(strings.error_media_no_stream_extracted);

			int codec = SelectStream(dataList);
			if (codec < 0)
				return new LocalStr(strings.error_media_no_stream_extracted);

			if (resource.ResourceTitle == null)
				resource.ResourceTitle = $"Twitch channel: {channel}";
			return new PlayResource(dataList[codec].Url, resource);
		}

		private static int SelectStream(List<StreamData> list) => list.FindIndex(s => s.QualityType == StreamQuality.audio_only);

		public string RestoreLink(ResolveContext _, AudioResource resource) => "https://www.twitch.tv/" + resource.ResourceId;

		public void Dispose() { }

#pragma warning disable IDE1006 // Naming Styles
		private class JsonAccessToken
		{
			public string token { get; set; }
			public string sig { get; set; }
			public DateTime expires_at { get; set; }
		}
#pragma warning restore IDE1006 // Naming Styles
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
