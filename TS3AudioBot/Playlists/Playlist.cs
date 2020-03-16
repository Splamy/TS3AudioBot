// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using TS3AudioBot.Localization;

namespace TS3AudioBot.Playlists
{
	public class Playlist : IReadOnlyPlaylist
	{
		private const int MaxSongs = 1000;
		private string title;
		public string Title { get => title; set => SetTitle(value); }
		private readonly List<PlaylistItem> items;
		public IReadOnlyList<PlaylistItem> Items => items;

		public PlaylistItem this[int i] => items[i];

		public Playlist() :
			this(new List<PlaylistItem>())
		{ }

		public Playlist(List<PlaylistItem> items)
		{
			this.items = items ?? throw new ArgumentNullException(nameof(items));
			title = string.Empty;
		}

		public Playlist SetTitle(string newTitle)
		{
			newTitle = newTitle.Replace("\r", "").Replace("\n", "");
			title = newTitle.Substring(0, Math.Min(newTitle.Length, 256));
			return this;
		}

		private int GetMaxAdd(int amount)
		{
			int remainingSlots = Math.Max(MaxSongs - items.Count, 0);
			return Math.Min(amount, remainingSlots);
		}

		public E<LocalStr> Add(PlaylistItem song)
		{
			if (GetMaxAdd(1) > 0)
			{
				items.Add(song);
				return R.Ok;
			}
			return ErrorFull;
		}

		public E<LocalStr> AddRange(IEnumerable<PlaylistItem> songs)
		{
			var maxAddCount = GetMaxAdd(MaxSongs);
			if (maxAddCount > 0)
			{
				items.AddRange(songs.Take(maxAddCount));
				return R.Ok;
			}
			return ErrorFull;
		}

		public void RemoveAt(int index) => items.RemoveAt(index);

		public E<LocalStr> Insert(int index, PlaylistItem song)
		{
			if (GetMaxAdd(1) > 0)
			{
				items.Insert(index, song);
				return R.Ok;
			}
			return ErrorFull;
		}

		public void Clear() => items.Clear();

		private static readonly E<LocalStr> ErrorFull = new LocalStr("Playlist is full");
	}

	public interface IReadOnlyPlaylist
	{
		PlaylistItem this[int i] { get; }
		string Title { get; }
		IReadOnlyList<PlaylistItem> Items { get; }
	}
}
