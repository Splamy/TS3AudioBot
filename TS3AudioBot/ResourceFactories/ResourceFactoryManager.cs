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
	using CommandSystem;
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Reflection;

	public sealed class ResourceFactoryManager : IDisposable
	{
		private const string cmdPrepath = "from ";

		public CommandGroup CommandNode { get; } = new CommandGroup();
		public IResourceFactory DefaultFactorty { get; internal set; }
		private List<IResourceFactory> factories;

		public ResourceFactoryManager()
		{
			Util.Init(ref factories);
		}

		// Load lookup stages
		// PlayResource != null    => ret PlayResource
		// ResourceData != null    => call RF.RestoreFromId
		// TextMessage != null     => call RF.GetResoruce
		// else                    => ret Error

		/// <summary>Generates a new <see cref="PlayResource"/> which can be played.</summary>
		/// <param name="resource">An <see cref="AudioResource"/> with at least
		/// <see cref="AudioResource.AudioType"/> and<see cref="AudioResource.ResourceId"/> set.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource> Load(AudioResource resource)
		{
			if (resource == null)
				throw new ArgumentNullException(nameof(resource));

			IResourceFactory factory = GetFactoryFor(resource.AudioType);

			var result = factory.GetResourceById(resource);
			if (!result)
				return $"Could not load ({result.Message})";
			return result;
		}

		/// <summary>Generates a new <see cref="PlayResource"/> which can be played.
		/// The message used will be cleared of bb-tags. Also lets you pick an
		/// <see cref="IResourceFactory"/> identifier to optionally select a factory.
		/// </summary>
		/// <param name="message">The link/uri to resolve for the resource.</param>
		/// <param name="audioType">The associated <see cref="AudioType"/> to a factory.
		/// Leave null to let it detect automatically.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource> Load(string message, AudioType? audioType = null)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentNullException(nameof(message));

			IResourceFactory factory;
			string netlinkurl = TextUtil.ExtractUrlFromBB(message);

			if (audioType.HasValue)
				factory = GetFactoryFor(audioType.Value);
			else
				factory = GetFactoryFor(netlinkurl);

			var result = factory.GetResource(netlinkurl);
			if (!result)
				return $"Could not load ({result.Message})";
			return result;
		}

		private IResourceFactory GetFactoryFor(AudioType audioType)
		{
			foreach (var fac in factories)
				if (fac != DefaultFactorty && fac.FactoryFor == audioType) return fac;
			return DefaultFactorty;
		}
		private IResourceFactory GetFactoryFor(string uri)
		{
			foreach (var fac in factories)
				if (fac != DefaultFactorty && fac.MatchLink(uri)) return fac;
			return DefaultFactorty;
		}

		public void AddFactory(IResourceFactory factory, CommandManager cmdMgr)
		{
			factories.Add(factory);

			// register factory command node
			var playCommand = new PlayCommand(factory.FactoryFor, cmdPrepath + factory.SubCommandName);
			cmdMgr.RegisterCommand(playCommand.Command);
		}

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

		sealed class PlayCommand
		{
			public BotCommand Command { get; }
			private AudioType audioType;
			private static readonly MethodInfo playMethod = typeof(PlayCommand).GetMethod(nameof(PropagiatePlay));

			public PlayCommand(AudioType audioType, string cmdPath)
			{
				this.audioType = audioType;
				var builder = new CommandBuildInfo(
					this,
					playMethod,
					new CommandAttribute(cmdPath),
					null);
				Command = new BotCommand(builder);
			}

			public string PropagiatePlay(ExecutionInformation info, string parameter)
			{
				return info.Session.Bot.PlayManager.Play(new InvokerData(info.Session.Client), parameter, audioType);
			}
		}
	}
}
