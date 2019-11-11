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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;

namespace TS3AudioBot.ResourceFactories
{
	public sealed class YoutubeResolver : IResourceResolver, IPlaylistResolver, IThumbnailResolver, ISearchResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex IdMatch = new Regex(@"((&|\?)v=|youtu\.be\/)([\w\-_]{11})", Util.DefaultRegexConfig);
		private static readonly Regex LinkMatch = new Regex(@"^(https?\:\/\/)?(www\.|m\.)?(youtube\.|youtu\.be)", Util.DefaultRegexConfig);
		private static readonly Regex ListMatch = new Regex(@"(&|\?)list=([\w\-_]+)", Util.DefaultRegexConfig);
		private static readonly Regex StreamCodecMatch = new Regex(@"CODECS=""([^""]*)""", Util.DefaultRegexConfig);
		private static readonly Regex StreamBitrateMatch = new Regex(@"BANDWIDTH=(\d+)", Util.DefaultRegexConfig);
		private const string YoutubeProjectId = "AIzaSyBOqG5LUbGSkBfRUoYfUUea37-5xlEyxNs";

		public string ResolverFor => "youtube";

		MatchCertainty IResourceResolver.MatchResource(string uri) =>
			LinkMatch.IsMatch(uri) || IdMatch.IsMatch(uri)
				? MatchCertainty.Always
				: MatchCertainty.Never;

		MatchCertainty IPlaylistResolver.MatchPlaylist(string uri) => ListMatch.IsMatch(uri) ? MatchCertainty.Always : MatchCertainty.Never;

		public R<PlayResource, LocalStr> GetResource(string uri)
		{
			Match matchYtId = IdMatch.Match(uri);
			if (!matchYtId.Success)
				return new LocalStr(strings.error_media_failed_to_parse_id);
			return GetResourceById(new AudioResource(matchYtId.Groups[3].Value, null, ResolverFor));
		}

		public R<PlayResource, LocalStr> GetResourceById(AudioResource resource)
		{
			var result = ResolveResourceInternal(resource);
			if (result.Ok)
				return result;

			return YoutubeDlWrapped(resource);
		}

		private R<PlayResource, LocalStr> ResolveResourceInternal(AudioResource resource)
		{
			if (!WebWrapper.DownloadString(out string resulthtml, new Uri($"https://www.youtube.com/get_video_info?video_id={resource.ResourceId}")))
				return new LocalStr(strings.error_net_no_connection);

			var videoTypes = new List<VideoData>();
			var dataParse = ParseQueryString(resulthtml);

			if (dataParse.TryGetValue("player_response", out var playerData))
			{
				var parsed = JsonConvert.DeserializeObject<JsonPlayerResponse>(playerData[0]);
				Log.Debug("Extracted data: {@playerData}", parsed);

				if (parsed?.videoDetails != null)
				{
					resource.ResourceTitle = resource.ResourceTitle ?? parsed.videoDetails.title;

					bool isLive = parsed.videoDetails.isLive ?? false;
					if (isLive && parsed.streamingData?.hlsManifestUrl != null)
					{
						return ParseLiveData(resource, parsed);
					}
					else if (isLive)
					{
						Log.Warn("Live stream without hls stream data");
					}

					ParsePlayerData(parsed, videoTypes);
				}
			}

			if (dataParse.TryGetValue("url_encoded_fmt_stream_map", out var videoDataUnsplit))
				ParseEncodedFmt(videoDataUnsplit, videoTypes);

			if (dataParse.TryGetValue("adaptive_fmts", out videoDataUnsplit))
				ParseAdaptiveFmt(videoDataUnsplit, videoTypes);

			// Validation Process

			if (videoTypes.Count <= 0)
				return new LocalStr(strings.error_media_no_stream_extracted);

			int codec = SelectStream(videoTypes);
			if (codec < 0)
				return new LocalStr(strings.error_media_no_stream_extracted);

			var result = ValidateMedia(videoTypes[codec]);
			if (!result.Ok)
				return result.Error;

			resource.ResourceTitle = resource.ResourceTitle ?? $"<YT - no title : {resource.ResourceId}>";

			return new PlayResource(videoTypes[codec].Link, resource);
		}

