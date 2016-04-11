namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Web.Script.Serialization;
	using System.Xml;
	using TS3AudioBot.Algorithm;
	using TS3AudioBot.Helper;
	using TS3AudioBot.ResourceFactories;

	// TODO make public and byref when finished
	internal class PlaylistManager : IDisposable
	{
		private static readonly Regex ytListMatch = new Regex(@"(&|\?)list=([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		// get video info
		// https://www.googleapis.com/youtube/v3/videos?id=...,...&part=contentDetails&key=...

		// get playlist videos
		// https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId=...&key=...

		// todo youtube playlist
		// folder as playlist
		// managing ?

		private PlaylistManagerData data;
		private JavaScriptSerializer json;

		private PlaylistMode mode;
		private int indexCount = 0;
		private IShuffleAlgorithm shuffle;
		private List<DataSet> dataSets;
		private int dataSetLength = 0;

		private Queue<AudioResource> playQueue;

		public bool Random { get; set; }
		public bool Loop { get; set; }

		public PlaylistManager(PlaylistManagerData pmd)
		{
			data = pmd;
			json = new JavaScriptSerializer();
			shuffle = new ListedShuffle();
			dataSets = new List<DataSet>();
			playQueue = new Queue<AudioResource>();
		}

		public void Enqueue(AudioResource resource)
		{
			Random = false;
			Loop = false;
			mode = PlaylistMode.Queue;
			playQueue.Enqueue(resource);
		}

		public void Clear()
		{
			switch (mode)
			{
			case PlaylistMode.List:
				break;
			case PlaylistMode.Queue:
				playQueue.Clear();
				break;
			default: throw new NotImplementedException();
			}
		}

		public AudioResource Next()
		{
			switch (mode)
			{
			case PlaylistMode.List:
				indexCount++;
				int pseudoListIndex;
				if (Random)
					pseudoListIndex = shuffle.SeedIndex(indexCount);
				else
					pseudoListIndex = indexCount;
				return null;

			case PlaylistMode.Queue:
				if (playQueue.Any())
					return playQueue.Dequeue();
				return null;

			default: throw new NotImplementedException();
			}
		}

		private void AddDataSet(DataSet set)
		{
			if (!dataSets.Contains(set))
			{
				dataSets.Add(set);
				RecalcDataSet();
			}
		}

		private void RemoveDataSet(DataSet set)
		{
			if (dataSets.Contains(set))
			{
				dataSets.Remove(set);
				RecalcDataSet();
			}
		}

		private void RecalcDataSet()
		{
			dataSetLength = dataSets.Sum(s => s.Length);
			shuffle.SetData(dataSetLength);
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
				var queryString = new Uri($"https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId={id}{(hasNext ? ("&pageToken=" + nextToken) : string.Empty)}&key={data.youtubeApiKey}");

				string response;
				if (!WebWrapper.DownloadString(out response, queryString))
					throw new Exception(); // TODO correct error handling
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
					queryString = new Uri($"https://www.googleapis.com/youtube/v3/videos?id={string.Join(",", itemBuffer.Select(item => item.Id))}&part=contentDetails&key={data.youtubeApiKey}");
					if (!WebWrapper.DownloadString(out response, queryString))
						throw new Exception(); // TODO correct error handling
					parsed = (Dictionary<string, object>)json.DeserializeObject(response);
					videoDicts = ((object[])parsed["items"]).Cast<Dictionary<string, object>>().ToArray();
					for (int i = 0; i < videoDicts.Length; i++)
						itemBuffer[i].Length = XmlConvert.ToTimeSpan((string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["duration"]));
				}

				videoList.AddRange(itemBuffer);
			} while (hasNext);
		}

		public void Dispose() { }
	}

	enum PlaylistMode
	{
		List,
		Queue,
	}

	abstract class DataSet
	{
		public int Length { get; protected set; }
		public bool NeedRecalc { get; set; }

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
			if (!resourceSet.Contains(resource))
			{

				Length++;
			}
		}

		public void RemoveResource(AudioResource resource)
		{
			if (!resourceSet.Contains(resource))
			{

			}
		}

		public override AudioResource GetResource(int index) => resources[index];
	}

	class YoutubePlaylist : DataSet
	{
		public override AudioResource GetResource(int index)
		{
			throw new NotImplementedException();
		}
	}

	class YoutubePlaylistItem
	{
		public AudioType AudioType { get; set; }
		public string Id { get; set; }
		public TimeSpan Length { get; set; }
	}

#pragma warning disable CS0649
	struct PlaylistManagerData
	{
		[Info("a youtube apiv3 'Browser' type key")]
		public string youtubeApiKey;
	}
#pragma warning restore CS0649
}
