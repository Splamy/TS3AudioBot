using System.Collections.Generic;
using System.Text.RegularExpressions;
using TS3AudioBot.ResourceFactories;
using System.Net;
using System.Web.Script.Serialization;
using System;
using System.Linq;
using System.Xml;
using TS3AudioBot.Algorithm;

namespace TS3AudioBot
{
	class PlaylistManager
	{
		private static readonly Regex ytListMatch = new Regex(@"(&|\?)list=([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		// get video info
		// https://www.googleapis.com/youtube/v3/videos?id=...,...&part=contentDetails&key=...

		// get playlist videos
		// https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId=...&key=...

		// todo youtube playlist
		// folder as playlist
		// managing ?

		private WebClient client;
		private PlaylistManagerData data;
		private JavaScriptSerializer json;

		private int indexCount = 0;
		private IShuffleAlgorithm shuffle;
		private List<DataSet> dataSets;
		private int dataSetLength = 0;

		public bool Random { get; set; }
		public bool Loop { get; set; }

		public PlaylistManager(PlaylistManagerData pmd)
		{
			data = pmd;
			json = new JavaScriptSerializer();
			client = new WebClient();
			shuffle = new ListedShuffle();
		}

		public void Enqueue(AudioResource resource)
		{

		}

		public void Next()
		{
			indexCount++;

			int pseudoListIndex;
			if (Random)
				pseudoListIndex = shuffle.Next();
			else
				pseudoListIndex = indexCount;


		}

		private void AddDataSet(DataSet set)
		{
			if (!dataSets.Contains(set))
			{
				dataSets.Add(set);
				dataSetLength = dataSets.Sum(s => s.Length);
				shuffle.SetData(dataSetLength);
			}
		}

		private void RemoveDataSet(DataSet set)
		{
			if (dataSets.Contains(set))
			{
				dataSets.Remove(set);
				dataSetLength = dataSets.Sum(s => s.Length);
				shuffle.SetData(dataSetLength);
			}
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

			List<YoutubePlaylistItem> videoList = new List<YoutubePlaylistItem>();

			bool hasNext = false;
			object nextToken = null;
			do
			{
				string queryString = $"https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId={id}{(hasNext ? ("&pageToken=" + nextToken) : string.Empty)}&key={data.youtubeApiKey}";
				var response = client.DownloadString(queryString);
				var parsed = (Dictionary<string, object>)json.DeserializeObject(response);
				var videoDicts = ((object[])parsed["items"]).Cast<Dictionary<string, object>>().ToArray();
				YoutubePlaylistItem[] itemBuffer = new YoutubePlaylistItem[videoDicts.Length];
				for (int i = 0; i < videoDicts.Length; i++)
					itemBuffer[i] = new YoutubePlaylistItem
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

	abstract class DataSet
	{
		public int Length { get; protected set; }

		public abstract AudioResource GetResource(int index);
	}

	class FreeSet : DataSet
	{
		private HashSet<AudioResource> resourceSet;
		private List<AudioResource> resources;

		public FreeSet()
		{
			resourceSet = new HashSet<AudioResource>();
			resources = new List<AudioResource>();
		}

		public void AddResource(AudioResource resource)
		{

		}

		public override AudioResource GetResource(int index)
		{
			return resources[index];
		}
	}

	class YoutubePlaylist
	{

	}

	class YoutubePlaylistItem
	{
		public AudioType AudioType { get; set; }
		public string Id { get; set; }
		public TimeSpan Length { get; set; }
	}

	struct PlaylistManagerData
	{
		[Info("a youtube apiv3 'Browser' type key")]
		public string youtubeApiKey;
	}
}
