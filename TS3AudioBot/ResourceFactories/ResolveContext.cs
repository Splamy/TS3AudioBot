// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
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

		public Task<PlayResource> Load(AudioResource resource, CancellationToken cancellationToken) => Resolver.Load(this, resource, cancellationToken);
		public Task<PlayResource> Load(string message, CancellationToken cancellationToken, string? audioType = null) => Resolver.Load(this, message, audioType, cancellationToken);
		public Task<Playlist> LoadPlaylistFrom(string message, CancellationToken cancellationToken, string? audioType = null) => Resolver.LoadPlaylistFrom(this, message, audioType, cancellationToken);
		public string? RestoreLink(AudioResource res) => Resolver.RestoreLink(this, res);
		public Task GetThumbnail(PlayResource playResource, AsyncStreamAction action, CancellationToken cancellationToken) => Resolver.GetThumbnail(this, playResource, action, cancellationToken);
		public Task<IList<AudioResource>> Search(string resolverName, string query, CancellationToken cancellationToken) => Resolver.Search(this, resolverName, query, cancellationToken);
	}
}
