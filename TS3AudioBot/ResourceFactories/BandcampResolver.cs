// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;

namespace TS3AudioBot.ResourceFactories
{
	public class BandcampResolver : IResourceResolver, IThumbnailResolver
	{
		private static readonly Regex BandcampUrlRegex = new Regex(@"([\w_-]+).bandcamp.com/track/([\w_-]+)", Util.DefaultRegexConfig);
		private static readonly Regex TrackLinkRegex = new Regex(@"""mp3-128""\s*:\s*""([^""]*)""", Util.DefaultRegexConfig);
		private static readonly Regex TrackNameRegex = new Regex(@"""title""\s*:\s*""([^""]*)""", Util.DefaultRegexConfig);
		private static readonly Regex TrackArtRegex = new Regex(@"""album_art_id""\s*:\s*(\d+)\s*,", Util.DefaultRegexConfig);
		private static readonly Regex TrackMainJsonRegex = new Regex(@"trackinfo\s*:(.*),(\r|\n)", Util.DefaultRegexConfig);

		private const string AddArtist = "artist";
		private const string AddTrack = "track";

		public string ResolverFor => "bandcamp";

		public MatchCertainty MatchResource(ResolveContext _, string uri) => BandcampUrlRegex.IsMatch(uri).ToMatchCertainty();

		public async Task<PlayResource> GetResource(ResolveContext _, string url)
		{
			var match = BandcampUrlRegex.Match(url);
			if (!match.Success)
				throw Error.LocalStr(strings.error_media_invalid_uri);

			var artistName = match.Groups[1].Value;
			var trackName = match.Groups[2].Value;

			var webSite = await WebWrapper.Request($"https://{artistName}.bandcamp.com/track/{trackName}").AsString();

			match = TrackMainJsonRegex.Match(webSite);
			if (!match.Success)
				throw Error.LocalStr(strings.error_media_internal_missing + " (TrackMainJsonRegex)");

			JToken jobj;
			try { jobj = JToken.Parse(match.Groups[1].Value); }
			catch (JsonReaderException ex) { throw Error.Exception(ex).LocalStr(strings.error_media_internal_missing + " (TrackMainJsonRegex.JToken)"); }

			if (!(jobj is JArray jarr) || jarr.Count == 0)
				throw Error.LocalStr(strings.error_media_no_stream_extracted);

			var firstTrack = jarr[0];
			JToken? firstTrackFile;
			if (!firstTrack.TryCast<string>("track_id", out var id)
				|| !firstTrack.TryCast<string>("title", out var title)
				|| (firstTrackFile = firstTrack["file"]) == null
				|| !firstTrackFile.TryCast<string>("mp3-128", out var trackObj))
				throw Error.LocalStr(strings.error_media_no_stream_extracted);

			return new BandcampPlayResource(trackObj,
				new AudioResource(id, title, ResolverFor)
					.Add(AddArtist, artistName)
					.Add(AddTrack, trackName),
				GetTrackArtId(webSite));
		}

		public async Task<PlayResource> GetResourceById(ResolveContext _, AudioResource resource)
		{
			var webSite = await DownloadEmbeddedSite(resource.ResourceId);

			if (string.IsNullOrEmpty(resource.ResourceTitle))
			{
				var nameMatch = TrackNameRegex.Match(webSite);
				resource.ResourceTitle = nameMatch.Success
					? nameMatch.Groups[1].Value
					: $"Bandcamp (id: {resource.ResourceId})";
			}

			var match = TrackLinkRegex.Match(webSite);
			if (!match.Success)
				throw Error.LocalStr(strings.error_media_internal_missing + " (TrackLinkRegex)");

			return new BandcampPlayResource(match.Groups[1].Value, resource, GetTrackArtId(webSite));
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

		private static Task<string> DownloadEmbeddedSite(string id)
			=> WebWrapper.Request($"https://bandcamp.com/EmbeddedPlayer/v=2/track={id}").AsString();

		public async Task GetThumbnail(ResolveContext _, PlayResource playResource, Func<Stream, Task> action)
		{
			string? artId = null;
			if (playResource is BandcampPlayResource bandcampPlayResource)
			{
				artId = bandcampPlayResource.ArtId;
			}
			if (artId is null)
			{
				var webSite = await DownloadEmbeddedSite(playResource.AudioResource.ResourceId);
				artId = GetTrackArtId(webSite);
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
			await WebWrapper.Request($"https://f4.bcbits.com/img/a{artId}_4.jpg").ToStream(action);
		}

		private static string? GetTrackArtId(string site)
		{
			var match = TrackArtRegex.Match(site);
			if (!match.Success)
				return null;
			return match.Groups[1].Value;
		}

		public void Dispose() { }
	}

	public class BandcampPlayResource : PlayResource
	{
		public string? ArtId { get; set; }

		public BandcampPlayResource(string uri, AudioResource baseData, string? artId) : base(uri, baseData)
		{
			ArtId = artId;
		}
	}
}
