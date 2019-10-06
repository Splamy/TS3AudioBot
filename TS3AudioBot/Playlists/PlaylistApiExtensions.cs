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
	using TS3AudioBot.ResourceFactories;
	using TS3AudioBot.Web.Model;

	public static class PlaylistApiExtensions
	{
		public static PlaylistItemGetData ToApiFormat(this ResourceFactory resourceFactory, PlaylistItem item)
		{
			var resource = item.Resource;
			return new PlaylistItemGetData
			{
				Link = resourceFactory.RestoreLink(resource).OkOr(null),
				Title = resource.ResourceTitle,
				AudioType = resource.AudioType,
			};
		}
	}
}
