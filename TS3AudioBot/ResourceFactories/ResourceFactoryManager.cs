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
	using History;

	public sealed class ResourceFactoryManager : MarshalByRefObject, IDisposable
	{
		public IResourceFactory DefaultFactorty { get; internal set; }
		private IList<IResourceFactory> factories;
		private AudioFramework audioFramework;
		private PlaylistManager playlistManager;

		public ResourceFactoryManager(AudioFramework audioFramework, PlaylistManager playlistManager)
		{
			factories = new List<IResourceFactory>();
			this.audioFramework = audioFramework;
			this.playlistManager = playlistManager;
		}

		/// <summary>
		/// Creates a new <see cref="PlayResource"/> which can be played.
		/// The build data will be taken from <see cref="PlayData.ResourceData"/> or 
		/// <see cref="PlayData.Message"/> if no AudioResource is given.
		/// </summary>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R LoadAndPlay(PlayData data)
		{
			string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);
			IResourceFactory factory = GetFactoryFor(netlinkurl);
			return LoadAndPlay(factory, data);
		}

		/// <summary>
		/// Same as <see cref="LoadAndPlay(PlayData)"/> except it lets you pick an
		/// <see cref="IResourceFactory"/> identifier to manually select a factory.
		/// </summary>
		/// <param name="audioType">The associated <see cref="AudioType"/> to a factory.</param>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R LoadAndPlay(AudioType audioType, PlayData data)
		{
			var factory = GetFactoryFor(audioType);
			return LoadAndPlay(factory, data);
		}

		private R LoadAndPlay(IResourceFactory factory, PlayData data)
		{
			if (data.ResourceData == null)
			{
				string netlinkurl = TextUtil.ExtractUrlFromBB(data.Message);

				var result = factory.GetResource(netlinkurl);
				if (!result)
					return $"Could not play ({result.Message})";
				data.PlayResource = result.Value;
			}
			return PostProcessAndStartInternal(factory, data);
		}

		//public R<PlayResource> Restore(AudioResource resource)
		//	=> RestoreInternal(GetFactoryFor(resource.AudioType), resource);

		private R<PlayResource> RestoreInternal(IResourceFactory factory, AudioResource resource)
		{
			var result = factory.GetResourceById(resource.ResourceId, resource.ResourceTitle);
			if (!result)
				return $"Could not restore ({result.Message})";
			return result;
		}

		/// <summary>
		/// Creates a new <see cref="PlayResource"/> which can be played, but restores values like
		/// title or first invoker from the history database.
		/// </summary>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R RestoreAndPlay(PlayData data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));
			if (data.ResourceData == null)
				throw new ArgumentNullException(nameof(data.ResourceData));

			var factory = GetFactoryFor(data.ResourceData.AudioType);
			var result = RestoreInternal(factory, data.ResourceData);
			if (!result) return result.Message;
			data.PlayResource = result.Value;
			return PostProcessAndStartInternal(factory, data);
		}

		/// <summary>
		/// Invokes postprocess operations for the passed <see cref="PlayData.ResourceData"/> and
		/// the corresponding factory. Starts the resource afterwards if the pp was successful.
		/// </summary>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R PostProcessAndStart(PlayData data)
			=> PostProcessAndStartInternal(GetFactoryFor(data.ResourceData.AudioType), data);

		private R PostProcessAndStartInternal(IResourceFactory factory, PlayData data)
		{
			var result = factory.PostProcess(data);
			if (!result)
				return result.Message;
			else
				return Play(data);
		}

		/// <summary>Playes the passed <see cref="PlayData.PlayResource"/></summary>
		/// <param name="data">The building parameters for the resource.</param>
		/// <returns>Ok if successful, or an error message otherwise.</returns>
		public R Play(PlayData data)
		{
			if (data.Enqueue && audioFramework.IsPlaying)
			{
				playlistManager.AddToPlaylist(data);
				return R.OkR;
			}
			else
			{
				return audioFramework.StartResource(data);
			}
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
