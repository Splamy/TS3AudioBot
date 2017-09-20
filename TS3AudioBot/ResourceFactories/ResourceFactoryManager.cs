// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.ResourceFactories
{
	using CommandSystem;
	using Helper;
	using System;
	using System.Drawing;
	using System.Collections.Generic;
	using System.Reflection;

	public sealed class ResourceFactoryManager : IDisposable
	{
		private const string CmdResPrepath = "from ";
		private const string CmdListPrepath = "list from ";

		public CommandGroup CommandResNode { get; } = new CommandGroup();
		public CommandGroup CommandListNode { get; } = new CommandGroup();
		public IResourceFactory DefaultFactorty { get; internal set; }
		private readonly Dictionary<AudioType, IFactory> allFacories;
		private readonly List<IResourceFactory> resFactories;
		private readonly List<IPlaylistFactory> listFactories;

		public ResourceFactoryManager()
		{
			Util.Init(ref allFacories);
			Util.Init(ref resFactories);
			Util.Init(ref listFactories);
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

			string netlinkurl = TextUtil.ExtractUrlFromBb(message);

			var factory = audioType.HasValue
				? GetFactoryFor(audioType.Value)
				: GetFactoryFor(netlinkurl);

			var result = factory.GetResource(netlinkurl);
			if (!result)
				return $"Could not load ({result.Message})";
			return result;
		}

		private IResourceFactory GetFactoryFor(AudioType audioType)
		{
			if (allFacories.TryGetValue(audioType, out var factory) && factory is IResourceFactory resFactory)
				return resFactory;
			return DefaultFactorty;
		}
		private IResourceFactory GetFactoryFor(string uri)
		{
			foreach (var fac in resFactories)
				if (fac != DefaultFactorty && fac.MatchLink(uri)) return fac;
			return DefaultFactorty;
		}

		public R<Playlist> LoadPlaylistFrom(string message, AudioType? type = null)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentNullException(nameof(message));

			string netlinkurl = TextUtil.ExtractUrlFromBb(message);

			if (type.HasValue)
			{
				foreach (var factory in listFactories)
				{
					if (factory.FactoryFor == type.Value)
						return factory.GetPlaylist(netlinkurl);
				}
				return "There is not factory registered for this type";
			}
			else
			{
				foreach (var factory in listFactories)
				{
					if (factory.MatchLink(netlinkurl))
						return factory.GetPlaylist(netlinkurl);
				}
				return "Unknown playlist type. Please use '!list from <type> <url>' to specify your playlist type.";
			}
		}

		public void AddFactory(IFactory factory, CommandManager cmdMgr)
		{
			allFacories.Add(factory.FactoryFor, factory);
			if (factory is IResourceFactory resFactory)
				AddResFactory(resFactory, cmdMgr);
			if (factory is IPlaylistFactory listFactory)
				AddListFactory(listFactory, cmdMgr);
		}

		public void AddResFactory(IResourceFactory factory, CommandManager cmdMgr)
		{
			resFactories.Add(factory);

			// register factory command node
			var playCommand = new PlayCommand(factory.FactoryFor, CmdResPrepath + factory.SubCommandName);
			cmdMgr.RegisterCommand(playCommand.Command);
		}

		public void AddListFactory(IPlaylistFactory factory, CommandManager cmdMgr)
		{
			listFactories.Add(factory);

			// register factory command node
			var playCommand = new PlayListCommand(factory.FactoryFor, CmdListPrepath + factory.SubCommandName);
			cmdMgr.RegisterCommand(playCommand.Command);
		}

		public string RestoreLink(AudioResource res)
		{
			var factory = GetFactoryFor(res.AudioType);
			return factory.RestoreLink(res.ResourceId);
		}

		public R<Image> GetThumbnail(PlayResource playResource)
		{
			if (allFacories.TryGetValue(playResource.BaseData.AudioType, out var factory)
				&& factory is IThumbnailFactory thumbFactory)
				return thumbFactory.GetThumbnail(playResource);
			return "No matching thumbnail factory found";
		}

		public void Dispose()
		{
			foreach (var fac in allFacories.Values)
				fac.Dispose();
			allFacories.Clear();
		}

		private sealed class PlayCommand
		{
			public BotCommand Command { get; }
			private readonly AudioType audioType;
			private static readonly MethodInfo PlayMethod = typeof(PlayCommand).GetMethod(nameof(PropagiatePlay));

			public PlayCommand(AudioType audioType, string cmdPath)
			{
				this.audioType = audioType;
				var builder = new CommandBuildInfo(
					this,
					PlayMethod,
					new CommandAttribute(cmdPath),
					null);
				Command = new BotCommand(builder);
			}

			public string PropagiatePlay(ExecutionInformation info, string parameter)
			{
				return info.Session.Bot.PlayManager.Play(info.InvokerData, parameter, audioType);
			}
		}

		private sealed class PlayListCommand
		{
			public BotCommand Command { get; }
			private readonly AudioType audioType;
			private static readonly MethodInfo PlayMethod = typeof(PlayListCommand).GetMethod(nameof(PropagiateLoad));

			public PlayListCommand(AudioType audioType, string cmdPath)
			{
				this.audioType = audioType;
				var builder = new CommandBuildInfo(
					this,
					PlayMethod,
					new CommandAttribute(cmdPath),
					null);
				Command = new BotCommand(builder);
			}

			public string PropagiateLoad(ExecutionInformation info, string parameter)
			{
				var result = info.Session.Bot.FactoryManager.LoadPlaylistFrom(parameter, audioType);

				if (!result)
					return result;

				result.Value.CreatorDbId = info.InvokerData.DatabaseId;
				info.Session.Set<PlaylistManager, Playlist>(result.Value);
				return "Ok";
			}
		}
	}
}
