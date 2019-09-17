// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TS3AudioBot.ResourceFactories;

namespace TS3AudioBot.Playlists
{
	public class PlaylistItemGetData
	{
		// Optional, useful when adding a single element to a list
		// public int? Index { get; set; }
		public string Link { get; set; }
		public string Title { get; set; }
		public string AudioType { get; set; }
		// Link
		// AlbumCover
	}

	public static class PlaylistApiExtensions
	{
		public static PlaylistItemGetData ToApiFormat(this ResourceFactory resourceFactory, PlaylistItem item)
		{
			var resource = item.AudioResource;
			return new PlaylistItemGetData
			{
				Link = resourceFactory.RestoreLink(resource).OkOr(null),
				Title = resource.ResourceTitle,
				AudioType = resource.AudioType,
			};
		}
	}

	public class PlaylistInfo
	{
		public string FileName { get; set; }
		public string PlaylistName { get; set; }

		/// <summary>How many songs are in the entire playlist</summary>
		public int SongCount { get; set; }
		/// <summary>From which index the itemization begins.</summary>
		public int DisplayOffset { get; set; }
		/// <summary>How many items are returned.</summary>
		public int DisplayCount { get; set; }
		/// <summary>The playlist items for the request.
		/// This might only be a part of the entire playlist.
		/// Check <see cref="SongCount"> for the entire count.</summary>
		public PlaylistItemGetData[] Items { get; set; }
	}
}