		private static R<PlayResource, LocalStr> ParseLiveData(AudioResource resource, JsonPlayerResponse parsed)
		{
			var webListResponse = WebWrapper.GetResponse(new Uri(parsed.streamingData.hlsManifestUrl), response =>
			{
				return AudioTags.M3uReader.TryGetData(response.GetResponseStream()).OkOr(null);
			});
			if (webListResponse.Ok)
			{
				const string AacHe = "mp4a.40.5";
				const string AacLc = "mp4a.40.2";

				var webList = webListResponse.Value;
				var streamPref = from item in webList
								 let codecs = item.StreamMeta != null ? StreamCodecMatch.Match(item.StreamMeta).Groups[1].Value : ""
								 let codecPref = codecs.Contains(AacLc) ? 0
									 : codecs.Contains(AacHe) ? 1
									 : 2
								 let bitrate = item.StreamMeta != null ? int.Parse(StreamBitrateMatch.Match(item.StreamMeta).Groups[1].Value) : int.MaxValue
								 orderby codecPref, bitrate ascending
								 select item;
				var streamSelect = streamPref.FirstOrDefault();
				if (streamSelect != null)
				{
					if (resource.ResourceTitle == null)
						resource.ResourceTitle = parsed.videoDetails.title;
					return new PlayResource(streamSelect.TrackUrl, resource);
				}
			}
			return new LocalStr(strings.error_media_no_stream_extracted);
		}

		private static void ParsePlayerData(JsonPlayerResponse data, List<VideoData> videoTypes)
		{
			// TODO
		}

		private static void ParseEncodedFmt(List<string> videoDataUnsplit, List<VideoData> videoTypes)
		{
			if (videoDataUnsplit.Count == 0)
				return;
			string[] videoData = videoDataUnsplit[0].Split(',');

			foreach (string vdat in videoData)
			{
				var videoparse = ParseQueryString(vdat);

				if (!videoparse.TryGetValue("url", out var vLink))
					continue;

				if (!videoparse.TryGetValue("type", out var vType))
					continue;

				if (!videoparse.TryGetValue("quality", out var vQuality))
					continue;

				var vt = new VideoData()
				{
					Link = vLink[0],
					Codec = GetCodec(vType[0]),
					Qualitydesciption = vQuality[0]
				};
				videoTypes.Add(vt);
			}
		}

		private static void ParseAdaptiveFmt(List<string> videoDataUnsplit, List<VideoData> videoTypes)
		{
			if (videoDataUnsplit.Count == 0)
				return;

			string[] videoData = videoDataUnsplit[0].Split(',');

			foreach (string vdat in videoData)
			{
				var videoparse = ParseQueryString(vdat);

				if (!videoparse.TryGetValue("type", out var vTypeArr))
					continue;
				var vType = vTypeArr[0];

				bool audioOnly = false;
				if (vType.StartsWith("video/", StringComparison.Ordinal))
					continue;
				else if (vType.StartsWith("audio/", StringComparison.Ordinal))
					audioOnly = true;

				if (!videoparse.TryGetValue("url", out var vLink))
					continue;

				var vt = new VideoData()
				{
					Codec = GetCodec(vType),
					Qualitydesciption = vType,
					Link = vLink[0]
				};
				if (audioOnly)
					vt.AudioOnly = true;
				else
					vt.VideoOnly = true;
				videoTypes.Add(vt);
			}
		}

		public string RestoreLink(AudioResource resource) => "https://youtu.be/" + resource.ResourceId;

		private static int SelectStream(List<VideoData> list)
		{
			if (Log.IsTraceEnabled)
			{
				var dbg = new System.Text.StringBuilder("YT avail codecs: ");
				foreach (var yd in list)
					dbg.Append(yd.Qualitydesciption).Append(" @ ").Append(yd.Codec).Append(", ");
				Log.Trace("{0}", dbg);
			}

			int autoselectIndex = list.FindIndex(t => t.Codec == VideoCodec.M4A);
			if (autoselectIndex == -1)
				autoselectIndex = list.FindIndex(t => t.AudioOnly);
			if (autoselectIndex == -1)
				autoselectIndex = list.FindIndex(t => !t.VideoOnly);

			return autoselectIndex;
		}

		private static E<LocalStr> ValidateMedia(VideoData media) => WebWrapper.GetResponse(new Uri(media.Link), TimeSpan.FromSeconds(3));

