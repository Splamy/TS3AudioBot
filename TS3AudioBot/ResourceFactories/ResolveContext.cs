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
using System.IO;
using System.Threading.Tasks;
using TS3AudioBot.Config;
using TS3AudioBot.Playlists;

namespace TS3AudioBot.ResourceFactories
{
	public class ResolveContext
	{
		public ResourceResolver Resolver { get; }
		public ConfBot Config { get; }

		public ResolveContext(ResourceResolver resolver, ConfBot config)
		{
			Resolver = resolver;
			Config = config;
		}

		public Task<PlayResource> Load(AudioResource resource) => Resolver.Load(this, resource);
		public Task<PlayResource> Load(string message, string? audioType = null) => Resolver.Load(this, message, audioType);
		public Task<Playlist> LoadPlaylistFrom(string message, string? audioType = null) => Resolver.LoadPlaylistFrom(this, message, audioType);
		public string? RestoreLink(AudioResource res) => Resolver.RestoreLink(this, res);
		public Task GetThumbnail(PlayResource playResource, Func<Stream, Task> action) => Resolver.GetThumbnail(this, playResource, action);
		public Task<IList<AudioResource>> Search(string resolverName, string query) => Resolver.Search(this, resolverName, query);
	}
}
