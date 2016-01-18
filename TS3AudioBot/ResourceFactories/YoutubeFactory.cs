using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using TS3Query.Messages;
using TS3AudioBot.Helper;

namespace TS3AudioBot.ResourceFactories
{
	class YoutubeFactory : IResourceFactory
	{
		private Regex idMatch = new Regex(@"(&|\?)v=([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private WebClient wc;

		public AudioType FactoryFor => AudioType.Youtube;

		public YoutubeFactory()
		{
			wc = new WebClient();
		}

		public bool MatchLink(string link) => Regex.IsMatch(link, @"^(https?\:\/\/)?(www\.)?(youtube\.|youtu\.be)");

		public RResultCode GetResource(string ytLink, out AudioResource result)
		{
			Match matchYtId = idMatch.Match(ytLink);
			if (!matchYtId.Success)
			{
				result = null;
				return RResultCode.YtIdNotFound;
			}
			return GetResourceById(matchYtId.Groups[2].Value, null, out result);
		}

		public RResultCode GetResourceById(string ytID, string name, out AudioResource result)
		{
			string resulthtml = string.Empty;
			try
			{
				resulthtml = wc.DownloadString(string.Format("http://www.youtube.com/get_video_info?video_id={0}&el=info", ytID));
			}
			catch (WebException ex)
			{
				Log.Write(Log.Level.Warning, "Youtube downloadreqest failed: " + ex.Message);
				result = null;
				return RResultCode.YtNoConnection;
			}

			List<VideoType> videoTypes = new List<VideoType>();
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

					VideoType vt = new VideoType();
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
			}

			string finalName = name ?? dataParse["title"] ?? string.Format("<YT - no title : {0}>", ytID);
			var ytResult = new YoutubeResource(ytID, finalName, videoTypes.AsReadOnly());
			result = ytResult;
			return ytResult.AvailableTypes.Count > 0 ? RResultCode.Success : RResultCode.YtNoVideosExtracted;
		}

		public string RestoreLink(string id) => $"https://www.youtube.com/watch?v={id}";

		public void PostProcess(PlayData data, out bool abortPlay)
		{
			YoutubeResource ytResource = (YoutubeResource)data.Resource;

			var availList = ytResource.AvailableTypes.ToList();
			int autoselectIndex = availList.FindIndex(t => t.codec == VideoCodec.M4A);
			if (autoselectIndex == -1)
				autoselectIndex = availList.FindIndex(t => t.audioOnly);
			if (autoselectIndex != -1)
			{
				ytResource.Selected = autoselectIndex;
				if (!ValidateMedia(ytResource))
				{
					abortPlay = true;
					data.Session.Write("The video cannot be played due to youtube restrictions.");
					return;
				}
				else {
					abortPlay = false;
					return;
				}
			}

			StringBuilder strb = new StringBuilder();
			strb.AppendLine("\nMultiple formats found please choose one with !f <number>");
			int count = 0;
			foreach (var videoType in ytResource.AvailableTypes)
			{
				strb.Append("[");
				strb.Append(count++);
				strb.Append("] ");
				strb.Append(videoType.codec.ToString());
				strb.Append(" @ ");
				strb.AppendLine(videoType.qualitydesciption);
			}

			abortPlay = true;
			data.Session.Write(strb.ToString());
			data.Session.UserResource = data;
			data.Session.SetResponse(ResponseYoutube, null, false);
		}

		private static bool ResponseYoutube(BotSession session, TextMessage tm, bool isAdmin)
		{
			string[] command = tm.Message.SplitNoEmpty(' ');
			if (command[0] != "!f")
				return false;
			if (command.Length != 2)
				return true;
			int entry;
			if (int.TryParse(command[1], out entry))
			{
				PlayData data = session.UserResource;
				if (data == null || data.Resource as YoutubeResource == null)
				{
					session.Write("An unexpected error with the ytresource occured: null.");
					return true;
				}
				YoutubeResource ytResource = (YoutubeResource)data.Resource;
				if (entry < 0 || entry >= ytResource.AvailableTypes.Count)
					return true;
				ytResource.Selected = entry;
				if (!ValidateMedia(ytResource))
				{
					session.Write("The video cannot be played due to youtube restrictions.");
					return true;
				}

				session.Bot.FactoryManager.Play(data);
			}
			return true;
		}

		private static bool ValidateMedia(YoutubeResource resource)
		{
			var media = resource.AvailableTypes[resource.Selected];
			var request = WebRequest.Create(media.link) as HttpWebRequest;
			try { request.GetResponse(); }
			catch (WebException webEx)
			{
				HttpWebResponse errorResponse = webEx.Response as HttpWebResponse;
				if (errorResponse == null)
					Log.Write(Log.Level.Warning, $"YT Video media error: {webEx}");
				else
					Log.Write(Log.Level.Warning, $"YT Video media error: [{(int)errorResponse.StatusCode}] {errorResponse.StatusCode}");
				return false;
			}
			return true;
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

	class YoutubeResource : AudioResource
	{
		public IList<VideoType> AvailableTypes { get; protected set; }

		public int Selected { get; set; }

		public override AudioType AudioType { get { return AudioType.Youtube; } }

		public YoutubeResource(string ytId, string youtubeName, IList<VideoType> availableTypes)
			: base(ytId, youtubeName)
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

	enum VideoCodec
	{
		Unknown,
		MP4,
		M4A,
		WEBM,
		FLV,
		ThreeGP,
	}
}
