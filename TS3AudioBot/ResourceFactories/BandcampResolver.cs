// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;

namespace TS3AudioBot.ResourceFactories;

public sealed class BandcampResolver : IResourceResolver, IThumbnailResolver
{
	private static readonly Regex BandcampUrlRegex = new(@"([\w_-]+).bandcamp.com/track/([\w_-]+)", Util.DefaultRegexConfig);
	private static readonly Regex MainJsonRegex = new(@"data-tralbum=""([^""]*)""", Util.DefaultRegexConfig);
	private static readonly Regex EmbedJsonRegex = new(@"data-player-data=""([^""]*)""", Util.DefaultRegexConfig);

	private const string AddArtist = "artist";
	private const string AddTrack = "track";

	public string ResolverFor => "bandcamp";

	public MatchCertainty MatchResource(ResolveContext _, string uri) => BandcampUrlRegex.IsMatch(uri).ToMatchCertainty();

	public async Task<PlayResource> GetResource(ResolveContext _, string url, CancellationToken cancellationToken)
	{
		var match = BandcampUrlRegex.Match(url);
		if (!match.Success)
			throw Error.LocalStr(strings.error_media_invalid_uri);

		var artistName = match.Groups[1].Value;
		var trackName = match.Groups[2].Value;

		var json = await DownloadMainSiteJson(artistName, trackName, cancellationToken);

		var track = json.Tracks?.FirstOrDefault();
		if (track is null)
			throw Error.LocalStr(strings.error_media_internal_missing + " (no main track)");

		var id = json.Id?.ToString();
		var link = track.File?.Mp3_128;
		var title = track.Title;
		if (id is null || link is null || title is null)
			throw Error.LocalStr(strings.error_media_internal_missing + " (no main)");

		return new BandcampPlayResource(link,
			new AudioResource(id, title, ResolverFor)
				.Add(AddArtist, artistName)
				.Add(AddTrack, trackName),
			json.ArtId?.ToString());
	}

	public async Task<PlayResource> GetResourceById(ResolveContext _, AudioResource resource, CancellationToken cancellationToken)
	{
		var json = await DownloadEmbeddedSiteJson(resource.ResourceId, cancellationToken);
		var track = json.Tracks?.FirstOrDefault();
		if (track is null)
			throw Error.LocalStr(strings.error_media_internal_missing + " (no embed track)");

		if (string.IsNullOrEmpty(resource.ResourceTitle))
		{
			var title = track.Title;
			resource.ResourceTitle = title ?? $"Bandcamp (id: {resource.ResourceId})";
		}

		var link = track.File?.Mp3_128;
		if (link is null)
			throw Error.LocalStr(strings.error_media_internal_missing + " (no embed link)");

		return new BandcampPlayResource(link, resource, json.AlbumArtId?.ToString());
	}

	public string RestoreLink(ResolveContext _, AudioResource resource)
	{
		var artistName = resource.Get(AddArtist);
		var trackName = resource.Get(AddTrack);

		if (artistName != null && trackName != null)
			return $"https://{artistName}.bandcamp.com/track/{trackName}";

		// backup when something's wrong with the website
		return $"https://bandcamp.com/EmbeddedPlayer/v=2/track={resource.ResourceId}";
	}

	private static Task<JsonEmbeddedBlob> DownloadEmbeddedSiteJson(string id, CancellationToken cancellationToken)
		=> DownloadSiteJsonInternal<JsonEmbeddedBlob>($"https://bandcamp.com/EmbeddedPlayer/track={id}", EmbedJsonRegex, "embed", cancellationToken);

	private static Task<JsonMainBlob> DownloadMainSiteJson(string artistName, string trackName, CancellationToken cancellationToken)
		=> DownloadSiteJsonInternal<JsonMainBlob>($"https://{artistName}.bandcamp.com/track/{trackName}", MainJsonRegex, "main", cancellationToken);

	private static async Task<T> DownloadSiteJsonInternal<T>(string url, Regex regex, string log, CancellationToken cancellationToken)
	{
		var html = await WebWrapper.Request(url).AsString(cancellationToken);
		var match = regex.Match(html);
		if (!match.Success)
			throw Error.LocalStr($"{strings.error_media_internal_missing} ({log}-regex)");
		var json = WebUtility.HtmlDecode(match.Groups[1].Value);
		try
		{
			return JsonSerializer.Deserialize<T>(json) ?? throw Error.LocalStr($"{strings.error_media_internal_missing} ({log}-json)");
		}
		catch (JsonException ex)
		{
			throw Error.Exception(ex).LocalStr($"{strings.error_media_internal_invalid} ({log}-json)");
		}
	}

	public async Task GetThumbnail(ResolveContext _, PlayResource playResource, AsyncStreamAction action, CancellationToken cancellationToken)
	{
		string? artId = null;
		if (playResource is BandcampPlayResource bandcampPlayResource)
		{
			artId = bandcampPlayResource.ArtId;
		}
		if (artId is null)
		{
			var json = await DownloadEmbeddedSiteJson(playResource.AudioResource.ResourceId, cancellationToken);
			artId = json.AlbumArtId?.ToString();
		}

		if (string.IsNullOrEmpty(artId))
			throw Error.LocalStr(strings.error_media_image_not_found);

		//  1 : 1600px/1600px
		//  2 :  350px/ 350px
		//  3 :  100px/ 100px / full digital discography
		//  4 :  300px/ 300px
		//  5 :  700px/ 700px
		//  6 :  100px/ 100px
		//  7 :  150px/ 150px / discography
		//  8 :  124px/ 127px
		//  9 :  210px/ 210px / suggestion
		// 10 : 1200px/1200px / main banner
		// 11 :  172px/ 172px
		// 12 :  138px/ 138px
		// 13 :  380px/ 380px
		// 14 :  368px/ 368px
		// 15 :  135px/ 135px
		// 16 :  700px/ 700px
		// 42 :   50px/  50px / supporter
		await WebWrapper.Request($"https://f4.bcbits.com/img/a{artId}_4.jpg").ToStream(action, cancellationToken);
	}

	public void Dispose() { }

	class JsonMainBlob
	{
		[JsonPropertyName("art_id")]
		public ulong? ArtId { get; set; }

		[JsonPropertyName("artist")]
		public string? Artist { get; set; }

		[JsonPropertyName("id")]
		public ulong? Id { get; set; }

		[JsonPropertyName("trackinfo")]
		public JsonTrack[]? Tracks { get; set; }
	}

	class JsonEmbeddedBlob
	{
		[JsonPropertyName("album_art_id")]
		public ulong? AlbumArtId { get; set; }

		[JsonPropertyName("artist")]
		public string? Artist { get; set; }

		[JsonPropertyName("tracks")]
		public JsonTrack[]? Tracks { get; set; }
	}

	class JsonTrack
	{
		[JsonPropertyName("title")]
		public string? Title { get; set; }

		[JsonPropertyName("artist")]
		public string? Artist { get; set; } // Only set in embed

		[JsonPropertyName("duration")]
		public float? DurationSeconds { get; set; }

		[JsonPropertyName("file")]
		public JsonTrackFile? File { get; set; }

		[JsonPropertyName("id")]
		public ulong? Id { get; set; }
	}

	class JsonTrackFile
	{
		[JsonPropertyName("mp3-128")]
		public string? Mp3_128 { get; set; }
	}
}

public class BandcampPlayResource : PlayResource
{
	public string? ArtId { get; set; }

	public BandcampPlayResource(string uri, AudioResource baseData, string? artId) : base(uri, baseData)
	{
		ArtId = artId;
	}
}
