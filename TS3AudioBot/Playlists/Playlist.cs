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
	using Helper;
	using System.Collections.Generic;

	public class Playlist
	{
		// metainfo
		public string Name { get; set; }
		public string OwnerUid { get; set; }
		// file behaviour: persistent playlist will be synced to a file
		public bool FilePersistent { get; set; }
		// playlist data
		public int Count => resources.Count;
		private readonly List<PlaylistItem> resources;

		public Playlist(string name, string ownerUid = null)
		{
			Util.Init(out resources);
			OwnerUid = ownerUid;
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
}
