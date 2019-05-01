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
	using Shuffle;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;

	public sealed class PlaylistManager
	{
		private static readonly Regex CleansePlaylistName = new Regex(@"[^\w-_]", Util.DefaultRegexConfig);
		private const string LocalPlaylistDirectory = "playlists";


		private readonly ConfPlaylists config;
		private readonly ConfBot confBot;
		private readonly PlaylistPool playlistPool;
		private readonly ConcurrentQueue<PlaylistItem> playQueue;
		private readonly Playlist mixList = new Playlist(".mix");
		public IReadOnlyPlaylist CurrentList { get; private set; }

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

		public PlaylistManager(ConfPlaylists config, ConfBot confBot, PlaylistPool playlistPool)
		{
			this.config = config;
			this.confBot = confBot;
			this.playlistPool = playlistPool;
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

		public void StartPlaylist(IReadOnlyPlaylist plist, int index = 0)
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

			while (playQueue.TryDequeue(out _)) ;
		}

		public PlaylistItem[] GetQueue() => playQueue.ToArray();

		private void SetRandomSeed()
		{
			shuffle.Seed = Util.Random.Next();
		}

		// Returns true if all values are normalized
		private (IReadOnlyPlaylist list, int index) NormalizeValues(IReadOnlyPlaylist list, int index)
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

		private string GetPlaylistDirectory()
		{
			switch (config.Share.Value)
			{
			case PlaylistLocation.Bot:
				return Path.Combine(confBot.LocalConfigDir, LocalPlaylistDirectory);
			case PlaylistLocation.Global:
				return config.Path;
			default: throw Util.UnhandledDefault(config.Share.Value);
			}
		}

		private FileInfo GetFileInfo(string name) => new FileInfo(Path.Combine(GetPlaylistDirectory(), name));

		public R<IReadOnlyPlaylist, LocalStr> LoadPlaylist(string name)
		{
			if (name is null)
				throw new ArgumentNullException(nameof(name));

			if (name.StartsWith(".", StringComparison.Ordinal))
				return GetSpecialPlaylist(name);

			var fi = GetFileInfo(name);
			var res = playlistPool.Read(fi);
			if (!res.Ok)
				return res.Error;
			return res.Value;
		}

		public E<LocalStr> ModifyPlaylist(string name, Action<Playlist> action)
		{
			var fi = GetFileInfo(name);
			var res = playlistPool.Read(fi);
			if (!res.Ok)
				return res.Error;
			action(res.Value);
			// TODO dirty instead?
			return playlistPool.Write(res.Value, fi);
		}

		public E<LocalStr> RenamePlaylist(string name, string newName)
		{
			var fiFrom = GetFileInfo(name);
			var fiTo = GetFileInfo(newName);
			return playlistPool.Move(fiFrom, fiTo);
		}

		public E<LocalStr> SavePlaylist(IReadOnlyPlaylist plist)
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
			playlistPool.Write(plist, fi);

			return R.Ok;
		}

		public E<LocalStr> DeletePlaylist(string name)
		{
			if (name is null)
				throw new ArgumentNullException(nameof(name));

			var fi = GetFileInfo(name);
			return playlistPool.Delete(fi);
		}

		public static string CleanseName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return "playlist";
			name = CleansePlaylistName.Replace(name, "");
			if (name.Length > 64)
				name = name.Substring(0, 64);
			if (!Util.IsSafeFileName(name))
				name = "playlist";
			return name;
		}

		// todo local/shared_server/shared_global
		public IEnumerable<string> GetAvailablePlaylists() => GetAvailablePlaylists(null);
		public IEnumerable<string> GetAvailablePlaylists(string pattern)
		{
			var di = new DirectoryInfo(GetPlaylistDirectory());
			if (!di.Exists)
				return Array.Empty<string>();

			IEnumerable<FileInfo> fileEnu;
			if (string.IsNullOrEmpty(pattern))
				fileEnu = di.EnumerateFiles();
			else
				fileEnu = di.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly);

			return fileEnu.Select(fi => fi.Name);
		}

		private R<IReadOnlyPlaylist, LocalStr> GetSpecialPlaylist(string name)
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
