using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;

namespace TS3AudioBot
{
	class YoutubeFramework : IDisposable
	{
		private WebClient wc;

		public YoutubeRessource LoadedRessource { get; protected set; }

		public YoutubeFramework()
		{
			wc = new WebClient();
			LoadedRessource = null;
		}

		public ResultCode ExtractURL(string ytLink, bool filterOutInvalid = true)
		{
			string resulthtml = string.Empty;
			Match matchYtId = Regex.Match(ytLink, @"(&|\?)v=([a-zA-Z0-9\-_]+)");
			if (!matchYtId.Success)
				return ResultCode.YtIdNotFound;
			string ytID = matchYtId.Groups[2].Value;

			try
			{
				resulthtml = wc.DownloadString(string.Format("http://www.youtube.com/get_video_info?video_id={0}&el=info", ytID));
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Warning, "Youtube downloadreqest failed: " + ex.Message);
				return ResultCode.NoYtConnection;
			}

			List<VideoType> videoTypes = new List<VideoType>();
			NameValueCollection dataParse = HttpUtility.ParseQueryString(resulthtml);

			string vTitle = dataParse["title"];
			if (vTitle == null)
				vTitle = string.Empty;

			string videoDataUnsplit = dataParse["url_encoded_fmt_stream_map"];
			if (videoDataUnsplit == null)
				return ResultCode.NoFMT;
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

				VideoType vt = new VideoType();
				vt.link = vLink;
				vt.codec = GetCodec(vType);
				vt.qualitydesciption = vQuality;
				videoTypes.Add(vt);
			}

			videoDataUnsplit = dataParse["adaptive_fmts"];
			if (videoDataUnsplit == null)
				return ResultCode.NoFMTS;
			videoData = videoDataUnsplit.Split(',');

			foreach (string vdat in videoData)
			{
				NameValueCollection videoparse = HttpUtility.ParseQueryString(vdat);

				string vType = videoparse["type"];
				if (vType == null)
					continue;

				bool audioOnly = false;
				if (vType.StartsWith("video/") && filterOutInvalid)
					continue;
				else if (vType.StartsWith("audio/"))
					audioOnly = true;

				string vLink = videoparse["url"];
				if (vLink == null)
					continue;

				VideoType vt = new VideoType();
				vt.codec = GetCodec(vType);
				vt.qualitydesciption = vType;
				vt.link = vLink;
				if (audioOnly)
					vt.audioOnly = true;
				else
					vt.videoOnly = true;
				videoTypes.Add(vt);
			}

			LoadedRessource = new YoutubeRessource(ytID, vTitle, videoTypes.AsReadOnly());
			if (LoadedRessource.AvailableTypes.Count > 0)
				return ResultCode.Success;
			else
				return ResultCode.NoVideosExtracted;
		}

		public ResultCode ExtractPlayList()
		{
			//https://gdata.youtube.com/feeds/api/playlists/UU4L4Vac0HBJ8-f3LBFllMsg?alt=json
			//https://gdata.youtube.com/feeds/api/playlists/UU4L4Vac0HBJ8-f3LBFllMsg?alt=json&start-index=1&max-results=1

			//totalResults":{"\$t":(\d+)}

			//"url"\w*:\w*"(https:\/\/www\.youtube\.com\/watch\?v=[a-zA-Z0-9\-_]+&)
			return ResultCode.Success;
		}

		private VideoCodec GetCodec(string type)
		{
			string lowtype = type.ToLower();
			bool audioOnly = false;
			string codecSubStr;
			if (lowtype.StartsWith("video/"))
				codecSubStr = lowtype.Substring("video/".Length);
			else if (lowtype.StartsWith("audio/"))
			{
				codecSubStr = lowtype.Substring("audio/".Length);
				audioOnly = true;
			}
			else
				return VideoCodec.Unknown;

			string extractedCodec;
			int codecEnd;
			if ((codecEnd = codecSubStr.IndexOf(';')) >= 0)
				extractedCodec = codecSubStr.Substring(0, codecEnd);
			else
				extractedCodec = codecSubStr;

			switch (extractedCodec)
			{
			case "mp4":
				if (audioOnly)
					return VideoCodec.M4A;
				return VideoCodec.MP4;
			case "x-flv":
				return VideoCodec.FLV;
			case "3gpp":
				return VideoCodec.ThreeGP;
			case "webm":
				return VideoCodec.WEBM;
			default:
				return VideoCodec.Unknown;
			}
		}

		public void Dispose()
		{
			if (wc != null)
			{
				wc.Dispose();
				wc = null;
			}
		}
	}

	class VideoType
	{
		public string link;
		public string qualitydesciption;
		public VideoCodec codec;
		public bool audioOnly = false;
		public bool videoOnly = false;

		public override string ToString()
		{
			return qualitydesciption + " @ " + codec + " - " + link;
		}
	}

	class YoutubeRessource : AudioRessource
	{
		public string YoutubeName { get; protected set; }

		public IReadOnlyList<VideoType> AvailableTypes { get; protected set; }

		public int Selected { get; set; }

		public override AudioType AudioType { get { return AudioType.Youtube; } }

		public YoutubeRessource(string link, string youtubeName, IReadOnlyList<VideoType> availableTypes)
			: base(link)
		{
			YoutubeName = youtubeName;
			AvailableTypes = availableTypes;
			Selected = 0;
		}

		public override bool Play(Action<string> setMedia)
		{
			if (Selected < 0 && Selected >= AvailableTypes.Count)
				return false;
			setMedia(AvailableTypes[1].link);
			return true;
		}
	}

	enum VideoCodec
	{
		Unknown,
		MP4,
		M4A,
		WEBM,
		FLV,
		ThreeGP,
	}

	enum ResultCode
	{
		Success,
		YtIdNotFound,
		NoYtConnection,
		NoVideosExtracted,
		NoFMT,
		NoFMTS,
	}
}
