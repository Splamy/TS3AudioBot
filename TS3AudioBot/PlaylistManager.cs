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
	using System.Xml;
	using System.IO;
	using System.Text;
	using Algorithm;
	using Helper;
	using ResourceFactories;

	// TODO make public and byref when finished
	public sealed class PlaylistManager : IDisposable
	{
		private static readonly Regex ytListMatch = new Regex(@"(&|\?)list=([\w-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex validPlistName = new Regex(@"^[\w -]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
		private static readonly Encoding FileEncoding = Encoding.ASCII;

		private int indexCount = 0;
		private IShuffleAlgorithm shuffle;
		private Playlist freeList;
		private int dataSetLength = -1;

		public int Index
		{
			get { return Random ? (shuffle.Length > 0 ? shuffle.Index : 0) : indexCount; }
			set
			{
				if (Random)
				{
					shuffle.Index = value;
					indexCount = 0;
				}
				else
				{
					indexCount = value;
				}
			}
		}
		private bool random;
		public bool Random
		{
			get { return random; }
			set
			{
				random = value;
				if (random) shuffle.Index = indexCount;
				else indexCount = shuffle.Index;
			}
		}
		/// <summary>Loop state for the entire playlist.</summary>
		public bool Loop { get; set; }

		public PlaylistManager(PlaylistManagerData pmd)
		{
			data = pmd;
			shuffle = new LinearFeedbackShiftRegister();
			freeList = new Playlist(string.Empty);
		}

		public PlaylistItem Current() => NPMove(0);

		public PlaylistItem Next() => NPMove(+1);

		public PlaylistItem Previous() => NPMove(-1);

		private PlaylistItem NPMove(sbyte off)
		{
			if (freeList.Count == 0) return null;
			indexCount += Math.Sign(off);

			if (Loop)
				indexCount = Util.MathMod(indexCount, freeList.Count);
			else if (indexCount < 0 || indexCount >= freeList.Count)
				return null;

			if (Random)
			{
				if (dataSetLength != freeList.Count)
				{
					dataSetLength = freeList.Count;
					shuffle.Set(Util.RngInstance.Next(), dataSetLength);
				}
				if (off > 0) shuffle.Next();
				if (off < 0) shuffle.Prev();
			}

			var entry = freeList.GetResource(Index);
			if (entry == null) return null;
			entry.Meta.FromPlaylist = true;
			return entry;
		}

		public void PlayFreelist(Playlist plist)
		{
			if (plist == null)
				throw new ArgumentNullException(nameof(plist));

			freeList = plist;
			Reset();
		}

		private void Reset()
		{
			indexCount = 0;
			dataSetLength = -1;
			Index = 0;
		}

		public int AddToFreelist(PlaylistItem item) => freeList.AddItem(item);

		public int InsertToFreelist(PlaylistItem item) => freeList.InsertItem(item, Math.Min(Index + 1, freeList.Count));

		/// <summary>Clears the current playlist</summary>
		public void ClearFreelist() => freeList.Clear();

		public R<Playlist> LoadPlaylist(string name, bool headOnly = false)
		{
			var fi = new FileInfo(Path.Combine(data.playlistPath, name));
			if (!fi.Exists)
				return "Playlist not found";

			using (var sr = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read), FileEncoding))
			{
				Playlist plist = new Playlist(name);

				// TODO: seems like every line will need a userbdid...
				// Info; owner:<dbid>
				// Line: <proto>:<userdbid>:<data>
				string line;
				while ((line = sr.ReadLine()) != null)
				{
					if (headOnly && plist.CreatorDbId != null)
						break;

					var kvp = line.Split(new[] { ':' }, 3);

					var meta = new MetaData();
					ulong userid;
					if (string.IsNullOrWhiteSpace(kvp[1]))
						meta.ResourceOwnerDbId = null;
					if (ulong.TryParse(kvp[1].Trim(), out userid))
						meta.ResourceOwnerDbId = userid;
					else
						return "Invalid dbid for entry: " + line;

					switch (kvp[0].Trim())
					{
					case "owner":
						if (plist.CreatorDbId != null)
							return "Invalid playlist file: duplicate userid";
						plist.CreatorDbId = userid;
						break;

					case "ln":
						if (kvp.Length < 3) goto default;
						var lnSplit = kvp[2].Trim().Split(new[] { '.' }, 2);
						if (lnSplit.Length < 2) goto default;
						AudioType audioType;
						if (!string.IsNullOrWhiteSpace(lnSplit[0]) && Enum.TryParse(lnSplit[0], out audioType))
							plist.AddItem(new PlaylistItem(lnSplit[1], audioType, meta));
						else
							plist.AddItem(new PlaylistItem(lnSplit[1], null, meta));
						break;

					case "rs":
						if (kvp.Length < 3) goto default;
						var rsSplit = kvp[2].Trim().Split(new[] { '.' }, 2);
						if (rsSplit.Length < 2) goto default;
						if (!string.IsNullOrWhiteSpace(rsSplit[0]) && Enum.TryParse(rsSplit[0], out audioType))
							plist.AddItem(new PlaylistItem(new AudioResource(rsSplit[1], null, audioType), meta));
						else goto default;
						break;

					case "id":
						if (kvp.Length < 3) goto default;
						uint hid;
						if (!uint.TryParse(kvp[2].Trim(), out hid))
							goto default;
						plist.AddItem(new PlaylistItem(hid, meta));
						break;

					default: Log.Write(Log.Level.Warning, "Erroneus playlist entry: {0}", line); break;
					}
				}
				return plist;
			}
		}

		public R SavePlaylist(Playlist plist)
		{
			if (plist == null)
				throw new ArgumentNullException(nameof(plist));

			if (!IsNameValid(plist.Name))
				return "Invalid playlist name.";

			var di = new DirectoryInfo(data.playlistPath);
			if (!di.Exists)
				return "No playlist directory has been set up.";

			var fi = new FileInfo(Path.Combine(data.playlistPath, plist.Name));
			if (fi.Exists)
			{
				var tempList = LoadPlaylist(plist.Name, true);
				if (!tempList)
					return "Existing playlist ist corrupted, please use another name or repair the existing.";
				if (tempList.Value.CreatorDbId.HasValue && tempList.Value.CreatorDbId != plist.CreatorDbId)
					return "You cannot overwrite a playlist which you dont own.";
			}

			using (var sw = new StreamWriter(fi.Open(FileMode.Create, FileAccess.Write, FileShare.Read), FileEncoding))
			{
				if (plist.CreatorDbId.HasValue)
				{
					sw.Write("owner:");
					sw.Write(plist.CreatorDbId.Value);
					sw.WriteLine();
				}

				foreach (var pli in plist.AsEnumerable())
				{
					if (pli.HistoryId.HasValue)
					{
						sw.Write("id:");
						if (pli.Meta.ResourceOwnerDbId.HasValue)
							sw.Write(pli.Meta.ResourceOwnerDbId.Value);
						sw.Write(":");
						sw.Write(pli.HistoryId.Value);
					}
					else if (!string.IsNullOrWhiteSpace(pli.Link))
					{
						sw.Write("ln:");
						if (pli.Meta.ResourceOwnerDbId.HasValue)
							sw.Write(pli.Meta.ResourceOwnerDbId.Value);
						sw.Write(":");
						if (pli.AudioType.HasValue)
							sw.Write(pli.AudioType.Value);
						sw.Write(".");
						sw.Write(pli.Link);
					}
					else if (pli.Resource != null)
					{
						sw.Write("rs:");
						if (pli.Meta.ResourceOwnerDbId.HasValue)
							sw.Write(pli.Meta.ResourceOwnerDbId.Value);
						sw.Write(":");
						sw.Write(pli.Resource.AudioType);
						sw.Write(".");
						sw.Write(pli.Resource.ResourceId);
					}
					else
						continue;

					sw.WriteLine();
				}
			}

			return R.OkR;
		}

		private R<Playlist> LoadYoutubePlaylist(string ytLink, bool loadLength)
		{
			Match matchYtId = ytListMatch.Match(ytLink);
			if (!matchYtId.Success)
				return "Could not extract a playlist id";

			string id = matchYtId.Groups[2].Value;
			var plist = new Playlist("Youtube playlist: " + id);

			bool hasNext = false;
			object nextToken = null;
			do
			{
				var queryString = new Uri($"https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId={id}{(hasNext ? ("&pageToken=" + nextToken) : string.Empty)}&key={data.youtubeApiKey}");

				string response;
				if (!WebWrapper.DownloadString(out response, queryString))
					return "Web response error";
				var parsed = (Dictionary<string, object>)Util.Serializer.DeserializeObject(response);
				var videoDicts = ((object[])parsed["items"]).Cast<Dictionary<string, object>>().ToArray();
				YoutubePlaylistItem[] itemBuffer = new YoutubePlaylistItem[videoDicts.Length];
				for (int i = 0; i < videoDicts.Length; i++)
				{
					itemBuffer[i] = new YoutubePlaylistItem(new AudioResource(
							(string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["videoId"]),
							null,
							AudioType.Youtube));
				}
				hasNext = parsed.TryGetValue("nextPageToken", out nextToken);

				if (loadLength)
				{
					queryString = new Uri($"https://www.googleapis.com/youtube/v3/videos?id={string.Join(",", itemBuffer.Select(item => item.Resource.ResourceId))}&part=contentDetails&key={data.youtubeApiKey}");
					if (!WebWrapper.DownloadString(out response, queryString))
						return "Web response error";
					parsed = (Dictionary<string, object>)Util.Serializer.DeserializeObject(response);
					videoDicts = ((object[])parsed["items"]).Cast<Dictionary<string, object>>().ToArray();
					for (int i = 0; i < videoDicts.Length; i++)
						itemBuffer[i].Length = XmlConvert.ToTimeSpan((string)(((Dictionary<string, object>)videoDicts[i]["contentDetails"])["duration"]));
				}

				plist.AddRange(itemBuffer);
			} while (hasNext);
			return plist;
		}

		public static bool IsNameValid(string name) => validPlistName.IsMatch(name);

		public void Dispose() { }
	}

	public class PlaylistItem
	{
		public MetaData Meta { get; }
		//one of these:
		// playdata holds all needed information for playing + first possiblity
		// > can be a resource
		public AudioResource Resource { get; } = null;
		// > can be a history entry (will need to fall back to resource-load if entry is deleted in meanwhile)
		public uint? HistoryId { get; } = null;
		// > can be a link to be resolved normally (+ optional audio type)
		public string Link { get; } = null;
		public AudioType? AudioType { get; } = null;

		public PlaylistItem(AudioResource resource, MetaData meta = null) { Resource = resource; Meta = meta ?? new MetaData(); }
		public PlaylistItem(uint hId, MetaData meta = null) { HistoryId = hId; Meta = meta; }
		public PlaylistItem(string message, AudioType? type, MetaData meta = null) { Link = message; AudioType = type; Meta = meta; }
	}

	public class Playlist
	{
		// metainfo
		public string Name { get; set; }
		public ulong? CreatorDbId { get; set; }
		// file behaviour: persistent playlist will be synced to a file
		public bool FilePersistent { get; set; }
		// playlist data
		public int Count => resources.Count;
		private List<PlaylistItem> resources;

		public Playlist(string name) : this(name, null) { }
		public Playlist(string name, ulong? creatorDbId)
		{
			Util.Init(ref resources);
			CreatorDbId = creatorDbId;
			Name = name;
		}

		public int AddItem(PlaylistItem item)
		{
			resources.Add(item);
			return resources.Count - 1;
		}

		public int InsertItem(PlaylistItem item, int index)
		{
			resources.Insert(index, item);
			return index;
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

		public IEnumerable<PlaylistItem> AsEnumerable() => resources;

		public PlaylistItem GetResource(int index)
		{
			return resources[index];
		}
	}

	class YoutubePlaylistItem : PlaylistItem
	{
		public TimeSpan Length { get; set; }

		public YoutubePlaylistItem(AudioResource resource) : base(resource) { }
	}

#pragma warning disable CS0649
	public struct PlaylistManagerData
	{
		[Info("absolute or relative path the playlist folder", "Playlists")]
		public string playlistPath;
		[Info("a youtube apiv3 'Browser' type key")]
		public string youtubeApiKey;
	}
#pragma warning restore CS0649
}
