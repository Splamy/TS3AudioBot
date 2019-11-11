// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using TS3AudioBot.Localization;

namespace TS3AudioBot.ResourceFactories
{
	public interface IResourceResolver : IResolver
	{
		/// <summary>Check method to ask if a factory can load the given link.</summary>
		/// <param name="uri">Any link or something similar a user can obtain to pass it here.</param>
		/// <returns>True if the factory thinks it can parse it, false otherwise.</returns>
		MatchCertainty MatchResource(string uri);
		/// <summary>The factory will try to parse the uri and create a playable resource from it.</summary>
		/// <param name="uri">Any link or something similar a user can obtain to pass it here.</param>
		/// <returns>The playable resource if successful, or an error message otherwise</returns>
		R<PlayResource, LocalStr> GetResource(string uri);
		/// <summary>The factory will try to parse the unique identifier of its scope of responsibility and create a playable resource from it.</summary>
		/// <param name="resource">A resource containing the unique id for a song this factory is responsible for.</param>
		/// <returns>The playable resource if successful, or an error message otherwise</returns>
		R<PlayResource, LocalStr> GetResourceById(AudioResource resource);
		/// <summary>Gets a link to the original site/location. This may differ from the link the resource was orininally created.</summary>
		/// <param name="id">The unique id for a song this factory is responsible for.</param>
		/// <returns>The (close to) original link if successful, null otherwise.</returns>
		string RestoreLink(AudioResource resource);
	}
}
