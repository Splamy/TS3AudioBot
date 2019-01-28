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
	using System;
	using System.Collections.Generic;

	public class Playlist : IReadOnlyPlaylist
	{
		public string Name { get; set; }
		public List<PlaylistItem> Items { get; }
		IReadOnlyList<PlaylistItem> IReadOnlyPlaylist.Items => Items;

		public Playlist(string name) :
			this(name, new List<PlaylistItem>())
		{ }

		public Playlist(string name, List<PlaylistItem> items)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Items = items ?? throw new ArgumentNullException(nameof(items));
		}
	}

	public interface IReadOnlyPlaylist
	{
		string Name { get; set; }
		IReadOnlyList<PlaylistItem> Items { get; }
	}

	public static class PlaylistTrait {
		public static PlaylistItem GetResource(this IReadOnlyPlaylist self, int index)
		{
			PlaylistItem item = null;
			if (index >= 0 && index < self.Items.Count)
			{
				item = self.Items[index];
				item.Meta.From = PlaySource.FromPlaylist;
			}
			return item;
		}
	}
}
