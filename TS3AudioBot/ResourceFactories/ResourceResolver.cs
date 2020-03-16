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
using System.Text;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories.Youtube;

namespace TS3AudioBot.ResourceFactories
{
	public sealed class ResourceResolver : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly Dictionary<string, IResolver> allResolvers = new Dictionary<string, IResolver>();
		private readonly List<IPlaylistResolver> listResolvers = new List<IPlaylistResolver>();
		private readonly List<IResourceResolver> resResolvers = new List<IResourceResolver>();
		private readonly List<ISearchResolver> searchResolvers = new List<ISearchResolver>();

		public ResourceResolver(ConfFactories conf)
		{
			AddResolver(new MediaResolver());
			AddResolver(new YoutubeResolver(conf));
			AddResolver(new SoundcloudResolver());
			AddResolver(new TwitchResolver());
			AddResolver(new BandcampResolver());
		}

		private T GetResolverByType<T>(string audioType) where T : class, IResolver =>
			// ToLower for legacy reasons
			allResolvers.TryGetValue(audioType.ToLowerInvariant(), out var resolver) && resolver is T resolverT
				? resolverT
				: null;

		private IEnumerable<(IResourceResolver, MatchCertainty)> GetResResolverByLink(ResolveContext ctx, string uri) =>
			from rsv in resResolvers
			let rsvCertain = rsv.MatchResource(ctx, uri)
			where rsvCertain != MatchCertainty.Never
			orderby rsvCertain descending
			select (rsv, rsvCertain);

		private IEnumerable<(IPlaylistResolver, MatchCertainty)> GetListResolverByLink(ResolveContext ctx, string uri) =>
			from rsv in listResolvers
			let rsvCertain = rsv.MatchPlaylist(ctx, uri)
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
		public R<PlayResource, LocalStr> Load(ResolveContext ctx, AudioResource resource)
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
				result = resolver.GetResourceById(ctx, resource);
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
		public R<PlayResource, LocalStr> Load(ResolveContext ctx, string message, string audioType = null)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentNullException(nameof(message));

			var netlinkurl = TextUtil.ExtractUrlFromBb(message);

			if (audioType != null)
			{
				var resolver = GetResolverByType<IResourceResolver>(audioType);
				if (resolver is null)
					return CouldNotLoad(string.Format(strings.error_resfac_no_registered_factory, audioType));

				var result = resolver.GetResource(ctx, netlinkurl);
				if (!result.Ok)
					return CouldNotLoad(result.Error.Str);
				return result;
			}

			var sw = Stopwatch.StartNew();
			var resolvers = FilterUsable(GetResResolverByLink(ctx, netlinkurl));
			List<(string, LocalStr)> errors = null;
			foreach (var resolver in resolvers)
			{
				var result = resolver.GetResource(ctx, netlinkurl);
				Log.Trace("Resolver {0} tried, result: {1}", resolver.ResolverFor, result.Ok ? "Ok" : result.Error.Str);
				if (result)
					return result;
				(errors = errors ?? new List<(string, LocalStr)>()).Add((resolver.ResolverFor, result.Error));
			}
			Log.Debug("Took {0}ms to resolve resource.", sw.ElapsedMilliseconds);

			return ToErrorString(errors);
		}

		public R<Playlist, LocalStr> LoadPlaylistFrom(ResolveContext ctx, string message, string audioType = null)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentNullException(nameof(message));

			string netlinkurl = TextUtil.ExtractUrlFromBb(message);

			if (audioType != null)
			{
				var resolver = GetResolverByType<IPlaylistResolver>(audioType);
				if (resolver is null)
					return CouldNotLoad(string.Format(strings.error_resfac_no_registered_factory, audioType));

				var result = resolver.GetPlaylist(ctx, netlinkurl);
				if (!result.Ok)
					return CouldNotLoad(result.Error.Str);
				return result;
			}

			var resolvers = FilterUsable(GetListResolverByLink(ctx, netlinkurl));
			List<(string, LocalStr)> errors = null;
			foreach (var resolver in resolvers)
			{
				var result = resolver.GetPlaylist(ctx, netlinkurl);
				Log.Trace("ListResolver {0} tried, result: {1}", resolver.ResolverFor, result.Ok ? "Ok" : result.Error.Str);
				if (result)
					return result;
				(errors = errors ?? new List<(string, LocalStr)>()).Add((resolver.ResolverFor, result.Error));
			}

			return ToErrorString(errors);
		}

		public R<string, LocalStr> RestoreLink(ResolveContext ctx, AudioResource res)
		{
			var resolver = GetResolverByType<IResourceResolver>(res.AudioType);
			if (resolver is null)
				return CouldNotLoad();
			return resolver.RestoreLink(ctx, res);
		}

		public R<Stream, LocalStr> GetThumbnail(ResolveContext ctx, PlayResource playResource)
		{
			var resolver = GetResolverByType<IThumbnailResolver>(playResource.BaseData.AudioType);
			if (resolver is null)
				return new LocalStr(string.Format(strings.error_resfac_no_registered_factory, playResource.BaseData.AudioType));

			var sw = Stopwatch.StartNew();
			var result = resolver.GetThumbnail(ctx, playResource);
			Log.Debug("Took {0}ms to load thumbnail.", sw.ElapsedMilliseconds);
			return result;
		}

		public R<IList<AudioResource>, LocalStr> Search(ResolveContext ctx, string resolverName, string query)
		{
			var resolver = GetResolverByType<ISearchResolver>(resolverName);
			if (resolver is null)
				return CouldNotLoad(string.Format(strings.error_resfac_no_registered_factory, resolverName));
			return resolver.Search(ctx, query);
		}

		public void AddResolver(IResolver resolver)
		{
			if (resolver.ResolverFor.ToLowerInvariant() != resolver.ResolverFor)
				throw new ArgumentException($"The resolver audio type \"{nameof(IResolver.ResolverFor)}\" must be in lower case.", nameof(resolver));
			if (allResolvers.ContainsKey(resolver.ResolverFor))
				throw new ArgumentException("A resolver for this type already has been registered.", nameof(resolver));

			if (resolver is IResourceResolver resResolver)
			{
				resResolvers.Add(resResolver);
			}
			if (resolver is IPlaylistResolver listResolver)
			{
				listResolvers.Add(listResolver);
			}
			if (resolver is ISearchResolver searchResolver)
			{
				searchResolvers.Add(searchResolver);
			}

			allResolvers.Add(resolver.ResolverFor, resolver);
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
			foreach (var resolver in allResolvers.Values)
				resolver.Dispose();
			allResolvers.Clear();
		}
	}
}
