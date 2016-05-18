namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Web;
	using Helper;
	using CommandSystem;

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
			return GetResourceById(matchYtId.Groups[3].Value, null);
		}

		public R<PlayResource> GetResourceById(string ytID, string name)
		{
			string resulthtml;
			if (!WebWrapper.DownloadString(out resulthtml, new Uri($"http://www.youtube.com/get_video_info?video_id={ytID}&el=info")))
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

			string finalName = name ?? dataParse["title"] ?? $"<YT - no title : {ytID}>";
			if (videoTypes.Count > 0)
				return new YoutubeResource(videoTypes, new AudioResource(ytID, finalName, AudioType.Youtube));
			else
				return RResultCode.YtNoVideosExtracted.ToString();
		}

		public string RestoreLink(string id) => "https://youtu.be/" + id;

		public R<PlayResource> PostProcess(PlayData data)
		{
			YoutubeResource ytResource = (YoutubeResource)data.PlayResource;

#if DEBUG
			StringBuilder dbg = new StringBuilder("YT avail codecs: ");
			foreach (var yd in ytResource.AvailableTypes)
				dbg.Append(yd.qualitydesciption).Append(" @ ").Append(yd.codec).Append(", ");
			Log.Write(Log.Level.Debug, dbg.ToString());
#endif

			var availList = ytResource.AvailableTypes;
			int autoselectIndex = availList.FindIndex(t => t.codec == VideoCodec.M4A);
			if (autoselectIndex == -1)
				autoselectIndex = availList.FindIndex(t => t.audioOnly);
			if (autoselectIndex != -1)
			{
				ytResource.Selected = autoselectIndex;
				var result = ValidateMedia(ytResource);
				if (!result)
					return result.Message;
				return ytResource;
			}

			StringBuilder strb = new StringBuilder();
			strb.AppendLine("\nMultiple formats found please choose one with !f <number>");
			int count = 0;
			foreach (var videoType in ytResource.AvailableTypes)
				strb.Append("[")
					.Append(count++)
					.Append("] ")
					.Append(videoType.codec.ToString())
					.Append(" @ ")
					.AppendLine(videoType.qualitydesciption);

			data.Session.SetResponse(ResponseYoutube, data);
			return strb.ToString();
		}

		private static bool ResponseYoutube(ExecutionInformation info)
		{
			string[] command = info.TextMessage.Message.SplitNoEmpty(' ');
			if (command[0] != "!f")
				return false;
			if (command.Length != 2)
				return true;
			int entry;
			if (int.TryParse(command[1], out entry))
			{
				PlayData data = (PlayData)info.Session.ResponseData;
				if (data?.PlayResource as YoutubeResource == null)
				{
					info.Session.Write("An unexpected error with the ytresource occured: null.");
					return true;
				}
				YoutubeResource ytResource = (YoutubeResource)data.PlayResource;
				if (entry < 0 || entry >= ytResource.AvailableTypes.Count)
					return true;
				ytResource.Selected = entry;
				if (ValidateMedia(ytResource))
					info.Session.Bot.FactoryManager.Play(data);
			}
			return true;
		}

		private static R ValidateMedia(YoutubeResource resource)
		{
			var media = resource.AvailableTypes[resource.Selected];
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

	public sealed class YoutubeResource : PlayResource
	{
		public List<VideoData> AvailableTypes { get; }
		public int Selected { get; set; }

		public YoutubeResource(List<VideoData> availableTypes, AudioResource baseData)
			: base(baseData)
		{
			AvailableTypes = availableTypes;
			Selected = 0;
		}

		public override string Play()
		{
			if (Selected < 0 && Selected >= AvailableTypes.Count)
				return null;
			Log.Write(Log.Level.Debug, "YT Playing: {0}", AvailableTypes[Selected]);
			return AvailableTypes[Selected].link;
		}
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
