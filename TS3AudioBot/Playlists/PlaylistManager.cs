// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Playlists
{
	using Algorithm;
	using Config;
	using Helper;
	using Localization;
	using Newtonsoft.Json;
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
		private static readonly Regex CleansePlaylistName = new Regex(@"[^\w-]", Util.DefaultRegexConfig);

		private readonly ConfPlaylists config;
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

		public PlaylistManager(ConfPlaylists config)
		{
			this.config = config;
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

		public R<Playlist, LocalStr> LoadPlaylist(string name, bool headOnly = false)
		{
			if (name.StartsWith(".", StringComparison.Ordinal))
			{
				var result = GetSpecialPlaylist(name);
				if (result)
					return result;
			}
			var fi = GetFileInfo(name);
			if (!fi.Exists)
				return new LocalStr(strings.error_playlist_not_found);

			using (var sr = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read), FileEncoding))
			{
				var plist = new Playlist(name);

				// Info: version:<num>
				// Info: owner:<uid>
				// Line: <kind>:<data,data,..>:<opt-title>

				string line;
				int version = 1;

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
					case "version":
						version = int.Parse(value);
						if (version > 2)
							return new LocalStr("The file version is too new and can't be read."); // LOC: TODO
						break;

					case "owner":
						if (plist.OwnerUid != null)
						{
							Log.Warn("Invalid playlist file: duplicate userid");
							return new LocalStr(strings.error_playlist_broken_file);
						}

						if (version == 2)
						{
							plist.OwnerUid = value;
						}
						break;
					}
				}

				if (headOnly)
					return plist;

				// read content
				while ((line = sr.ReadLine()) != null)
				{
					var kvp = line.Split(new[] { ':' }, 2);
					if (kvp.Length < 2) continue;

					string key = kvp[0];
					string value = kvp[1];

					switch (key)
					{
					case "rs":
						{
							var rskvp = value.Split(new[] { ':' }, 2);
							if (kvp.Length < 2)
							{
								Log.Warn("Erroneus playlist split count: {0}", line);
								continue;
							}
							string optOwner = rskvp[0];
							string content = rskvp[1];

							var rsSplit = content.Split(new[] { ',' }, 3);
							if (rsSplit.Length < 3)
								goto default;
							if (!string.IsNullOrWhiteSpace(rsSplit[0]))
								plist.AddItem(new PlaylistItem(new AudioResource(Uri.UnescapeDataString(rsSplit[1]), Uri.UnescapeDataString(rsSplit[2]), rsSplit[0])));
							else
								goto default;
							break;
						}

					case "rsj":
						var rsjdata = JsonConvert.DeserializeAnonymousType(value, new
						{
							type = string.Empty,
							resid = string.Empty,
							title = string.Empty
						});
						plist.AddItem(new PlaylistItem(new AudioResource(rsjdata.resid, rsjdata.title, rsjdata.type)));
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

		private static R<Playlist, LocalStr> LoadChecked(R<Playlist, LocalStr> loadResult, string ownerUid)
		{
			if (!loadResult)
				return new LocalStr($"{strings.error_playlist_broken_file} ({loadResult.Error.Str})");
			if (loadResult.Value.OwnerUid != null && loadResult.Value.OwnerUid != ownerUid)
				return new LocalStr(strings.error_playlist_cannot_access_not_owned);
			return loadResult;
		}

		public E<LocalStr> SavePlaylist(Playlist plist)
		{
			if (plist == null)
				throw new ArgumentNullException(nameof(plist));

			var nameCheck = Util.IsSafeFileName(plist.Name);
			if (!nameCheck)
				return nameCheck.Error;

			var di = new DirectoryInfo(config.Path);
			if (!di.Exists)
				return new LocalStr(strings.error_playlist_no_store_directory);

			var fi = GetFileInfo(plist.Name);
			if (fi.Exists)
			{
				var tempList = LoadChecked(LoadPlaylist(plist.Name, true), plist.OwnerUid);
				if (!tempList)
					return tempList.OnlyError();
			}

			using (var sw = new StreamWriter(fi.Open(FileMode.Create, FileAccess.Write, FileShare.Read), FileEncoding))
			{
				sw.WriteLine("version:2");
				if (plist.OwnerUid != null)
				{
					sw.Write("owner:");
					sw.Write(plist.OwnerUid);
					sw.WriteLine();
				}

				sw.WriteLine();
				
				using (var json = new JsonTextWriter(sw))
				{
					json.Formatting = Formatting.None;

					foreach (var pli in plist.AsEnumerable())
					{
						sw.Write("rsj:");
						json.WriteStartObject();
						json.WritePropertyName("type");
						json.WriteValue(pli.Resource.AudioType);
						json.WritePropertyName("resid");
						json.WriteValue(pli.Resource.ResourceId);
						if (pli.Resource.ResourceTitle != null)
						{
							json.WritePropertyName("title");
							json.WriteValue(pli.Resource.ResourceTitle);
						}
						json.WriteEndObject();
						json.Flush();
						sw.WriteLine();
					}
				}
			}

			return R.Ok;
		}

		private FileInfo GetFileInfo(string name) => new FileInfo(Path.Combine(config.Path, name ?? string.Empty));

		public E<LocalStr> DeletePlaylist(string name, string requestingClientUid, bool force = false)
		{
			var fi = GetFileInfo(name);
			if (!fi.Exists)
				return new LocalStr(strings.error_playlist_not_found);
			else if (!force)
			{
				var tempList = LoadChecked(LoadPlaylist(name, true), requestingClientUid);
				if (!tempList)
					return tempList.OnlyError();
			}

			try
			{
				fi.Delete();
				return R.Ok;
			}
			catch (IOException) { return new LocalStr(strings.error_io_in_use); }
			catch (System.Security.SecurityException) { return new LocalStr(strings.error_io_missing_permission); }
		}

		public static string CleanseName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return "playlist";
			if (name.Length >= 64)
				name = name.Substring(0, 63);
			name = CleansePlaylistName.Replace(name, "");
			if (!Util.IsSafeFileName(name))
				name = "playlist";
			return name;
		}

		public IEnumerable<string> GetAvailablePlaylists() => GetAvailablePlaylists(null);
		public IEnumerable<string> GetAvailablePlaylists(string pattern)
		{
			var di = new DirectoryInfo(config.Path);
			if (!di.Exists)
				return Array.Empty<string>();

			IEnumerable<FileInfo> fileEnu;
			if (string.IsNullOrEmpty(pattern))
				fileEnu = di.EnumerateFiles();
			else
				fileEnu = di.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly);

			return fileEnu.Select(fi => fi.Name);
		}

		private R<Playlist, LocalStr> GetSpecialPlaylist(string name)
		{
			if (!name.StartsWith(".", StringComparison.Ordinal))
				throw new ArgumentException("Not a reserved list type.", nameof(name));

			switch (name)
			{
			case ".queue": return freeList;
			case ".trash": return trashList;
			default: return new LocalStr(strings.error_playlist_special_not_found);
			}
		}
	}
}
