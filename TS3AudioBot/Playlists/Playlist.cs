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
	using System.Collections.Generic;

	public class Playlist
	{
		// metainfo
		public string Name { get; set; }
		public string OwnerUid { get; set; }
		public List<PlaylistItem> Items { get; }

		public Playlist(string name, string ownerUid = null) :
			this(name, new List<PlaylistItem>(), ownerUid)
		{ }

		public Playlist(string name, List<PlaylistItem> items, string ownerUid = null)
		{
			OwnerUid = ownerUid;
			Name = name;
			Items = new List<PlaylistItem>();
		}

		public PlaylistItem GetResource(int index)
		{
			PlaylistItem item = null;
			if (index >= 0 && index < Items.Count)
			{
				item = Items[index];
				item.Meta.From = PlaySource.FromPlaylist;
			}
			return item;
		}
	}
}
