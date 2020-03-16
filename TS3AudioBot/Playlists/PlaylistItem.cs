// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.ResourceFactories;

namespace TS3AudioBot.Playlists
{
	public class PlaylistItem : IAudioResourceResult
	{
		public MetaData Meta { get; set; }
		public AudioResource AudioResource { get; }

		private PlaylistItem(MetaData meta = null)
		{
			Meta = meta;
		}

		public PlaylistItem(AudioResource resource, MetaData meta = null) : this(meta)
		{
			AudioResource = resource ?? throw new ArgumentNullException(nameof(resource));
		}

		public override string ToString() => AudioResource.ResourceTitle ?? $"{AudioResource.AudioType}: {AudioResource.ResourceId}";
	}
}
