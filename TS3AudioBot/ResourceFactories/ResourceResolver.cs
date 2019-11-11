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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.CommandSystem.Text;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.Sessions;
using TS3AudioBot.Web.Api;

namespace TS3AudioBot.ResourceFactories
{
	public sealed class ResourceResolver : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const string CmdResPrepath = "from ";
		private const string CmdListPrepath = "list from ";
		private const string CmdSearchPrepath = "search from ";

		private readonly Dictionary<string, ResolverData> allResolvers = new Dictionary<string, ResolverData>();
		private readonly List<IPlaylistResolver> listResolvers = new List<IPlaylistResolver>();
		private readonly List<IResourceResolver> resResolvers = new List<IResourceResolver>();
		private readonly List<ISearchResolver> searchResolvers = new List<ISearchResolver>();
		private readonly CommandManager commandManager;

		public ResourceResolver(ConfFactories config, CommandManager commandManager)
		{
			this.commandManager = commandManager;

			AddResolver(new MediaResolver(config.Media));
			AddResolver(new YoutubeResolver());
			AddResolver(new SoundcloudResolver());
			AddResolver(new TwitchResolver());
			AddResolver(new BandcampResolver());
		}

		private T GetResolverByType<T>(string audioType) where T : class, IResolver =>
			// ToLower for legacy reasons
			allResolvers.TryGetValue(audioType.ToLowerInvariant(), out var ResolverInfo) && ResolverInfo.Resolver is T resolver
				? resolver
				: null;

		private IEnumerable<(IResourceResolver, MatchCertainty)> GetResResolverByLink(string uri) =>
			from rsv in resResolvers
			let rsvCertain = rsv.MatchResource(uri)
			where rsvCertain != MatchCertainty.Never
			orderby rsvCertain descending
			select (rsv, rsvCertain);

		private IEnumerable<(IPlaylistResolver, MatchCertainty)> GetListResolverByLink(string uri) =>
			from rsv in listResolvers
			let rsvCertain = rsv.MatchPlaylist(uri)
			where rsvCertain != MatchCertainty.Never
			orderby rsvCertain descending
			select (rsv, rsvCertain);

		private static IEnumerable<T> FilterUsable<T>(IEnumerable<(T, MatchCertainty)> enu)
		{
			var highestCertainty = MatchCertainty.Never;
			foreach (var (rsv, cert) in enu)
			{
				if ((highestCertainty == MatchCertainty.Always && cert < MatchCertainty.Always)
					|| (highestCertainty > MatchCertainty.Never && cert <= MatchCertainty.OnlyIfLast))
					yield break;

				yield return rsv;

				if (cert > highestCertainty)
					highestCertainty = cert;
			}
		}

		/// <summary>Generates a new <see cref="PlayResource"/> which can be played.</summary>
		/// <param name="resource">An <see cref="AudioResource"/> with at least
		/// <see cref="AudioResource.AudioType"/> and<see cref="AudioResource.ResourceId"/> set.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource, LocalStr> Load(AudioResource resource)
		{
			if (resource is null)
				throw new ArgumentNullException(nameof(resource));

			var resolver = GetResolverByType<IResourceResolver>(resource.AudioType);
			if (resolver is null)
				return CouldNotLoad(string.Format(strings.error_resfac_no_registered_factory, resource.AudioType));

			var sw = Stopwatch.StartNew();
			R<PlayResource, LocalStr> result;
			try
			{
				result = resolver.GetResourceById(resource);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Resource resolver '{0}' threw while trying to resolve '{@resource}'", resolver.ResolverFor, resource);
				return CouldNotLoad(strings.error_playmgr_internal_error);
			}
			if (!result.Ok)
				return CouldNotLoad(result.Error.Str);
			Log.Debug("Took {0}ms to resolve resource.", sw.ElapsedMilliseconds);
			return result.Value;
		}

		/// <summary>Generates a new <see cref="PlayResource"/> which can be played.
		/// The message used will be cleared of bb-tags. Also lets you pick an
		/// <see cref="IResourceResolver"/> identifier to optionally select a resolver.
		/// </summary>
		/// <param name="message">The link/uri to resolve for the resource.</param>
		/// <param name="audioType">The associated resource type string to a resolver.
		/// Leave null to let it detect automatically.</param>
		/// <returns>The playable resource if successful, or an error message otherwise.</returns>
		public R<PlayResource, LocalStr> Load(string message, string audioType = null)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentNullException(nameof(message));

			var netlinkurl = TextUtil.ExtractUrlFromBb(message);

