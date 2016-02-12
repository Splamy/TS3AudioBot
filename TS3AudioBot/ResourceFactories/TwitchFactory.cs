namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Collections.Generic;
	using System.Text.RegularExpressions;
	using System.Web.Script.Serialization;
	using TS3AudioBot.Helper;

	public sealed class TwitchFactory : IResourceFactory
	{
		private Regex twitchMatch = new Regex(@"^(https?://)?(www\.)?twitch\.tv/(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private Regex m3u8ExtMatch = new Regex(@"#([\w-]+)(:(([\w-]+)=(""[^""]*""|[^,]+),?)*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private JavaScriptSerializer jsonParser;

		public TwitchFactory()
		{
			jsonParser = new JavaScriptSerializer();
		}

		public AudioType FactoryFor => AudioType.Twitch;

		public RResultCode GetResource(string url, out AudioResource resource)
		{
			var match = twitchMatch.Match(url);
			if (!match.Success)
			{
				resource = null;
				return RResultCode.TwitchInvalidUrl;
			}
			return GetResourceById(match.Groups[3].Value, null, out resource);
		}

		public RResultCode GetResourceById(string id, string name, out AudioResource resource)
		{
			var channel = id;

			// request api token
			string jsonResponse;
			if (!WebWrapper.DownloadString(out jsonResponse, new Uri($"http://api.twitch.tv/api/channels/{channel}/access_token")))
			{
				resource = null;
				return RResultCode.NoConnection;
			}

			var jsonDict = (Dictionary<string, object>)jsonParser.DeserializeObject(jsonResponse);

			// request m3u8 file
			var token = Uri.EscapeUriString(jsonDict["token"].ToString());
			var sig = jsonDict["sig"];
			// guaranteed to be random, chosen by fair dice roll.
			var random = 4;
			string m3u8;
			if (!WebWrapper.DownloadString(out m3u8, new Uri($"http://usher.twitch.tv/api/channel/hls/{channel}.m3u8?player=twitchweb&&token={token}&sig={sig}&allow_audio_only=true&allow_source=true&type=any&p={random}")))
			{
				resource = null;
				return RResultCode.NoConnection;
			}

			// parse m3u8 file
			var dataList = new List<StreamData>();
			using (var reader = new System.IO.StringReader(m3u8))
			{
				var header = reader.ReadLine();
				if (string.IsNullOrEmpty(header) || header != "#EXTM3U")
				{
					resource = null;
					return RResultCode.TwitchMalformedM3u8File;
				}

				while (true)
				{
					var blockInfo = reader.ReadLine();
					if (string.IsNullOrEmpty(blockInfo))
						break;

					var match = m3u8ExtMatch.Match(blockInfo);
					if (!match.Success)
						continue;

					switch (match.Groups[1].Value)
					{
					case "EXT-X-TWITCH-INFO": break; // Ignore twitch info line
					case "EXT-X-MEDIA":
						string streamInfo = reader.ReadLine();
						Match infoMatch;
						if (string.IsNullOrEmpty(streamInfo) ||
							 !(infoMatch = m3u8ExtMatch.Match(streamInfo)).Success ||
							 infoMatch.Groups[1].Value != "EXT-X-STREAM-INF")
						{
							resource = null;
							return RResultCode.TwitchMalformedM3u8File;
						}

						var streamData = new StreamData();
						// #EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=128000,CODECS="mp4a.40.2",VIDEO="audio_only"
						for (int i = 0; i < infoMatch.Groups[3].Captures.Count; i++)
						{
							string key = infoMatch.Groups[4].Captures[i].Value.ToUpper();
							string value = infoMatch.Groups[5].Captures[i].Value;

							switch (key)
							{
							case "BANDWIDTH": streamData.Bandwidth = int.Parse(value); break;
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

			resource = new TwitchResource(channel, name ?? $"Twitch channel: {channel}", dataList);
			return dataList.Count > 0 ? RResultCode.Success : RResultCode.TwitchNoStreamsExtracted;
		}

		public bool MatchLink(string uri) => twitchMatch.IsMatch(uri);

		public void PostProcess(PlayData data, out bool abortPlay)
		{
			var twResource = (TwitchResource)data.Resource;
			// selecting the best stream
			int autoselectIndex = twResource.AvailableStreams.FindIndex(s => s.QualityType == StreamQuality.audio_only);
			if (autoselectIndex != -1)
			{
				twResource.Selected = autoselectIndex;
				abortPlay = false;
				return;
			}

			// TODO add response like youtube
			data.Session.Write("The stream has no audio_only version.");
			abortPlay = true;
		}

		public string RestoreLink(string id) => "http://www.twitch.tv/" + id;

		public void Dispose() { }
	}

	public sealed class StreamData
	{
		public StreamQuality QualityType;
		public int Bandwidth;
		public string Codec;
		public string Url;
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

	public sealed class TwitchResource : AudioResource
	{
		public List<StreamData> AvailableStreams { get; private set; }
		public int Selected { get; set; }

		public override AudioType AudioType => AudioType.Twitch;

		public TwitchResource(string channel, string name, List<StreamData> availableStreams) : base(channel, name)
		{
			AvailableStreams = availableStreams;
		}

		public override string Play()
		{
			if (Selected < 0 && Selected >= AvailableStreams.Count)
				return null;
			Log.Write(Log.Level.Debug, "YT Playing: {0}", AvailableStreams[Selected]);
			return AvailableStreams[Selected].Url;
		}
	}
}
