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
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories.AudioTags;
using TSLib.Helper;

namespace TS3AudioBot.ResourceFactories.Youtube;

public sealed partial class YoutubeResolver : IResourceResolver, IPlaylistResolver, IThumbnailResolver, ISearchResolver
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	[GeneratedRegex(@"(?:(?:&|\?)v=|youtu\.be\/|youtube\.com\/shorts\/)([\w\-_]{11})", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
	private static partial Regex IdMatch { get; }
	[GeneratedRegex(@"(?:&|\?)t=([0-9]+)", RegexOptions.IgnoreCase)]
	private static partial Regex YtTimestampMatch { get; }
	[GeneratedRegex(@"^(https?\:\/\/)?(www\.|m\.)?(youtube\.|youtu\.be)", RegexOptions.IgnoreCase)]
	private static partial Regex LinkMatch { get; }
	[GeneratedRegex(@"(&|\?)list=([\w\-_]+)", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
	private static partial Regex ListMatch { get; }
	[GeneratedRegex(@"CODECS=""([^""]*)""", RegexOptions.IgnoreCase)]
	private static partial Regex StreamCodecMatch { get; }
	[GeneratedRegex(@"BANDWIDTH=([0-9]+)", RegexOptions.IgnoreCase)]
	private static partial Regex StreamBitrateMatch { get; }
	private string YoutubeProjectId => conf.ApiKey.Value;
	private readonly ConfResolverYoutube conf;

	public YoutubeResolver(ConfResolverYoutube conf)
	{
		this.conf = conf;
	}

	public string ResolverFor => "youtube";

	public MatchCertainty MatchResource(ResolveContext? _, string uri) =>
		LinkMatch.IsMatch(uri) || IdMatch.IsMatch(uri)
			? MatchCertainty.Always
			: MatchCertainty.Never;

	public MatchCertainty MatchPlaylist(ResolveContext? _, string uri) => ListMatch.IsMatch(uri) ? MatchCertainty.Always : MatchCertainty.Never;

	public async Task<PlayResource> GetResource(ResolveContext? _, string uri, CancellationToken cancellationToken)
	{
		Match matchYtId = IdMatch.Match(uri);
		if (!matchYtId.Success)
			throw Error.LocalStr(strings.error_media_failed_to_parse_id);

		var play = await GetResourceById(null, new AudioResource(matchYtId.Groups[1].Value, null, ResolverFor), cancellationToken);
		Match matchTimestamp = YtTimestampMatch.Match(uri);
		if (matchYtId.Success && int.TryParse(matchTimestamp.Groups[1].Value, out var secs))
		{
			play.PlayInfo ??= new PlayInfo();
			play.PlayInfo.StartOffset = TimeSpan.FromSeconds(secs);
		}
		return play;
	}

	public async Task<PlayResource> GetResourceById(ResolveContext? _, AudioResource resource, CancellationToken cancellationToken)
	{
		return await YoutubeDlWrapped(resource, cancellationToken);
	}

	public string RestoreLink(ResolveContext _, AudioResource resource) => "https://youtu.be/" + resource.ResourceId;

	public async Task<Playlist> GetPlaylist(ResolveContext _, string url, CancellationToken cancellationToken)
	{
		Match matchYtId = ListMatch.Match(url);
		if (!matchYtId.Success)
			throw Error.LocalStr(strings.error_media_failed_to_parse_id);

		string id = matchYtId.Groups[2].Value;
		if (string.IsNullOrEmpty(YoutubeProjectId))
			return await GetPlaylistYoutubeDl(id, url, cancellationToken);
		else
			return await GetPlaylistYoutubeApi(id, cancellationToken);
	}

	private async Task<Playlist> GetPlaylistYoutubeApi(string id, CancellationToken cancellationToken)
	{
		var plist = new Playlist().SetTitle(id);

		string? nextToken = null;
		do
		{
			var parsed = await WebWrapper.Request("https://www.googleapis.com/youtube/v3/playlistItems"
					+ "?part=contentDetails,snippet"
					+ "&fields=" + Uri.EscapeDataString("items(contentDetails/videoId,snippet/title),nextPageToken")
					+ "&maxResults=50"
					+ "&playlistId=" + id
					+ (nextToken != null ? "&pageToken=" + nextToken : string.Empty)
					+ "&key=" + YoutubeProjectId).AsJson<JsonVideoListResponse>(cancellationToken);

			if (parsed.items is null) { Log.Debug("Breaking on items:null"); break; }
			var videoItems = parsed.items;
			if (!plist.AddRange(
				videoItems.Select(item =>
					new PlaylistItem(
						new AudioResource(
							item.contentDetails?.videoId ?? throw new NullReferenceException("item.contentDetails.videoId was null"),
							item.snippet?.title,
							ResolverFor
						)
					)
				)
			)) break;

			nextToken = parsed.nextPageToken;
		} while (nextToken != null);

		return plist;
	}

	private async Task<Playlist> GetPlaylistYoutubeDl(string id, string url, CancellationToken cancellationToken)
	{
		var plistData = await YoutubeDlHelper.GetPlaylistAsync(url, cancellationToken);
		var plist = new Playlist().SetTitle(plistData.title ?? $"youtube-{id}");
		if (plistData.entries is null) { Log.Debug("Youtube-dl returned entries:null"); return plist; }

		plist.AddRange(plistData.entries
			.Where(entry => entry.id != null)
			.Select(entry => new PlaylistItem(
				new AudioResource(
					entry.id!,
					entry.title,
					ResolverFor
				)
			)));

		return plist;
	}

	private static async Task<PlayResource> YoutubeDlWrapped(AudioResource resource, CancellationToken cancellationToken)
	{
		Log.Debug("Falling back to youtube-dl!");

		var response = await YoutubeDlHelper.GetSingleVideo(resource.ResourceId, cancellationToken);
		resource.ResourceTitle = response.AutoTitle ?? $"Youtube-{resource.ResourceId}";
		var songInfo = YoutubeDlHelper.MapToSongInfo(response);
		var format = YoutubeDlHelper.FilterBest(response.formats);
		var url = format?.url;

		if (string.IsNullOrEmpty(url))
			throw Error.LocalStr(strings.error_ytdl_empty_response);

		Log.Debug("youtube-dl succeeded!");
		return new PlayResource(url, resource, songInfo: songInfo);
	}

	public Task GetThumbnail(ResolveContext _, PlayResource playResource, AsyncStreamAction action, CancellationToken cancellationToken)
	{
		// default  :  120px/ 90px /default.jpg
		// medium   :  320px/180px /mqdefault.jpg
		// high     :  480px/360px /hqdefault.jpg
		// standard :  640px/480px /sddefault.jpg
		// maxres   : 1280px/720px /maxresdefault.jpg
		return WebWrapper
			.Request($"https://i.ytimg.com/vi/{playResource.AudioResource.ResourceId}/mqdefault.jpg")
			.ToStream(action, cancellationToken);
	}

	public async Task<IList<AudioResource>> Search(ResolveContext _, string keyword, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(YoutubeProjectId))
			return await SearchYoutubeDlAsync(keyword, cancellationToken);
		else
			return await SearchYoutubeApi(keyword, cancellationToken);
	}

	public async Task<IList<AudioResource>> SearchYoutubeApi(string keyword, CancellationToken cancellationToken)
	{
		const int maxResults = 10;
		var parsed = await WebWrapper.Request(
				"https://www.googleapis.com/youtube/v3/search"
				+ "?part=snippet"
				+ "&fields=" + Uri.EscapeDataString("items(id/videoId,snippet(channelTitle,title))")
				+ "&type=video"
				+ "&safeSearch=none"
				+ "&q=" + Uri.EscapeDataString(keyword)
				+ "&maxResults=" + maxResults
				+ "&key=" + YoutubeProjectId).AsJson<JsonSearchListResponse>(cancellationToken);
		if (parsed.items is null) { Log.Debug("Youtube returned items:null"); return []; }

		return parsed.items.Select(item => new AudioResource(
			item.id?.videoId ?? throw new NullReferenceException("item.id.videoId was null"),
			item.snippet?.title,
			ResolverFor)).ToArray();
	}

	public async Task<IList<AudioResource>> SearchYoutubeDlAsync(string keyword, CancellationToken cancellationToken)
	{
		var search = await YoutubeDlHelper.GetSearchAsync(keyword, cancellationToken);
		if (search.entries is null)
		{
			Log.Debug("Youtube-dl returned entries:null");
			return [];
		}

		return search.entries
			.Where(entry => entry.id != null)
			.Select(entry => new AudioResource(
				entry.id!,
				entry.title,
				ResolverFor
			)).ToArray();
	}

	public void Dispose() { }
}
