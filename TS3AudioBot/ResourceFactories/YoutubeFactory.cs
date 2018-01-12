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
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Drawing;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Web;
	using Newtonsoft.Json;

	public sealed class YoutubeFactory : IResourceFactory, IPlaylistFactory, IThumbnailFactory
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex IdMatch = new Regex(@"((&|\?)v=|youtu\.be\/)([\w\-_]+)", Util.DefaultRegexConfig);
		private static readonly Regex LinkMatch = new Regex(@"^(https?\:\/\/)?(www\.|m\.)?(youtube\.|youtu\.be)", Util.DefaultRegexConfig);
		private static readonly Regex ListMatch = new Regex(@"(&|\?)list=([\w\-_]+)", Util.DefaultRegexConfig);

		private readonly YoutubeFactoryData data;

		public YoutubeFactory(YoutubeFactoryData yfd)
		{
			data = yfd;
		}

		public string FactoryFor => "youtube";

		MatchCertainty IResourceFactory.MatchResource(string link) =>
			LinkMatch.IsMatch(link)
				? MatchCertainty.Always
				: IdMatch.IsMatch(link)
					? MatchCertainty.Probably
					: MatchCertainty.Never;

		MatchCertainty IPlaylistFactory.MatchPlaylist(string link) => ListMatch.IsMatch(link) ? MatchCertainty.Probably : MatchCertainty.Never;

		public R<PlayResource> GetResource(string ytLink)
		{
			Match matchYtId = IdMatch.Match(ytLink);
			if (!matchYtId.Success)
				return "The youtube id could not get parsed.";
			return GetResourceById(new AudioResource(matchYtId.Groups[3].Value, null, FactoryFor));
		}

		public R<PlayResource> GetResourceById(AudioResource resource)
		{
			var result = ResolveResourceInternal(resource);
			if (result.Ok)
				return result;
			
			return YoutubeDlWrapped(resource);
		}

		private R<PlayResource> ResolveResourceInternal(AudioResource resource)
		{
			if (!WebWrapper.DownloadString(out string resulthtml, new Uri($"http://www.youtube.com/get_video_info?video_id={resource.ResourceId}&el=info")))
				return "No connection to the youtube api could be established";

			var videoTypes = new List<VideoData>();
			NameValueCollection dataParse = HttpUtility.ParseQueryString(resulthtml);

			string videoDataUnsplit = dataParse["url_encoded_fmt_stream_map"];
			if (videoDataUnsplit != null)
			{
				string[] videoData = videoDataUnsplit.Split(',');

				foreach (string vdat in videoData)
				{
					NameValueCollection videoparse = HttpUtility.ParseQueryString(vdat);

					string vLink = videoparse["url"];
					if (vLink == null)
						continue;

					string vType = videoparse["type"];
					if (vType == null)
						continue;

					string vQuality = videoparse["quality"];
					if (vQuality == null)
						continue;

					var vt = new VideoData()
					{
						Link = vLink,
						Codec = GetCodec(vType),
						Qualitydesciption = vQuality
					};
					videoTypes.Add(vt);
				}
			}

			videoDataUnsplit = dataParse["adaptive_fmts"];
			if (videoDataUnsplit != null)
			{
				string[] videoData = videoDataUnsplit.Split(',');

				foreach (string vdat in videoData)
				{
					NameValueCollection videoparse = HttpUtility.ParseQueryString(vdat);

					string vType = videoparse["type"];
					if (vType == null)
						continue;

					bool audioOnly = false;
					if (vType.StartsWith("video/", StringComparison.Ordinal))
						continue;
					else if (vType.StartsWith("audio/", StringComparison.Ordinal))
						audioOnly = true;

					string vLink = videoparse["url"];
					if (vLink == null)
						continue;

					var vt = new VideoData()
					{
						Codec = GetCodec(vType),
						Qualitydesciption = vType,
						Link = vLink
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
				return "No video streams extracted.";

			int codec = SelectStream(videoTypes);
			if (codec < 0)
				return "No playable codec found";

			var result = ValidateMedia(videoTypes[codec]);
			if (!result.Ok)
				return result.Error;

			return new PlayResource(videoTypes[codec].Link, resource.ResourceTitle != null ? resource : resource.WithName(dataParse["title"] ?? $"<YT - no title : {resource.ResourceTitle}>"));
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

		private static R ValidateMedia(VideoData media)
		{
			var vcode = WebWrapper.GetResponse(new Uri(media.Link), TimeSpan.FromSeconds(3));

			switch (vcode)
			{
			case ValidateCode.Ok: return R.OkR;
			case ValidateCode.Restricted: return "The video cannot be played due to youtube restrictions.";
			case ValidateCode.Timeout: return "No connection could be established to youtube. Please try again later.";
			case ValidateCode.UnknownError: return "Unknown error occoured";
			default: throw new InvalidOperationException();
			}
		}

		private static VideoCodec GetCodec(string type)
		{
			string lowtype = type.ToLower(CultureInfo.InvariantCulture);
			bool audioOnly = false;
			string codecSubStr;
			if (lowtype.StartsWith("video/", StringComparison.Ordinal))
				codecSubStr = lowtype.Substring("video/".Length);
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

		public R<Playlist> GetPlaylist(string url)
		{
			Match matchYtId = ListMatch.Match(url);
			if (!matchYtId.Success)
				return "Could not extract a playlist id";

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
							+ "&key=" + data.ApiKey);

				if (!WebWrapper.DownloadString(out string response, queryString))
					return "Web response error";
				var parsed = JsonConvert.DeserializeObject<JsonPlaylistItems>(response);
				var videoItems = parsed.items;
				YoutubePlaylistItem[] itemBuffer = new YoutubePlaylistItem[videoItems.Length];
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

				plist.AddRange(itemBuffer);

				nextToken = parsed.nextPageToken;
			} while (nextToken != null);

			return plist;
		}
		
		private static R<PlayResource> YoutubeDlWrapped(AudioResource resource)
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
					.FirstOrDefault(u => HttpUtility.ParseQueryString(u.Query)
						.GetValues("mime")?
						.Any(x => x.StartsWith("audio", StringComparison.OrdinalIgnoreCase)) ?? false);
				url = (bestMatch ?? uriList[0]).OriginalString;
			}

			if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url))
				return "No youtube-dl response";

			Log.Debug("youtube-dl succeeded!");
			return new PlayResource(url, resource.WithName(title));
		}

		public R<Image> GetThumbnail(PlayResource playResource)
		{
			if (!WebWrapper.DownloadString(out string response,
				new Uri($"https://www.googleapis.com/youtube/v3/videos?part=snippet&id={playResource.BaseData.ResourceId}&key={data.ApiKey}")))
				return "No connection";
			var parsed = JsonConvert.DeserializeObject<JsonPlaylistItems>(response);

			// default: 120px/ 90px
			// medium : 320px/180px
			// high   : 480px/360px
			var imgurl = new Uri(parsed.items[0].snippet.thumbnails.medium.url);
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

#pragma warning disable CS0649
	public class YoutubeFactoryData : ConfigData
	{
		[Info("A youtube apiv3 'Browser' type key", "AIzaSyBOqG5LUbGSkBfRUoYfUUea37-5xlEyxNs")]
		public string ApiKey { get; set; }
		[Info("Path to the youtube-dl binary or local git repository", "")]
		public string YoutubedlPath { get; set; }
	}
#pragma warning restore CS0649

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
