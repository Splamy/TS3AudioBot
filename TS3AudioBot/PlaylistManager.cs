namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Web.Script.Serialization;
	using System.Xml;
	using Algorithm;
	using Helper;
	using ResourceFactories;

	// TODO make public and byref when finished
	public class PlaylistManager : IDisposable
	{
		private static readonly Regex ytListMatch = new Regex(@"(&|\?)list=([a-zA-Z0-9\-_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		// get video info
		// https://www.googleapis.com/youtube/v3/videos?id=...,...&part=contentDetails&key=...

		// get playlist videos
		// https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId=...&key=...

		// Idea:
		// > File as playist
		// each line starts with either
		// ln:<link> and a link which can be opened with a resourcefactory
		// id:<id>   for any already resolved link

		// > playlist must only contain [a-zA-Z ]+ to prevent security issues, max len 63 ??!?

		private PlaylistManagerData data;
		private JavaScriptSerializer json;
		private History.HistoryManager HistoryManager;

		private int indexCount = 0;
		private IShuffleAlgorithm shuffle;
		private Playlist freeList;
		private int dataSetLength = 0;

		public bool Random { get; set; }
		public bool Loop { get; set; }

		public PlaylistManager(PlaylistManagerData pmd)
		{
			data = pmd;
			json = new JavaScriptSerializer();
			shuffle = new ListedShuffle();
		}

		public PlayData Next()
		{
			indexCount++;
			if (Loop)
				indexCount %= freeList.Length;
			else if (indexCount > freeList.Length)
				return null;

			int pseudoListIndex;
			if (Random)
				pseudoListIndex = shuffle.Get(indexCount);
			else
				pseudoListIndex = indexCount;
			return freeList.GetResource(pseudoListIndex);
		}

		public void AddToPlaylist(PlayData resource)
		{
			freeList.AddResource(resource);
		}

		/// <summary>Clears the current playlist</summary>
		public void ClearPlaylist()
		{
			freeList.Clear();
		}

		private void LoadPlaylist(string name)
		{
			throw new NotImplementedException();
		}

		private void SavePlaylist(string name)
		{
			throw new NotImplementedException();
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
						ResourceType = AudioType.Youtube,
						ResourceId = (string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["videoId"]),
					};
				hasNext = parsed.TryGetValue("nextPageToken", out nextToken);

				if (loadLength)
				{
					queryString = new Uri($"https://www.googleapis.com/youtube/v3/videos?id={string.Join(",", itemBuffer.Select(item => item.ResourceId))}&part=contentDetails&key={data.youtubeApiKey}");
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

	class PlaylistItem
	{
		//one of these:
		// playdata holds all needed information for playing + first possiblity
		// > can be a resource (+ in future lazily loaded resource)
		public PlayData MetaData { get; set; }
		// data to load the AR via RFM.RestoreAndPlay
		public string ResourceId { get; set; }
		public AudioType ResourceType { get; set; }
		// > can be a history entry (will need to fall back to id-load if entry is deleted in meanwhile)
		public uint HistoryId { get; set; }
	}

	class Playlist
	{
		// metainfo
		public string Name { get; }
		public uint CreatorDbId { get; }
		// file behaviour: persistent playlist will be synced to a file
		public bool FilePersistent { get; set; }
		// playlist data
		public int Length { get; protected set; }
		private HashSet<AudioResource> resourceSet;
		private List<PlaylistItem> resources;

		public Playlist()
		{
			Util.Init(ref resourceSet);
			Util.Init(ref resources);
		}

		public void AddResource(PlayData resource)
		{
			if (!resourceSet.Contains(resource.Resource))
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

		public void Clear()
		{

		}

		public PlayData GetResource(int index)
		{
			//resources[index];
			throw new NotImplementedException();
		}
	}

	class YoutubePlaylistItem : PlaylistItem
	{
		public TimeSpan Length { get; set; }
	}

#pragma warning disable CS0649
	public struct PlaylistManagerData
	{
		[Info("a youtube apiv3 'Browser' type key")]
		public string youtubeApiKey;
	}
#pragma warning restore CS0649
}
