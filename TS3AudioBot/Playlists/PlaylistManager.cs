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
	using Config;
	using Helper;
	using Localization;
	using Newtonsoft.Json;
	using ResourceFactories;
	using Shuffle;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;

	public sealed class PlaylistManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex CleansePlaylistName = new Regex(@"[^\w-]", Util.DefaultRegexConfig);

		private readonly ConfPlaylists config;
		private readonly ConcurrentQueue<PlaylistItem> playQueue;
		private readonly Playlist mixList = new Playlist(".mix");
		public Playlist CurrentList { get; private set; }

		private IShuffleAlgorithm shuffle;

		private readonly IShuffleAlgorithm NormalOrder = new NormalOrder();
		private readonly IShuffleAlgorithm RandomOrder = new LinearFeedbackShiftRegister();

		public int Index => shuffle.Index;

		public PlaylistItem Current => MoveIndex(null, true);

		private bool random;
		public bool Random
		{
			get => random;
			set
			{
				random = value;
				var index = shuffle.Index;
				if (random)
					shuffle = RandomOrder;
				else
					shuffle = NormalOrder;
				shuffle.Index = index;
			}
		}

		public int Seed { get => shuffle.Seed; set => shuffle.Seed = value; }

		/// <summary>Loop mode for the current playlist.</summary>
		public LoopMode Loop { get; set; } = LoopMode.Off;

		public PlaylistManager(ConfPlaylists config)
		{
			this.config = config;
			shuffle = NormalOrder;
			Util.Init(out playQueue);
		}

		public PlaylistItem Next(bool manually = true) => MoveIndex(forward: true, manually);

		public PlaylistItem Previous(bool manually = true) => MoveIndex(forward: false, manually);

		private PlaylistItem MoveIndex(bool? forward, bool manually)
		{
			if (forward == true && playQueue.TryDequeue(out var pli))
				return pli;

			var (list, index) = NormalizeValues(CurrentList, shuffle.Index);
			if (list == null)
				return null;

			// When next/prev was requested manually (via command) we ignore the loop one
			// mode and instead move the index.
			if ((Loop == LoopMode.One && !manually) || !forward.HasValue)
				return list.GetResource(index);

			bool listEnded;
			if (forward == true)
				listEnded = shuffle.Next();
			else if (forward == false)
				listEnded = shuffle.Prev();
			else
				listEnded = false;

			// Get a new seed when one play-through ended.
			if (listEnded && Random)
				SetRandomSeed();

			// If a next/prev request goes over the bounds of the list while loop mode is off
			// but was requested manually we act as if the list was looped.
			// This will give a more intuitive behaviour when the list is shuffeled (and also if not)
			// as the end might not be clear or visible.
			if (Loop == LoopMode.Off && listEnded && !manually)
				return null;

			(list, index) = NormalizeValues(list, shuffle.Index);
			return list.GetResource(index);
		}

		public void StartPlaylist(Playlist plist, int index = 0)
		{
			if (plist is null)
				throw new ArgumentNullException(nameof(plist));

			(CurrentList, _) = NormalizeValues(plist, index);
			SetRandomSeed();
		}

		public void QueueItem(PlaylistItem item) => playQueue.Enqueue(item);

		public void ClearQueue()
		{
			// Starting with dotnet core 2.1 available
			// playQueue.Clear();

			while (playQueue.TryDequeue(out var _)) ;
		}

		public PlaylistItem[] GetQueue() => playQueue.ToArray();

		private void SetRandomSeed()
		{
			shuffle.Seed = Util.Random.Next();
		}

		// Returns true if all values are normalized
		private (Playlist list, int index) NormalizeValues(Playlist list, int index)
		{
			if (list == null || list.Items.Count == 0)
				return (null, 0);

			if (shuffle.Length != list.Items.Count)
				shuffle.Length = list.Items.Count;

			if (index < 0 || index >= list.Items.Count)
				index = Util.MathMod(index, list.Items.Count);

			if (shuffle.Index != index)
				shuffle.Index = index;

			return (list, index);
		}

		public R<Playlist, LocalStr> LoadPlaylist(string name, bool headOnly = false)
		{
			if (name.StartsWith(".", StringComparison.Ordinal))
				return GetSpecialPlaylist(name);

			var fi = GetFileInfo(name);
			if (!fi.Exists)
				return new LocalStr(strings.error_playlist_not_found);

			using (var sr = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read), Util.Utf8Encoder))
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
								plist.Items.Add(new PlaylistItem(new AudioResource(Uri.UnescapeDataString(rsSplit[1]), Uri.UnescapeDataString(rsSplit[2]), rsSplit[0])));
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
						plist.Items.Add(new PlaylistItem(new AudioResource(rsjdata.resid, rsjdata.title, rsjdata.type)));
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
			if (plist is null)
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

			using (var sw = new StreamWriter(fi.Open(FileMode.Create, FileAccess.Write, FileShare.Read), Util.Utf8Encoder))
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

					foreach (var pli in plist.Items)
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
			{
				return new LocalStr(strings.error_playlist_not_found);
			}
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
			case ".queue": return new Playlist(".queue", new List<PlaylistItem>(playQueue));
			case ".mix": return mixList;
			default: return new LocalStr(strings.error_playlist_special_not_found);
			}
		}
	}
}
