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
	using Localization;
	using Newtonsoft.Json;
	using Playlists;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;

	public sealed class YoutubeFactory : IResourceFactory, IPlaylistFactory, IThumbnailFactory
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex IdMatch = new Regex(@"((&|\?)v=|youtu\.be\/)([\w\-_]+)", Util.DefaultRegexConfig);
		private static readonly Regex LinkMatch = new Regex(@"^(https?\:\/\/)?(www\.|m\.)?(youtube\.|youtu\.be)", Util.DefaultRegexConfig);
		private static readonly Regex ListMatch = new Regex(@"(&|\?)list=([\w\-_]+)", Util.DefaultRegexConfig);
		private const string YoutubeProjectId = "AIzaSyBOqG5LUbGSkBfRUoYfUUea37-5xlEyxNs";

		public string FactoryFor => "youtube";

		MatchCertainty IResourceFactory.MatchResource(string uri) =>
			LinkMatch.IsMatch(uri)
				? MatchCertainty.Always
				: IdMatch.IsMatch(uri)
					? MatchCertainty.Probably
					: MatchCertainty.Never;

		MatchCertainty IPlaylistFactory.MatchPlaylist(string uri) => ListMatch.IsMatch(uri) ? MatchCertainty.Probably : MatchCertainty.Never;

		public R<PlayResource, LocalStr> GetResource(string uri)
		{
			Match matchYtId = IdMatch.Match(uri);
			if (!matchYtId.Success)
				return new LocalStr(strings.error_media_failed_to_parse_id);
			return GetResourceById(new AudioResource(matchYtId.Groups[3].Value, null, FactoryFor));
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

			if (dataParse.TryGetValue("url_encoded_fmt_stream_map", out var videoDataUnsplit))
			{
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

			if (dataParse.TryGetValue("adaptive_fmts", out videoDataUnsplit))
			{
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

			// Validation Process

			if (videoTypes.Count <= 0)
				return new LocalStr(strings.error_media_no_stream_extracted);

			int codec = SelectStream(videoTypes);
			if (codec < 0)
				return new LocalStr(strings.error_media_no_stream_extracted);

			var result = ValidateMedia(videoTypes[codec]);
			if (!result.Ok)
				return result.Error;

			var title = dataParse.TryGetValue("title", out var titleArr)
				? titleArr[0]
				: $"<YT - no title : {resource.ResourceTitle}>";

			return new PlayResource(videoTypes[codec].Link, resource.ResourceTitle != null ? resource : resource.WithName(title));
		}

		public string RestoreLink(string id) => "https://youtu.be/" + id;

		private static int SelectStream(List<VideoData> list)
		{
#if DEBUG
			var dbg = new StringBuilder("YT avail codecs: ");
			foreach (var yd in list)
				dbg.Append(yd.Qualitydesciption).Append(" @ ").Append(yd.Codec).Append(", ");
			Log.Trace(dbg.ToString());
#endif

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
			var plist = new Playlist(id);

			string nextToken = null;
			do
			{
				var queryString =
					new Uri("https://www.googleapis.com/youtube/v3/playlistItems"
							+ "?part=contentDetails,snippet"
							+ "&maxResults=50"
							+ "&playlistId=" + id
							+ "&fields=" + Uri.EscapeDataString("items(contentDetails/videoId,snippet/title),nextPageToken")
							+ (nextToken != null ? ("&pageToken=" + nextToken) : string.Empty)
							+ "&key=" + YoutubeProjectId);

				if (!WebWrapper.DownloadString(out string response, queryString))
					return new LocalStr(strings.error_net_unknown);
				var parsed = JsonConvert.DeserializeObject<JsonPlaylistItems>(response);
				var videoItems = parsed.items;
				var itemBuffer = new YoutubePlaylistItem[videoItems.Length];
				for (int i = 0; i < videoItems.Length; i++)
				{
					itemBuffer[i] = new YoutubePlaylistItem(new AudioResource(
						videoItems[i].contentDetails.videoId,
						videoItems[i].snippet.title,
						FactoryFor));
				}

#if getlength
				queryString = new Uri($"https://www.googleapis.com/youtube/v3/videos?id={string.Join(",", itemBuffer.Select(item => item.Resource.ResourceId))}&part=contentDetails&key={data.apiKey}");
				if (!WebWrapper.DownloadString(out response, queryString))
					return "Web response error";
				var parsedTime = (Dictionary<string, object>)Util.Serializer.DeserializeObject(response); // TODO dictionary-object does not work with newtonsoft
				var videoDicts = ((object[])parsedTime["items"]).Cast<Dictionary<string, object>>().ToArray();
				for (int i = 0; i < videoDicts.Length; i++)
					itemBuffer[i].Length = XmlConvert.ToTimeSpan((string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["duration"]));
#endif

				plist.Items.AddRange(itemBuffer);

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

			Log.Debug("youtube-dl succeeded!");
			return new PlayResource(url, resource.WithName(title));
		}

		public static Dictionary<string, List<string>> ParseQueryString(string requestQueryString)
		{
			var rc = new Dictionary<string, List<string>>();
			string[] ar1 = requestQueryString.Split('&', '?');
			foreach (string row in ar1)
			{
				if (string.IsNullOrEmpty(row)) continue;
				int index = row.IndexOf('=');
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
			var parsed = JsonConvert.DeserializeObject<JsonPlaylistItems>(response);

			// default: 120px/ 90px
			// medium : 320px/180px
			// high   : 480px/360px
			var imgurl = new Uri(parsed.items[0].snippet.thumbnails.medium.url);
			return WebWrapper.GetResponseUnsafe(imgurl);
		}

		public void Dispose() { }

#pragma warning disable CS0649, CS0169
		// ReSharper disable ClassNeverInstantiated.Local, InconsistentNaming
		private class JsonPlaylistItems
		{
			public string nextPageToken;
			public JsonItem[] items;

			public class JsonItem
			{
				public JsonContentDetails contentDetails;
				public JsonSnippet snippet;

				public class JsonContentDetails
				{
					public string videoId;
				}

				public class JsonSnippet
				{
					public string title;
					public JsonThumbnailList thumbnails;

					public class JsonThumbnailList
					{
						public JsonThumbnail @default;
						public JsonThumbnail medium;
						public JsonThumbnail high;
						public JsonThumbnail standard;
						public JsonThumbnail maxres;

						public class JsonThumbnail
						{
							public string url;
							public int heigth;
							public int width;
						}
					}
				}
			}
		}
		// ReSharper enable ClassNeverInstantiated.Local, InconsistentNaming
#pragma warning restore CS0649, CS0169
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

	internal class YoutubePlaylistItem : PlaylistItem
	{
		public TimeSpan Length { get; set; }

		public YoutubePlaylistItem(AudioResource resource) : base(resource) { }
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
