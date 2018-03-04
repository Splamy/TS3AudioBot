// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	using Algorithm;
	using Helper;
	using ResourceFactories;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;

	public sealed class PlaylistManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex ValidPlistName = new Regex(@"^[\w-]+$", Util.DefaultRegexConfig);
		private static readonly Regex CleansePlaylistName = new Regex(@"[^\w-]", Util.DefaultRegexConfig);

		private readonly PlaylistManagerData data;
		private static readonly Encoding FileEncoding = Util.Utf8Encoder;
		private readonly Playlist freeList;
		private readonly Playlist trashList;

		private int indexCount = 0;
		private IShuffleAlgorithm shuffle;
		private int dataSetLength = -1;

		public int Index
		{
			get => Random ? shuffle.Index : indexCount;
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
			get => random;
			set
			{
				random = value;
				if (random) shuffle.Index = indexCount;
				else indexCount = shuffle.Index;
			}
		}
		public int Seed { get => shuffle.Seed; set => shuffle.Seed = value; }
		/// <summary>Loop state for the entire playlist.</summary>
		public bool Loop { get; set; }

		// Playlistfactory related stuff

		public PlaylistManager(PlaylistManagerData pmd)
		{
			data = pmd;
			shuffle = new LinearFeedbackShiftRegister { Seed = Util.Random.Next() };
			freeList = new Playlist(string.Empty);
			trashList = new Playlist(string.Empty);
		}

		public PlaylistItem Current() => NpMove(0);

		public PlaylistItem Next() => NpMove(+1);

		public PlaylistItem Previous() => NpMove(-1);

		private PlaylistItem NpMove(sbyte off)
		{
			if (freeList.Count == 0) return null;
			indexCount += Math.Sign(off);

			if (Loop)
				indexCount = Util.MathMod(indexCount, freeList.Count);
			else if (indexCount < 0 || indexCount >= freeList.Count)
			{
				indexCount = Math.Max(indexCount, 0);
				indexCount = Math.Min(indexCount, freeList.Count);
				return null;
			}

			if (Random)
			{
				if (dataSetLength != freeList.Count)
				{
					dataSetLength = freeList.Count;
					shuffle.Length = dataSetLength;
				}
				if (off > 0) shuffle.Next();
				if (off < 0) shuffle.Prev();
			}

			if (Index < 0) return null;
			var entry = freeList.GetResource(Index);
			if (entry == null) return null;
			entry.Meta.FromPlaylist = true;
			return entry;
		}

		public void PlayFreelist(Playlist plist)
		{
			if (plist == null)
				throw new ArgumentNullException(nameof(plist));

			freeList.Clear();
			freeList.AddRange(plist.AsEnumerable());
			Reset();
		}

		private void Reset()
		{
			indexCount = 0;
			dataSetLength = -1;
			Index = 0;
		}

		public int AddToFreelist(PlaylistItem item) => freeList.AddItem(item);
		public void AddToFreelist(IEnumerable<PlaylistItem> items) => freeList.AddRange(items);
		public int AddToTrash(PlaylistItem item) => trashList.AddItem(item);
		public void AddToTrash(IEnumerable<PlaylistItem> items) => trashList.AddRange(items);

		public int InsertToFreelist(PlaylistItem item) => freeList.InsertItem(item, Math.Min(Index + 1, freeList.Count));

		/// <summary>Clears the current playlist</summary>
		public void ClearFreelist() => freeList.Clear();
		public void ClearTrash() => trashList.Clear();

		public R<Playlist> LoadPlaylist(string name, bool headOnly = false)
		{
			if (name.StartsWith(".", StringComparison.Ordinal))
			{
				var result = GetSpecialPlaylist(name);
				if (result)
					return result;
			}
			var fi = GetFileInfo(name);
			if (!fi.Exists)
				return "Playlist not found";

			using (var sr = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read), FileEncoding))
			{
				var plist = new Playlist(name);

				// Info: version:<num>
				// Info: owner:<dbid>
				// Line: <kind>:<data,data,..>:<opt-title>

				string line;

				// read header
				while ((line = sr.ReadLine()) != null)
				{
					if (string.IsNullOrEmpty(line))
						break;

					var kvp = line.Split(new[] { ':' }, 2);
					if (kvp.Length < 2) continue;

					string key = kvp[0];
					string value = kvp[1];

					switch (key)
					{
					case "version": // skip, not yet needed
						break;

					case "owner":
						if (plist.CreatorDbId != null)
							return "Invalid playlist file: duplicate userid";
						if (ulong.TryParse(value, out var userid))
							plist.CreatorDbId = userid;
						else
							return "Broken playlist header";
						break;
					}
				}

				if (headOnly)
					return plist;

				// read content
				while ((line = sr.ReadLine()) != null)
				{
					var kvp = line.Split(new[] { ':' }, 3);
					if (kvp.Length < 3)
					{
						Log.Warn("Erroneus playlist split count: {0}", line);
						continue;
					}
					string kind = kvp[0];
					string optOwner = kvp[1];
					string content = kvp[2];

					var meta = new MetaData();
					if (string.IsNullOrWhiteSpace(optOwner))
						meta.ResourceOwnerDbId = null;
					else if (ulong.TryParse(optOwner, out var userid))
						meta.ResourceOwnerDbId = userid;
					else
						Log.Warn("Erroneus playlist meta data: {0}", line);

					switch (kind)
					{
					case "rs":
						var rsSplit = content.Split(new[] { ',' }, 3);
						if (rsSplit.Length < 3)
							goto default;
						if (!string.IsNullOrWhiteSpace(rsSplit[0]))
							plist.AddItem(new PlaylistItem(new AudioResource(Uri.UnescapeDataString(rsSplit[1]), Uri.UnescapeDataString(rsSplit[2]), rsSplit[0]), meta));
						else
							goto default;
						break;

					case "id":
					case "ln":
						Log.Warn("Deprecated playlist data block: {0}", line);
						break;

					default:
						Log.Warn("Erroneus playlist data block: {0}", line);
						break;
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

			var di = new DirectoryInfo(data.PlaylistPath);
			if (!di.Exists)
				return "No playlist directory has been set up.";

			var fi = GetFileInfo(plist.Name);
			if (fi.Exists)
			{
				var tempList = LoadPlaylist(plist.Name, true);
				if (!tempList)
					return "Existing playlist is corrupted, please use another name or repair the existing.";
				if (tempList.Value.CreatorDbId.HasValue && tempList.Value.CreatorDbId != plist.CreatorDbId)
					return "You cannot overwrite a playlist which you dont own.";
			}

			using (var sw = new StreamWriter(fi.Open(FileMode.Create, FileAccess.Write, FileShare.Read), FileEncoding))
			{
				sw.WriteLine("version:1");

				if (plist.CreatorDbId.HasValue)
				{
					sw.Write("owner:");
					sw.Write(plist.CreatorDbId.Value);
					sw.WriteLine();
				}

				sw.WriteLine();

				foreach (var pli in plist.AsEnumerable())
				{
					sw.Write("rs:");
					if (pli.Meta.ResourceOwnerDbId.HasValue
						&& (!plist.CreatorDbId.HasValue || pli.Meta.ResourceOwnerDbId.Value != plist.CreatorDbId.Value))
						sw.Write(pli.Meta.ResourceOwnerDbId.Value);
					sw.Write(":");
					sw.Write(pli.Resource.AudioType);
					sw.Write(",");
					sw.Write(Uri.EscapeDataString(pli.Resource.ResourceId));
					sw.Write(",");
					sw.Write(Uri.EscapeDataString(pli.Resource.ResourceTitle));

					sw.WriteLine();
				}
			}

			return R.OkR;
		}

		private FileInfo GetFileInfo(string name) => new FileInfo(Path.Combine(data.PlaylistPath, name ?? string.Empty));

		public R DeletePlaylist(string name, ulong requestingClientDbId, bool force = false)
		{
			var fi = GetFileInfo(name);
			if (!fi.Exists)
				return "Playlist not found";
			else if (!force)
			{
				var tempList = LoadPlaylist(name, true);
				if (!tempList)
					return "Existing playlist is corrupted, please use another name or repair the existing.";
				if (tempList.Value.CreatorDbId.HasValue && tempList.Value.CreatorDbId != requestingClientDbId)
					return "You cannot delete a playlist which you dont own.";
			}

			try
			{
				fi.Delete();
				return R.OkR;
			}
			catch (IOException) { return "File still in use"; }
			catch (System.Security.SecurityException) { return "Missing rights to delete this file"; }
		}

		public static R IsNameValid(string name)
		{
			if (string.IsNullOrEmpty(name))
				return "An empty playlist name is not valid";
			if (name.Length >= 64)
				return "Length must be <64";
			if (!ValidPlistName.IsMatch(name))
				return "The new name is invalid please only use [a-zA-Z0-9_-]";
			return R.OkR;
		}

		public static string CleanseName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return "playlist";
			if (name.Length >= 64)
				name = name.Substring(0, 63);
			name = CleansePlaylistName.Replace(name, "");
			if (!IsNameValid(name))
				name = "playlist";
			return name;
		}

		public IEnumerable<string> GetAvailablePlaylists() => GetAvailablePlaylists(null);
		public IEnumerable<string> GetAvailablePlaylists(string pattern)
		{
			var di = new DirectoryInfo(data.PlaylistPath);
			if (!di.Exists)
				return Array.Empty<string>();

			IEnumerable<FileInfo> fileEnu;
			if (string.IsNullOrEmpty(pattern))
				fileEnu = di.EnumerateFiles();
			else
				fileEnu = di.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly);

			return fileEnu.Select(fi => fi.Name);
		}

		private R<Playlist> GetSpecialPlaylist(string name)
		{
			if (!name.StartsWith(".", StringComparison.Ordinal))
				return "Not a reserved list type.";

			switch (name)
			{
			case ".queue": return freeList;
			case ".trash": return trashList;
			default: return "Special list not found";
			}
		}
	}

	public class PlaylistItem
	{
		public MetaData Meta { get; }
		//one of these:
		// playdata holds all needed information for playing + first possibility
		// > can be a resource
		public AudioResource Resource { get; }

		public string DisplayString => Resource.ResourceTitle ?? $"{Resource.AudioType}: {Resource.ResourceId}";

		private PlaylistItem(MetaData meta) { Meta = meta ?? new MetaData(); }
		public PlaylistItem(AudioResource resource, MetaData meta = null) : this(meta) { Resource = resource; }
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
		private readonly List<PlaylistItem> resources;

		public Playlist(string name, ulong? creatorDbId = null)
		{
			Util.Init(out resources);
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

		public void AddRange(IEnumerable<PlaylistItem> items) => resources.AddRange(items);

		public void RemoveItemAt(int i)
		{
			if (i < 0 || i >= resources.Count)
				return;
			resources.RemoveAt(i);
		}

		public void Clear() => resources.Clear();

		public IEnumerable<PlaylistItem> AsEnumerable() => resources;

		public PlaylistItem GetResource(int index) => resources[index];
	}

#pragma warning disable CS0649
	public class PlaylistManagerData : ConfigData
	{
		[Info("Path the playlist folder", "Playlists")]
		public string PlaylistPath { get; set; }
	}
#pragma warning restore CS0649
}
