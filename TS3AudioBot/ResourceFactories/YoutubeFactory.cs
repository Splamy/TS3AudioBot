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
	using System.Collections.Specialized;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Web;
	using Helper;

	public sealed class YoutubeFactory : IResourceFactory
	{
		private Regex idMatch = new Regex(@"((&|\?)v=|youtu\.be\/)([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private Regex linkMatch = new Regex(@"^(https?\:\/\/)?(www\.|m\.)?(youtube\.|youtu\.be)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		public AudioType FactoryFor => AudioType.Youtube;

		public YoutubeFactory() { }

		public bool MatchLink(string link) => linkMatch.IsMatch(link);

		public R<PlayResource> GetResource(string ytLink)
		{
			Match matchYtId = idMatch.Match(ytLink);
			if (!matchYtId.Success)
				return RResultCode.YtIdNotFound.ToString();
			return GetResourceById(new AudioResource(matchYtId.Groups[3].Value, null, AudioType.Youtube));
		}

		public R<PlayResource> GetResourceById(AudioResource resource)
		{
			string resulthtml;
			if (!WebWrapper.DownloadString(out resulthtml, new Uri($"http://www.youtube.com/get_video_info?video_id={resource.ResourceId}&el=info")))
				return RResultCode.NoConnection.ToString();

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

					var vt = new VideoData();
					vt.link = vLink;
					vt.codec = GetCodec(vType);
					vt.qualitydesciption = vQuality;
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
					if (vType.StartsWith("video/"))
						continue;
					else if (vType.StartsWith("audio/"))
						audioOnly = true;

					string vLink = videoparse["url"];
					if (vLink == null)
						continue;

					var vt = new VideoData();
					vt.codec = GetCodec(vType);
					vt.qualitydesciption = vType;
					vt.link = vLink;
					if (audioOnly)
						vt.audioOnly = true;
					else
						vt.videoOnly = true;
					videoTypes.Add(vt);
				}
			}

			// Validation Process

			if (videoTypes.Count <= 0)
				return RResultCode.YtNoVideosExtracted.ToString();

			int codec = SelectStream(videoTypes);
			if (codec < 0)
				return "No playable codec found";

			var result = ValidateMedia(videoTypes[codec]);
			if (!result)
				return result.Message;

			return new PlayResource(videoTypes[codec].link, resource.ResourceTitle != null ? resource : resource.WithName(dataParse["title"] ?? $"<YT - no title : {resource.ResourceTitle}>"));
		}

		public string RestoreLink(string id) => "https://youtu.be/" + id;

		private int SelectStream(List<VideoData> list)
		{
#if DEBUG
			StringBuilder dbg = new StringBuilder("YT avail codecs: ");
			foreach (var yd in list)
				dbg.Append(yd.qualitydesciption).Append(" @ ").Append(yd.codec).Append(", ");
			Log.Write(Log.Level.Debug, dbg.ToString());
#endif

			int autoselectIndex = list.FindIndex(t => t.codec == VideoCodec.M4A);
			if (autoselectIndex == -1)
				autoselectIndex = list.FindIndex(t => t.audioOnly);
			if (autoselectIndex == -1)
				autoselectIndex = list.FindIndex(t => !t.videoOnly);

			return autoselectIndex;
		}

		private static R ValidateMedia(VideoData media)
		{
			var vcode = WebWrapper.GetResponse(new Uri(media.link), TimeSpan.FromSeconds(1));

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
			else return VideoCodec.Unknown;

			string extractedCodec;
			int codecEnd;
			extractedCodec = (codecEnd = codecSubStr.IndexOf(';')) >= 0 ? codecSubStr.Substring(0, codecEnd) : codecSubStr;

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

		public void Dispose() { }
	}

	public sealed class VideoData
	{
		public string link;
		public string qualitydesciption;
		public VideoCodec codec;
		public bool audioOnly = false;
		public bool videoOnly = false;

		public override string ToString() => $"{qualitydesciption} @ {codec} - {link}";
	}

	public enum VideoCodec
	{
		Unknown,
		MP4,
		M4A,
		WEBM,
		FLV,
		ThreeGP,
	}
}
