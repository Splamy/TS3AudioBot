// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.Collections.Generic;
	using Helper;

	public sealed class ResourceFactoryManager : MarshalByRefObject, IDisposable
	{
		public IResourceFactory DefaultFactorty { get; internal set; }
		private IList<IResourceFactory> factories;

		public ResourceFactoryManager()
		{
			factories = new List<IResourceFactory>();
		}

		// Load lookup stages
		// PlayResource != null    => ret PlayResource
		// ResourceData != null    => call RF.RestoreFromId
		// TextMessage != null     => call RF.GetResoruce
		// else                    => ret Error

		/// <summary>
		/// Creates a new <see cref="PlayResource"/> which can be played.
		/// The build data will be taken from <see cref="PlayData.ResourceData"/> or 
		/// <see cref="PlayData.Message"/> if no AudioResource is given.
		/// </summary>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource> Load(PlayData playData)
		{
			if (playData == null)
				throw new ArgumentNullException(nameof(playData));

			if (playData.PlayResource != null)
				return playData.PlayResource;

			if (playData.ResourceData != null)
				return Load(playData, playData.ResourceData.AudioType);

			string netlinkurl = TextUtil.ExtractUrlFromBB(playData.Message);
			IResourceFactory factory = GetFactoryFor(netlinkurl);
			return Load(playData, factory);
		}

		/// <summary>
		/// Same as <see cref="LoadAndPlay(PlayData)"/> except it lets you pick an
		/// <see cref="IResourceFactory"/> identifier to manually select a factory.
		/// </summary>
		/// <param name="audioType">The associated <see cref="AudioType"/> to a factory.</param>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource> Load(PlayData playData, AudioType audioType)
		{
			if (playData == null)
				throw new ArgumentNullException(nameof(playData));

			if (playData.PlayResource != null)
				return playData.PlayResource;

			var factory = GetFactoryFor(audioType);
			return Load(playData, factory);
		}

		private R<PlayResource> Load(PlayData playData, IResourceFactory factory)
		{
			if (playData.ResourceData != null)
			{
				var result = factory.GetResourceById(playData.ResourceData);
				if (!result)
					return $"Could not restore ({result.Message})";
				return result;
			}
			else if (playData.Message != null)
			{
				string netlinkurl = TextUtil.ExtractUrlFromBB(playData.Message);

				var result = factory.GetResource(netlinkurl);
				if (!result)
					return $"Could not play ({result.Message})";
				return result;
			}
			else
				return "No method matched to load this resource";
		}

		/// <summary>
		/// Invokes postprocess operations for the passed <see cref="PlayData.ResourceData"/> and
		/// the corresponding factory.
		/// </summary>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource> PostProcess(PlayData data)
		{
			var factory = GetFactoryFor(data.ResourceData.AudioType);
			return factory.PostProcess(data);
		}

		private IResourceFactory GetFactoryFor(AudioType audioType)
		{
			foreach (var fac in factories)
				if (fac.FactoryFor == audioType) return fac;
			return DefaultFactorty;
		}
		private IResourceFactory GetFactoryFor(string uri)
		{
			foreach (var fac in factories)
				if (fac.MatchLink(uri)) return fac;
			return DefaultFactorty;
		}

		public void AddFactory(IResourceFactory factory)
		{
			factories.Add(factory);
		}

		public string RestoreLink(PlayData data) => RestoreLink(data.ResourceData);
		public string RestoreLink(AudioResource res)
		{
			IResourceFactory factory = GetFactoryFor(res.AudioType);
			return factory.RestoreLink(res.ResourceId);
		}

		public void Dispose()
		{
			foreach (var fac in factories)
				fac.Dispose();
		}
	}
}