			if (audioType != null)
			{
				var resolver = GetResolverByType<IResourceResolver>(audioType);
				if (resolver is null)
					return CouldNotLoad(string.Format(strings.error_resfac_no_registered_factory, audioType));

				var result = resolver.GetResource(netlinkurl);
				if (!result.Ok)
					return CouldNotLoad(result.Error.Str);
				return result;
			}

			var sw = Stopwatch.StartNew();
			var resolvers = FilterUsable(GetResResolverByLink(netlinkurl));
			List<(string, LocalStr)> errors = null;
			foreach (var resolver in resolvers)
			{
				var result = resolver.GetResource(netlinkurl);
				Log.Trace("Resolver {0} tried, result: {1}", resolver.ResolverFor, result.Ok ? "Ok" : result.Error.Str);
				if (result)
					return result;
				(errors = errors ?? new List<(string, LocalStr)>()).Add((resolver.ResolverFor, result.Error));
			}
			Log.Debug("Took {0}ms to resolve resource.", sw.ElapsedMilliseconds);

			return ToErrorString(errors);
		}

		public R<Playlist, LocalStr> LoadPlaylistFrom(string message) => LoadPlaylistFrom(message, null);

		private R<Playlist, LocalStr> LoadPlaylistFrom(string message, IPlaylistResolver listResolver)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentNullException(nameof(message));

			string netlinkurl = TextUtil.ExtractUrlFromBb(message);

			if (listResolver != null)
				return listResolver.GetPlaylist(netlinkurl);

			var resolvers = FilterUsable(GetListResolverByLink(netlinkurl));
			List<(string, LocalStr)> errors = null;
			foreach (var resolver in resolvers)
			{
				var result = resolver.GetPlaylist(netlinkurl);
				Log.Trace("ListResolver {0} tried, result: {1}", resolver.ResolverFor, result.Ok ? "Ok" : result.Error.Str);
				if (result)
					return result;
				(errors = errors ?? new List<(string, LocalStr)>()).Add((resolver.ResolverFor, result.Error));
			}

			return ToErrorString(errors);
		}

		public R<string, LocalStr> RestoreLink(AudioResource res)
		{
			var resolver = GetResolverByType<IResourceResolver>(res.AudioType);
			if (resolver is null)
				return CouldNotLoad();
			return resolver.RestoreLink(res);
		}

		public R<Stream, LocalStr> GetThumbnail(PlayResource playResource)
		{
			var resolver = GetResolverByType<IThumbnailResolver>(playResource.BaseData.AudioType);
			if (resolver is null)
				return new LocalStr(string.Format(strings.error_resfac_no_registered_factory, playResource.BaseData.AudioType));

			var sw = Stopwatch.StartNew();
			var result = resolver.GetThumbnail(playResource);
			Log.Debug("Took {0}ms to load thumbnail.", sw.ElapsedMilliseconds);
			return result;
		}

		public void AddResolver(IResolver resolver)
		{
			if (resolver.ResolverFor.ToLowerInvariant() != resolver.ResolverFor)
				throw new ArgumentException($"The resolver audio type \"{nameof(IResolver.ResolverFor)}\" must be in lower case.", nameof(resolver));
			if (allResolvers.ContainsKey(resolver.ResolverFor))
				throw new ArgumentException("A resolver for this type already has been registered.", nameof(resolver));

			var commands = new List<ResolverCommand>();
			if (resolver is IResourceResolver resResolver)
			{
				commands.Add(new PlayCommand(resolver.ResolverFor, CmdResPrepath + resResolver.ResolverFor));
				resResolvers.Add(resResolver);
			}
			if (resolver is IPlaylistResolver listResolver)
			{
				commands.Add(new PlayListCommand(listResolver, CmdListPrepath + listResolver.ResolverFor));
				listResolvers.Add(listResolver);
			}
			if (resolver is ISearchResolver searchResolver)
			{
				commands.Add(new SearchCommand(searchResolver, CmdSearchPrepath + searchResolver.ResolverFor));
				searchResolvers.Add(searchResolver);
			}

			var resolverInfo = new ResolverData(resolver, commands.ToArray());
			allResolvers.Add(resolver.ResolverFor, resolverInfo);
			commandManager.RegisterCollection(resolverInfo);
		}

		public void RemoveResolver(IResolver Resolver)
		{
			if (!allResolvers.TryGetValue(Resolver.ResolverFor, out var resolverInfo))
				return;

			allResolvers.Remove(Resolver.ResolverFor);

			if (Resolver is IResourceResolver resResolver)
				resResolvers.Remove(resResolver);
			if (Resolver is IPlaylistResolver listResolver)
				listResolvers.Remove(listResolver);
			if (Resolver is ISearchResolver searchResolver)
				searchResolvers.Remove(searchResolver);

			commandManager.UnregisterCollection(resolverInfo);
		}

		private static LocalStr CouldNotLoad(string reason = null)
		{
			if (reason is null)
				return new LocalStr(strings.error_resfac_could_not_load);
			var strb = new StringBuilder(strings.error_resfac_could_not_load);
			strb.Append(" (").Append(reason).Append(")");
			return new LocalStr(strb.ToString());
		}

		private static LocalStr ToErrorString(List<(string rsv, LocalStr err)> errors)
		{
			if (errors is null || errors.Count == 0)
				throw new ArgumentException("No errors provided", nameof(errors));
			if (errors.Count == 1)
				return CouldNotLoad($"{errors[0].rsv}: {errors[0].err}");
			return CouldNotLoad(strings.error_resfac_multiple_factories_failed);
		}

		public void Dispose()
		{
			foreach (var resolverInfo in allResolvers.Values)
				resolverInfo.Resolver.Dispose();
			allResolvers.Clear();
		}

		private sealed class ResolverData : ICommandBag
		{
			private readonly ResolverCommand[] registeredCommands;

			public IResolver Resolver { get; }
			public IReadOnlyCollection<BotCommand> BagCommands { get; }
			public IReadOnlyCollection<string> AdditionalRights => Array.Empty<string>();

			public ResolverData(IResolver resolver, ResolverCommand[] commands)
			{
				Resolver = resolver;
				registeredCommands = commands;
				BagCommands = registeredCommands.Select(x => x.Command).ToArray();
			}
		}

		private abstract class ResolverCommand
		{
			public BotCommand Command { get; protected set; }
		}

		private sealed class PlayCommand : ResolverCommand
		{
			private static readonly MethodInfo Method = typeof(PlayCommand).GetMethod(nameof(PropagiatePlay));
			private readonly string audioType;

			public PlayCommand(string audioType, string cmdPath)
			{
				this.audioType = audioType;
				var builder = new CommandBuildInfo(
					this,
					Method,
					new CommandAttribute(cmdPath));
				Command = new BotCommand(builder);
			}

			public void PropagiatePlay(PlayManager playManager, InvokerData invoker, string url)
			{
				playManager.Play(invoker, url, audioType).UnwrapThrow();
			}
		}

		private sealed class PlayListCommand : ResolverCommand
		{
			private static readonly MethodInfo Method = typeof(PlayListCommand).GetMethod(nameof(PropagiateLoad));
			private readonly IPlaylistResolver resolver;

			public PlayListCommand(IPlaylistResolver resolver, string cmdPath)
			{
				this.resolver = resolver;
				var builder = new CommandBuildInfo(
					this,
					Method,
					new CommandAttribute(cmdPath));
				Command = new BotCommand(builder);
			}

			public void PropagiateLoad(ResourceResolver resourceResolver, UserSession session, string url)
			{
				var playlist = resourceResolver.LoadPlaylistFrom(url, resolver).UnwrapThrow();

				session.Set(SessionConst.Playlist, playlist);
			}
		}

		private sealed class SearchCommand : ResolverCommand
		{
			private static readonly MethodInfo Method = typeof(SearchCommand).GetMethod(nameof(PropagiateSearch));
			private readonly ISearchResolver resolver;

			public SearchCommand(ISearchResolver resolver, string cmdPath)
			{
				this.resolver = resolver;
				var builder = new CommandBuildInfo(
					this,
					Method,
					new CommandAttribute(cmdPath));
				Command = new BotCommand(builder);
			}

			public JsonArray<AudioResource> PropagiateSearch(UserSession session, CallerInfo callerInfo, string keyword)
			{
				var result = resolver.Search(keyword);
				var list = result.UnwrapThrow();
				session.Set(SessionConst.SearchResult, list);

				return new JsonArray<AudioResource>(list, searchResults =>
				{
					if (searchResults.Count == 0)
						return strings.cmd_search_no_result;

					var tmb = new TextModBuilder(callerInfo.IsColor);
					tmb.AppendFormat(
						strings.cmd_search_header.Mod().Bold(),
						("!search play " + strings.info_number).Mod().Italic(),
						("!search add " + strings.info_number).Mod().Italic()).Append("\n");
					for (int i = 0; i < searchResults.Count; i++)
					{
						tmb.AppendFormat("{0}: {1}\n", i.ToString().Mod().Bold(), searchResults[i].ResourceTitle);
					}

					return tmb.ToString();
				});
			}
		}
	}
}