		private static VideoCodec GetCodec(string type)
		{
			string lowtype = type.ToLowerInvariant();
			bool audioOnly = false;
			string codecSubStr;
			if (lowtype.StartsWith("video/", StringComparison.Ordinal))
			{
				codecSubStr = lowtype.Substring("video/".Length);
			}
			else if (lowtype.StartsWith("audio/", StringComparison.Ordinal))
			{
				codecSubStr = lowtype.Substring("audio/".Length);
				audioOnly = true;
			}
			else return VideoCodec.Unknown;

			int codecEnd;
			var extractedCodec = (codecEnd = codecSubStr.IndexOf(';')) >= 0 ? codecSubStr.Substring(0, codecEnd) : codecSubStr;

			switch (extractedCodec)
			{
			case "mp4":
				if (audioOnly)
					return VideoCodec.M4A;
				return VideoCodec.Mp4;
			case "x-flv":
				return VideoCodec.Flv;
			case "3gpp":
				return VideoCodec.ThreeGp;
			case "webm":
				return VideoCodec.Webm;
			default:
				return VideoCodec.Unknown;
			}
		}

		public R<Playlist, LocalStr> GetPlaylist(string url)
		{
			Match matchYtId = ListMatch.Match(url);
			if (!matchYtId.Success)
				return new LocalStr(strings.error_media_failed_to_parse_id);

			string id = matchYtId.Groups[2].Value;
			var plist = new Playlist().SetTitle(id); // TODO TITLE !!!!!!!!!

			string nextToken = null;
			do
			{
				var queryString =
					new Uri("https://www.googleapis.com/youtube/v3/playlistItems"
						+ "?part=contentDetails,snippet"
						+ "&fields=" + Uri.EscapeDataString("items(contentDetails/videoId,snippet/title),nextPageToken")
						+ "&maxResults=50"
						+ "&playlistId=" + id
						+ (nextToken != null ? ("&pageToken=" + nextToken) : string.Empty)
						+ "&key=" + YoutubeProjectId);

				if (!WebWrapper.DownloadString(out string response, queryString))
					return new LocalStr(strings.error_net_unknown);
				var parsed = JsonConvert.DeserializeObject<JsonVideoListResponse>(response);
				var videoItems = parsed.items;
				if (!plist.AddRange(
					videoItems.Select(item =>
						new PlaylistItem(
							new AudioResource(
								item.contentDetails.videoId,
								item.snippet.title,
								ResolverFor
							)
						)
					)
				)) break;

				nextToken = parsed.nextPageToken;
			} while (nextToken != null);

			return plist;
		}

		private static R<PlayResource, LocalStr> YoutubeDlWrapped(AudioResource resource)
		{
			Log.Debug("Falling back to youtube-dl!");

			var result = YoutubeDlHelper.FindAndRunYoutubeDl(resource.ResourceId);
			if (!result.Ok)
				return result.Error;

			var response = result.Value;
			var title = response.title;
			var urlOptions = response.links;

			string url = null;
			if (urlOptions.Count == 1)
			{
				url = urlOptions[0];
			}
			else if (urlOptions.Count >= 1)
			{
				Uri[] uriList = urlOptions.Select(s => new Uri(s)).ToArray();
				Uri bestMatch = uriList
					.FirstOrDefault(u => ParseQueryString(u.Query).TryGetValue("mime", out var mimes)
										 && mimes.Any(x => x.StartsWith("audio", StringComparison.OrdinalIgnoreCase)));
				url = (bestMatch ?? uriList[0]).OriginalString;
			}

			if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url))
				return new LocalStr(strings.error_ytdl_empty_response);

			resource.ResourceTitle = title;

			Log.Debug("youtube-dl succeeded!");
			return new PlayResource(url, resource);
		}

		public static Dictionary<string, List<string>> ParseQueryString(string requestQueryString)
		{
			var rc = new Dictionary<string, List<string>>();
			string[] ar1 = requestQueryString.Split('&', '?');
			foreach (string row in ar1)
			{
				if (string.IsNullOrEmpty(row)) continue;
				int index = row.IndexOf('=');
				if (index < 0) continue;
				var param = Uri.UnescapeDataString(row.Substring(0, index).Replace('+', ' '));
				if (!rc.TryGetValue(param, out var list))
				{
					list = new List<string>();
					rc[param] = list;
				}
				list.Add(Uri.UnescapeDataString(row.Substring(index + 1).Replace('+', ' ')));
			}
			return rc;
		}

		public R<Stream, LocalStr> GetThumbnail(PlayResource playResource)
		{
			if (!WebWrapper.DownloadString(out string response,
				new Uri($"https://www.googleapis.com/youtube/v3/videos?part=snippet&id={playResource.BaseData.ResourceId}&key={YoutubeProjectId}")))
				return new LocalStr(strings.error_net_no_connection);
			var parsed = JsonConvert.DeserializeObject<JsonVideoListResponse>(response);

			// default: 120px/ 90px
			// medium : 320px/180px
			// high   : 480px/360px
			var imgurl = new Uri(parsed.items[0].snippet.thumbnails.medium.url);
			return WebWrapper.GetResponseUnsafe(imgurl);
		}

		public R<IList<AudioResource>, LocalStr> Search(string keyword)
		{
			// TODO checkout https://developers.google.com/youtube/v3/docs/search/list ->relatedToVideoId for auto radio play
			const int maxResults = 10;
			if (!WebWrapper.DownloadString(out string response,
				new Uri("https://www.googleapis.com/youtube/v3/search"
					+ "?part=snippet"
					+ "&fields=" + Uri.EscapeDataString("items(id/videoId,snippet(channelTitle,title))")
					+ "&type=video"
					+ "&safeSearch=none"
					+ "&q=" + Uri.EscapeDataString(keyword)
					+ "&maxResults=" + maxResults
					+ "&key=" + YoutubeProjectId)))
				return new LocalStr(strings.error_net_no_connection);

			var parsed = JsonConvert.DeserializeObject<JsonSearchListResponse>(response);
			return parsed.items.Select(x => new AudioResource(
				x.id.videoId,
				x.snippet.title,
				ResolverFor)).ToArray();
		}

		public void Dispose() { }

