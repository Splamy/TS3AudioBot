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

namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Web.Script.Serialization;
	using System.Xml;
	using System.IO;
	using System.Text;
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

		// > playlist must only contain [a-zA-Z _-]+ to prevent security issues, max len 63 ??!?

		// !playlist remove <hid>|<id>
		// !playlist add <song>
		// !playlist load <list>
		// !playlist save
		// !playlist rename <toNew>
		// !playlist status
		// !playlist merge <otherlist>
		// !playlist move <song> <somewhere?>

		private PlaylistManagerData data;
		private JavaScriptSerializer json;
		private History.HistoryManager HistoryManager;
		private static readonly Encoding FileEncoding = Encoding.ASCII;

		private int indexCount = 0;
		private IShuffleAlgorithm shuffle;
		private Playlist freeList;
		private int dataSetLength = 0;

		public bool Random { get; set; }
		/// <summary>Loop state for the entire playlist.</summary>
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

		public PlayData Previous()
		{
			throw new NotImplementedException();
		}

		public void AddToPlaylist(PlayData playData)
		{
			freeList.AddItem(new PlaylistItem(playData));
		}

		public void AddToPlaylist(PlayData playData, uint hId)
		{
			freeList.AddItem(new PlaylistItem(playData, hId));
		}

		/// <summary>Clears the current playlist</summary>
		public void ClearPlaylist()
		{
			freeList.Clear();
		}

		public R LoadPlaylist(PlayData playData, string name)
		{
			var fi = new FileInfo(Path.Combine(data.playlistPath, name));
			if (fi.Exists)
				return "Playlist not found";

			using (var sr = new StreamReader(fi.OpenRead(), FileEncoding))
			{
				Playlist plist = null;

				string line;
				while ((line = sr.ReadLine()) != null)
				{
					var kvp = line.Split(new[] { ':' }, 2);
					if (kvp.Length != 2) continue;
					string val = kvp[1].Trim();
					switch (kvp[0].Trim())
					{
					case "user":
						ulong userid;
						if (plist != null || !ulong.TryParse(val, out userid))
							return "Invalid playlist file: duplicate userid";
						plist = new Playlist(userid, name);
						break;
					case "ln": plist.AddItem(new PlaylistItem(null)); break;
					case "id": break;
					default: Log.Write(Log.Level.Warning, "Unknown playlist entry {0}:{1}", kvp); break;
					}
				}
			}
			return R.OkR;
		}

		private void SavePlaylist(string name)
		{
			throw new NotImplementedException();
		}

		private Playlist LoadYoutubePlaylist(PlayData playData, string ytLink, bool loadLength)
		{
			Match matchYtId = ytListMatch.Match(ytLink);
			if (!matchYtId.Success)
			{
				// error here
				return null;
			}
			string id = matchYtId.Groups[2].Value;

			var plist = new Playlist(playData.Invoker.DatabaseId, "Youtube playlist: " + id);
			playData.ResourceData = null;
			playData.PlayResource = null;

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
				{
					var pdCopy = playData.Clone();
					pdCopy.ResourceData = new AudioResource(
							(string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["videoId"]),
							null, // TODO: check if name is already available (and for rename conflict when the entry already exists)
							AudioType.Youtube);
					itemBuffer[i] = new YoutubePlaylistItem(pdCopy);
				}
				hasNext = parsed.TryGetValue("nextPageToken", out nextToken);

				if (loadLength)
				{
					queryString = new Uri($"https://www.googleapis.com/youtube/v3/videos?id={string.Join(",", itemBuffer.Select(item => item.MetaData.ResourceData.ResourceId))}&part=contentDetails&key={data.youtubeApiKey}");
					if (!WebWrapper.DownloadString(out response, queryString))
						throw new Exception(); // TODO correct error handling
					parsed = (Dictionary<string, object>)json.DeserializeObject(response);
					videoDicts = ((object[])parsed["items"]).Cast<Dictionary<string, object>>().ToArray();
					for (int i = 0; i < videoDicts.Length; i++)
						itemBuffer[i].Length = XmlConvert.ToTimeSpan((string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["duration"]));
				}

				plist.AddRange(itemBuffer);
			} while (hasNext);
			return plist;
		}

		public void Dispose() { }
	}

	class PlaylistItem
	{
		//one of these:
		// playdata holds all needed information for playing + first possiblity
		// > can be a resource (+ in future lazily loaded resource)
		public PlayData MetaData { get; }
		// > can be a history entry (will need to fall back to id-load if entry is deleted in meanwhile)
		public uint? HistoryId { get; }

		public PlaylistItem(PlayData playData) { MetaData = playData; HistoryId = null; }
		public PlaylistItem(PlayData playData, uint hId) { MetaData = playData; HistoryId = hId; }
	}

	class Playlist
	{
		// metainfo
		public string Name { get; }
		public ulong CreatorDbId { get; }
		// file behaviour: persistent playlist will be synced to a file
		public bool FilePersistent { get; set; }
		// playlist data
		public int Length => resources.Count;
		private List<PlaylistItem> resources;

		public Playlist(ulong creatorDbId, string name)
		{
			Util.Init(ref resources);
			CreatorDbId = creatorDbId;
			Name = name;
		}

		public void AddItem(PlaylistItem item)
		{
			resources.Add(item);
		}

		public void AddRange(IEnumerable<PlaylistItem> items)
		{
			resources.AddRange(items);
		}

		public void RemoveItemAt(int i)
		{
			if (i < 0 || i >= resources.Count)
				return;
			resources.RemoveAt(i);
		}

		public void Clear()
		{
			resources.Clear();
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

		public YoutubePlaylistItem(PlayData playData) : base(playData) { }
	}

#pragma warning disable CS0649
	public struct PlaylistManagerData
	{
		[Info("absolute or relative path the playlist folder", "Playlists")]
		public string playlistPath;
		[Info("a youtube apiv3 'Browser' type key")]
		public string youtubeApiKey;
		[Info("skip songs where user-input is required")]
		public bool skipPostProcessor;
	}
#pragma warning restore CS0649
}
