// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;

namespace TS3AudioBot.Web.Model
{
	public class PlaylistInfo
	{
		// TODO better names
		[JsonProperty(PropertyName = "Id")]
		public string Id { get; set; }
		[JsonProperty(PropertyName = "Title")]
		public string Title { get; set; }

		/// <summary>How many songs are in the entire playlist</summary>
		[JsonProperty(PropertyName = "SongCount")]
		public int SongCount { get; set; }
		/// <summary>From which index the itemization begins.</summary>
		[JsonProperty(PropertyName = "DisplayOffset")]
		public int DisplayOffset { get; set; }
		/// <summary>The playlist items for the request.
		/// This might only be a part of the entire playlist.
		/// Check <see cref="SongCount"> for the entire count.</summary>
		[JsonProperty(PropertyName = "Items", NullValueHandling = NullValueHandling.Ignore)]
		public PlaylistItemGetData[] Items { get; set; }
	}
}
