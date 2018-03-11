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
	using Sessions;
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Linq;
	using System.Reflection;

	public sealed class ResourceFactoryManager : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const string CmdResPrepath = "from ";
		private const string CmdListPrepath = "list from ";

		public ConfigFile Config { get; set; }
		public CommandManager CommandManager { get; set; }
		public Rights.RightsManager RightsManager { get; set; }

		private readonly Dictionary<string, FactoryData> allFacories;
		private readonly List<IPlaylistFactory> listFactories;
		private readonly List<IResourceFactory> resFactories;

		public ResourceFactoryManager()
		{
			Util.Init(out allFacories);
			Util.Init(out resFactories);
			Util.Init(out listFactories);
		}

		public void Initialize()
		{
			var yfd = Config.GetDataStruct<YoutubeFactoryData>("YoutubeFactory", true);
			var mfd = Config.GetDataStruct<MediaFactoryData>("MediaFactory", true);

			AddFactory(new MediaFactory(mfd));
			AddFactory(new YoutubeFactory(yfd));
			AddFactory(new SoundcloudFactory());
			AddFactory(new TwitchFactory());
			AddFactory(new BandcampFactory());
		}

		// Load lookup stages
		// PlayResource != null    => ret PlayResource
		// ResourceData != null    => call RF.RestoreFromId
		// TextMessage != null     => call RF.GetResoruce
		// else                    => ret Error

		private T GetFactoryByType<T>(string audioType) where T : class, IFactory =>
			// ToLower for legacy reasons
			allFacories.TryGetValue(audioType.ToLowerInvariant(), out var factoryInfo) && factoryInfo.Factory is T factory
				? factory
				: null;

		private IEnumerable<(IResourceFactory, MatchCertainty)> GetResFactoryByLink(string uri) =>
			from fac in resFactories
			let facCertain = fac.MatchResource(uri)
			where facCertain != MatchCertainty.Never
			orderby facCertain descending
			select (fac, facCertain);

		private IEnumerable<(IPlaylistFactory, MatchCertainty)> GetListFactoryByLink(string uri) =>
			from fac in listFactories
			let facCertain = fac.MatchPlaylist(uri)
			where facCertain != MatchCertainty.Never
			orderby facCertain descending
			select (fac, facCertain);

		private static IEnumerable<T> FilterUsable<T>(IEnumerable<(T, MatchCertainty)> enu)
		{
			var highestCertainty = MatchCertainty.Never;
			foreach (var (fac, cert) in enu)
			{
				if ((highestCertainty == MatchCertainty.Always && cert < MatchCertainty.Always)
					|| (highestCertainty > MatchCertainty.Never && cert <= MatchCertainty.OnlyIfLast))
					yield break;

				yield return fac;

				if (cert > highestCertainty)
					highestCertainty = cert;
			}
		}

		/// <summary>Generates a new <see cref="PlayResource"/> which can be played.</summary>
		/// <param name="resource">An <see cref="AudioResource"/> with at least
		/// <see cref="AudioResource.AudioType"/> and<see cref="AudioResource.ResourceId"/> set.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource> Load(AudioResource resource)
		{
			if (resource == null)
				throw new ArgumentNullException(nameof(resource));

			var factory = GetFactoryByType<IResourceFactory>(resource.AudioType);
			if (factory == null)
				return $"Could not load (No registered factory for \"{resource.AudioType}\" found)";

			var result = factory.GetResourceById(resource);
			if (!result)
				return $"Could not load ({result.Error})";
			return result;
		}

		/// <summary>Generates a new <see cref="PlayResource"/> which can be played.
		/// The message used will be cleared of bb-tags. Also lets you pick an
		/// <see cref="IResourceFactory"/> identifier to optionally select a factory.
		/// </summary>
		/// <param name="message">The link/uri to resolve for the resource.</param>
		/// <param name="audioType">The associated resource type string to a factory.
		/// Leave null to let it detect automatically.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource> Load(string message, string audioType = null)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentNullException(nameof(message));

			var netlinkurl = TextUtil.ExtractUrlFromBb(message);

			if (audioType != null)
			{
				var factory = GetFactoryByType<IResourceFactory>(audioType);
				if (factory == null)
					return $"Could not load (No registered factory for \"{audioType}\" found)";

				var result = factory.GetResource(netlinkurl);
				if (!result)
					return $"Could not load ({result.Error})";
				return result;
			}

			var factories = FilterUsable(GetResFactoryByLink(netlinkurl));
			List<(string, string)> errors = null;
			foreach (var factory in factories)
			{
				var result = factory.GetResource(netlinkurl);
				Log.Trace("ResFactory {0} tried, result: {1}", factory.FactoryFor, result.Ok ? "Ok" : result.Error);
				if (result)
					return result;
				(errors = errors ?? new List<(string, string)>()).Add((factory.FactoryFor, result.Error));
			}

			return ToErrorString(errors);
		}

		public R<Playlist> LoadPlaylistFrom(string message) => LoadPlaylistFrom(message, null);

		private R<Playlist> LoadPlaylistFrom(string message, IPlaylistFactory listFactory)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentNullException(nameof(message));

			string netlinkurl = TextUtil.ExtractUrlFromBb(message);

			if (listFactory != null)
				return listFactory.GetPlaylist(netlinkurl);

			var factories = FilterUsable(GetListFactoryByLink(netlinkurl));
			List<(string, string)> errors = null;
			foreach (var factory in factories)
			{
				var result = factory.GetPlaylist(netlinkurl);
				Log.Trace("ListFactory {0} tried, result: {1}", factory.FactoryFor, result.Ok ? "Ok" : result.Error);
				if (result)
					return result;
				(errors = errors ?? new List<(string, string)>()).Add((factory.FactoryFor, result.Error));
			}

			return ToErrorString(errors);
		}

		public string RestoreLink(AudioResource res)
		{
			var factory = GetFactoryByType<IResourceFactory>(res.AudioType);
			return factory.RestoreLink(res.ResourceId);
		}

		public R<Image> GetThumbnail(PlayResource playResource)
		{
			var factory = GetFactoryByType<IThumbnailFactory>(playResource.BaseData.AudioType);
			if (factory == null)
				return "No thumbnail factory found";

			return factory.GetThumbnail(playResource);
		}

		public void AddFactory(IFactory factory)
		{
			if (factory.FactoryFor.ToLowerInvariant() != factory.FactoryFor)
				throw new ArgumentException($"The factory audio type \"{nameof(IFactory.FactoryFor)}\" must be in lower case.", nameof(factory));
			if (allFacories.ContainsKey(factory.FactoryFor))
				throw new ArgumentException("A factory for this type already has been registered.", nameof(factory));

			var commands = new List<FactoryCommand>();
			if (factory is IResourceFactory resFactory)
			{
				commands.Add(new PlayCommand(factory.FactoryFor, CmdResPrepath + resFactory.FactoryFor));
				resFactories.Add(resFactory);
			}
			if (factory is IPlaylistFactory listFactory)
			{
				commands.Add(new PlayListCommand(listFactory, CmdListPrepath + listFactory.FactoryFor));
				listFactories.Add(listFactory);
			}

			var factoryInfo = new FactoryData(factory, commands.ToArray());
			allFacories.Add(factory.FactoryFor, factoryInfo);
			CommandManager.RegisterCollection(factoryInfo);
			RightsManager.RegisterRights(factoryInfo.ExposedRights);
		}

		public void RemoveFactory(IFactory factory)
		{
			if (!allFacories.TryGetValue(factory.FactoryFor, out var factoryInfo))
				return;

			allFacories.Remove(factory.FactoryFor);

			if (factory is IResourceFactory resFactory)
				resFactories.Remove(resFactory);
			if (factory is IPlaylistFactory listFactory)
				listFactories.Remove(listFactory);

			CommandManager.UnregisterCollection(factoryInfo);
			RightsManager.UnregisterRights(factoryInfo.ExposedRights);
		}

		private static string ToErrorString(IReadOnlyList<(string fact, string err)> errors)
		{
			if (errors.Count == 0)
				return "Could not load (The bot is stupid)";
			if (errors.Count == 1)
				return $"Could not load ({errors[0].fact}: {errors[0].err})";
			return "Could not load (Considered multiple factories but all failed. Try selecting one manually with !from <factory> <link>)";
		}

		public void Dispose()
		{
			foreach (var factoryInfo in allFacories.Values)
				factoryInfo.Factory.Dispose();
			allFacories.Clear();
		}


		private sealed class FactoryData : ICommandBag
		{
			private readonly FactoryCommand[] registeredCommands;

			public FactoryData(IFactory factory, FactoryCommand[] commands)
			{
				Factory = factory;
				registeredCommands = commands;
			}

			public IFactory Factory { get; }
			public IEnumerable<BotCommand> ExposedCommands => registeredCommands.Select(x => x.Command);
			public IEnumerable<string> ExposedRights => ExposedCommands.Select(x => x.RequiredRight);
		}

		private abstract class FactoryCommand
		{
			public BotCommand Command { get; protected set; }
		}

		private sealed class PlayCommand : FactoryCommand
		{
			private static readonly MethodInfo PlayMethod = typeof(PlayCommand).GetMethod(nameof(PropagiatePlay));
			private readonly string audioType;

			public PlayCommand(string audioType, string cmdPath)
			{
				this.audioType = audioType;
				var builder = new CommandBuildInfo(
					this,
					PlayMethod,
					new CommandAttribute(cmdPath));
				Command = new BotCommand(builder);
			}

			public string PropagiatePlay(PlayManager playManager, InvokerData invoker, string parameter)
			{
				return playManager.Play(invoker, parameter, audioType);
			}
		}

		private sealed class PlayListCommand : FactoryCommand
		{
			private static readonly MethodInfo PlayMethod = typeof(PlayListCommand).GetMethod(nameof(PropagiateLoad));
			private readonly IPlaylistFactory factory;

			public PlayListCommand(IPlaylistFactory factory, string cmdPath)
			{
				this.factory = factory;
				var builder = new CommandBuildInfo(
					this,
					PlayMethod,
					new CommandAttribute(cmdPath));
				Command = new BotCommand(builder);
			}

			public string PropagiateLoad(ResourceFactoryManager factoryManager, UserSession session, InvokerData invoker, string parameter)
			{
				var result = factoryManager.LoadPlaylistFrom(parameter, factory);

				if (!result)
					return result;

				result.Value.CreatorDbId = invoker.DatabaseId;
				session.Set<PlaylistManager, Playlist>(result.Value);
				return "Ok";
			}
		}
	}
}
