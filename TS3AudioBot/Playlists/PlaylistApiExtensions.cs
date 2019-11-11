// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TS3AudioBot.ResourceFactories;
using TS3AudioBot.Web.Model;

namespace TS3AudioBot.Playlists
{
	public static class PlaylistApiExtensions
	{
		public static PlaylistItemGetData ToApiFormat(this ResourceResolver resourceFactory, PlaylistItem item)
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
}
