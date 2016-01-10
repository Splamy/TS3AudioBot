using System.Collections.Generic;
using System.Text.RegularExpressions;
using TS3AudioBot.RessourceFactories;
using System.Net;
using System.Web.Script.Serialization;
using System;
using System.Linq;
using System.Xml;

namespace TS3AudioBot
{
	class PlaylistManager
	{
		// get video info
		// https://www.googleapis.com/youtube/v3/videos?id=...,...&part=contentDetails&key=...

		// get playlist videos
		// https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId=...&key=...

		// todo youtube playlist
		// folder as playlist
		// managing ?

		private WebClient client;
		private static readonly Regex ytListMatch = new Regex(@"(&|\?)list=([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private PlaylistManagerData data;
		private JavaScriptSerializer json;

		public bool Random { get; set; }
		public bool Loop { get; set; }

		public PlaylistManager(PlaylistManagerData pmd)
		{
			data = pmd;
			json = new JavaScriptSerializer();
			client = new WebClient();
		}

		public void Enqueue(AudioRessource ressource)
		{

		}

		public void LoadYoutubePlaylist(string ytLink, bool loadLength)
		{
			Match matchYtId = ytListMatch.Match(ytLink);
			if (!matchYtId.Success)
			{
				// error here
				return;
			}
			string id = matchYtId.Groups[2].Value;

			List<PlaylistItem> videoList = new List<PlaylistItem>();

			bool hasNext = false;
			object nextToken = null;
			do
			{
				string queryString = $"https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId={id}{(hasNext ? ("&pageToken=" + nextToken) : string.Empty)}&key={data.youtubeApiKey}";
				var response = client.DownloadString(queryString);
				var parsed = (Dictionary<string, object>)json.DeserializeObject(response);
				var videoDicts = ((object[])parsed["items"]).Cast<Dictionary<string, object>>().ToArray();
				PlaylistItem[] itemBuffer = new PlaylistItem[videoDicts.Length];
				for (int i = 0; i < videoDicts.Length; i++)
					itemBuffer[i] = new PlaylistItem
					{
						AudioType = AudioType.Youtube,
						Id = (string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["videoId"]),
					};
				hasNext = parsed.TryGetValue("nextPageToken", out nextToken);

				if (loadLength)
				{
					queryString = $"https://www.googleapis.com/youtube/v3/videos?id={string.Join(",", itemBuffer.Select(item => item.Id))}&part=contentDetails&key={data.youtubeApiKey}";
					response = client.DownloadString(queryString);
					parsed = (Dictionary<string, object>)json.DeserializeObject(response);
					videoDicts = ((object[])parsed["items"]).Cast<Dictionary<string, object>>().ToArray();
					for (int i = 0; i < videoDicts.Length; i++)
						itemBuffer[i].Length = XmlConvert.ToTimeSpan((string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["duration"]));
				}

				videoList.AddRange(itemBuffer);
			} while (hasNext);
		}
	}

	class PlaylistItem
	{
		public AudioType AudioType { get; set; }
		public string Id { get; set; }
		public TimeSpan Length { get; set; }
	}

	class PlaylistManagerData
	{
		[Info("a youtube apiv3 'Browser' type key")]
		public string youtubeApiKey;
	}
}