#pragma warning disable CS0649, CS0169, IDE1006
		// ReSharper disable ClassNeverInstantiated.Local, InconsistentNaming
		private class JsonVideoListResponse // # youtube#videoListResponse
		{
			public string nextPageToken { get; set; }
			public JsonVideo[] items { get; set; }
		}
		private class JsonVideo // youtube#video
		{
			public JsonContentDetails contentDetails { get; set; }
			public JsonSnippet snippet { get; set; }
		}
		private class JsonSearchListResponse // youtube#searchListResponse
		{
			public JsonSearchResult[] items { get; set; }
		}
		private class JsonSearchResult // youtube#searchResult
		{
			public JsonContentDetails id { get; set; }
			public JsonSnippet snippet { get; set; }
		}
		private class JsonContentDetails
		{
			public string videoId { get; set; }
		}
		private class JsonSnippet
		{
			public string title { get; set; }
			public JsonThumbnailList thumbnails { get; set; }
		}
		private class JsonThumbnailList
		{
			public JsonThumbnail @default { get; set; }
			public JsonThumbnail medium { get; set; }
			public JsonThumbnail high { get; set; }
			public JsonThumbnail standard { get; set; }
			public JsonThumbnail maxres { get; set; }
		}
		private class JsonThumbnail
		{
			public string url { get; set; }
			public int heigth { get; set; }
			public int width { get; set; }
		}
		// Custom json
		private class JsonPlayerResponse
		{
			public JsonStreamingData streamingData { get; set; }
			public JsonVideoDetails videoDetails { get; set; }
		}
		private class JsonStreamingData
		{
			public string dashManifestUrl { get; set; }
			public string hlsManifestUrl { get; set; }
		}
		private class JsonVideoDetails
		{
			public string title { get; set; }
			public bool? isLive { get; set; }
			public bool useCipher { get; set; }
			public bool isLiveContent { get; set; }
		}
		private class JsonPlayFormat
		{
			public string mimeType { get; set; }
			public int bitrate { get; set; }
			public string cipher { get; set; }
			public string url { get; set; }
		}
		// ReSharper enable ClassNeverInstantiated.Local, InconsistentNaming
#pragma warning restore CS0649, CS0169, IDE1006
	}

	public sealed class VideoData
	{
		public string Link { get; set; }
		public string Qualitydesciption { get; set; }
		public VideoCodec Codec { get; set; }
		public bool AudioOnly { get; set; }
		public bool VideoOnly { get; set; }

		public override string ToString() => $"{Qualitydesciption} @ {Codec} - {Link}";
	}

	public enum VideoCodec
	{
		Unknown,
		Mp4,
		M4A,
		Webm,
		Flv,
		ThreeGp,
	}
}
