using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Text.RegularExpressions;

namespace TS3AudioBot
{
	class YoutubeFramework
	{
		WebClient wc;

		public YoutubeRessource LoadedRessource { get; protected set; }

		public YoutubeFramework()
		{
			wc = new WebClient();
			LoadedRessource = null;
		}

		public bool ExtractedURL(string ytlink, bool filterOutInvalid = true)
		{
			string escaped = Uri.EscapeDataString(ytlink);
			string keepvidlink = "http://keepvid.com/?url=" + escaped;

			string resulthtml = string.Empty;
			try
			{
				resulthtml = wc.DownloadString(keepvidlink);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Kevidrequest failed: " + ex.Message);
				return false;
			}
			MatchCollection matches = Regex.Matches(resulthtml, @"<a href=\""(http:\/\/[a-z]\d{1,4}---[^\""]+?)\"".*?<br \/>");

			List<VideoType> videotypes = new List<VideoType>();
			foreach (Match match in matches)
			{
				VideoType vt = new VideoType();
				vt.link = match.Groups[1].Value;
				string codecname = Regex.Match(match.Value, @"Download ([A-Z0-9]+) ").Groups[1].Value;
				switch (codecname)
				{
				case "MP4":
					vt.codec = VideoCodec.MP4;
					break;
				case "M4A":
					vt.codec = VideoCodec.M4A;
					break;
				case "FLV":
					vt.codec = VideoCodec.FLV;
					break;
				case "3GP":
					vt.codec = VideoCodec.ThreeGP;
					break;
				case "WEBM":
					vt.codec = VideoCodec.WEBM;
					break;
				default:
					vt.codec = VideoCodec.Unknow;
					break;
				}
				string qualitydesc = Regex.Match(match.Value, @"<b>([\w\(\)\s]+)<\/b>").Groups[1].Value;
				vt.qualitydesciption = qualitydesc;
				videotypes.Add(vt);
			}
			LoadedRessource = new YoutubeRessource(ytlink, "<Unknown>", videotypes.AsReadOnly());
			return LoadedRessource.AvailableTypes.Count > 0;
		}
	}

	class VideoType
	{
		public string link;
		public string qualitydesciption;
		public VideoCodec codec;

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

		public override bool Play(IPlayerConnection mediaPlayer)
		{
			if (Selected < 0 && Selected >= AvailableTypes.Count)
				return false;
			mediaPlayer.AudioPlay(AvailableTypes[1].link);
			return true;
		}
	}

	enum VideoCodec
	{
		MP4,
		M4A,
		WEBM,
		FLV,
		ThreeGP,
		Unknow,
	}
}
