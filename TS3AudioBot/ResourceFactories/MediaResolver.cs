// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using PlaylistsNET.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories.AudioTags;

namespace TS3AudioBot.ResourceFactories
{
	public sealed class MediaResolver : IResourceResolver, IPlaylistResolver, IThumbnailResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly ConfPath config;

		public MediaResolver(ConfPath config)
		{
			this.config = config;
		}

		public string ResolverFor => "media";

		public MatchCertainty MatchResource(string uri) =>
			File.Exists(uri)
			? MatchCertainty.Always
			: MatchCertainty.OnlyIfLast;

		public MatchCertainty MatchPlaylist(string uri) =>
			Directory.Exists(uri) ? MatchCertainty.Always :
			File.Exists(uri) ? MatchCertainty.Always
			: MatchCertainty.OnlyIfLast;

		public R<PlayResource, LocalStr> GetResource(string uri)
		{
			return GetResourceById(new AudioResource(uri, null, ResolverFor));
		}

		public R<PlayResource, LocalStr> GetResourceById(AudioResource resource)
		{
			var result = ValidateFromString(resource.ResourceId);
			if (!result)
				return result.Error;

			var resData = result.Value;

			if (resData.IsIcyStream)
			{
				resource.ResourceTitle = resData.Title;
				return new MediaPlayResource(resData.FullUri, resource, null, true);
			}

			if (resource.ResourceTitle == null)
			{
				if (!string.IsNullOrWhiteSpace(resData.Title))
					resource.ResourceTitle = resData.Title;
				else
					resource.ResourceTitle = resource.ResourceId;
			}
			return new MediaPlayResource(resData.FullUri, resource, resData.Image, false);
		}

		public string RestoreLink(AudioResource resource) => resource.ResourceId;

		private R<ResData, LocalStr> ValidateFromString(string uriStr)
		{
			if (TryGetUri(uriStr).GetOk(out var uri))
				return ValidateUri(uri);
			return new LocalStr(strings.error_media_invalid_uri);
		}

		private R<ResData, LocalStr> ValidateUri(Uri uri)
		{
			if (uri.IsWeb())
				return ValidateWeb(uri);
			if (uri.IsFile())
				return ValidateFile(uri);

			return new LocalStr(strings.error_media_invalid_uri);
		}

		private static HeaderData GetStreamHeaderData(Stream stream)
		{
			var headerData = AudioTagReader.GetData(stream) ?? new HeaderData();
			headerData.Title = headerData.Title ?? string.Empty;
			return headerData;
		}

		private static R<ResData, LocalStr> ValidateWeb(Uri link)
		{
			var requestRes = WebWrapper.CreateRequest(link);
			if (!requestRes.Ok) return requestRes.Error;
			var request = requestRes.Value;
			if (request is HttpWebRequest httpRequest)
			{
				httpRequest.Headers["Icy-MetaData"] = "1";
			}

			try
			{
				using (var response = request.GetResponse())
				{
					if (response.Headers["icy-metaint"] != null)
					{
						return new ResData(link.AbsoluteUri, null) { IsIcyStream = true };
					}
					var contentType = response.Headers[HttpResponseHeader.ContentType];
					if (contentType == "application/vnd.apple.mpegurl"
						|| contentType == "application/vnd.apple.mpegurl.audio")
					{
						return new ResData(link.AbsoluteUri, null); // No title meta info
					}
					else
					{
						using (var stream = response.GetResponseStream())
						{
							var headerData = GetStreamHeaderData(stream);
							return new ResData(link.AbsoluteUri, headerData.Title) { Image = headerData.Picture };
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Failed to validate song");
				return new LocalStr(strings.error_net_unknown);
			}
		}

		private R<ResData, LocalStr> ValidateFile(Uri foundPath)
		{
			try
			{
				using (var stream = File.Open(foundPath.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					var headerData = GetStreamHeaderData(stream);
					return new ResData(foundPath.LocalPath, headerData.Title) { Image = headerData.Picture };
				}
			}
			catch (UnauthorizedAccessException) { return new LocalStr(strings.error_io_missing_permission); }
			catch (Exception ex)
			{
				Log.Warn(ex, "Failed to load song \"{0}\", because {1}", foundPath.OriginalString, ex.Message);
				return new LocalStr(strings.error_io_unknown_error);
			}
		}

		private R<Uri, LocalStr> TryGetUri(string uri)
		{
			if (Uri.TryCreate(uri, UriKind.Absolute, out Uri uriResult))
			{
				return uriResult;
			}
			else
			{
				var file = FindFile(uri);
				if (file == null)
					return new LocalStr(strings.error_media_file_not_found);
				return file;
			}
		}

		private Uri FindFile(string path)
		{
			Log.Trace("Finding media path: '{0}'", path);

			try
			{
				var fullPath = Path.GetFullPath(path);
				if (File.Exists(fullPath))
					return new Uri(fullPath, UriKind.Absolute);
				fullPath = Path.GetFullPath(Path.Combine(config.Path.Value, path));
				if (File.Exists(fullPath))
					return new Uri(fullPath, UriKind.Absolute);
			}
			catch (Exception ex)
			when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is System.Security.SecurityException)
			{
				Log.Trace(ex, "Couldn't load resource");
			}
			return null;
		}

		public void Dispose() { }

		public R<Playlist, LocalStr> GetPlaylist(string url)
		{
			if (Directory.Exists(url))
			{
				try
				{
					var di = new DirectoryInfo(url);
					var plist = new Playlist().SetTitle(di.Name);
					var resources = from file in di.EnumerateFiles()
									select ValidateFromString(file.FullName) into result
									where result.Ok
									select result.Value into val
									select new AudioResource(val.FullUri, string.IsNullOrWhiteSpace(val.Title) ? val.FullUri : val.Title, ResolverFor) into res
									select new PlaylistItem(res);
					plist.AddRange(resources);

					return plist;
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to load playlist \"{0}\", because {1}", url, ex.Message);
					return new LocalStr(strings.error_io_unknown_error);
				}
			}

			var plistResult = R<Playlist, LocalStr>.Err(new LocalStr(strings.error_media_invalid_uri));

			try
			{
				if (File.Exists(url))
				{
					using (var stream = File.OpenRead(url))
						plistResult = GetPlaylistContent(stream, url);
				}
				else if (TryGetUri(url).GetOk(out var uri))
				{
					plistResult = WebWrapper.GetResponse(uri, response =>
					{
						var contentType = response.Headers.Get("Content-Type");
						int index = url.LastIndexOf('.');
						string anyId = index >= 0 ? url.Substring(index) : url;

						using (var stream = response.GetResponseStream())
							return GetPlaylistContent(stream, url, contentType);
					}).Flat();
				}
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Error opening/reading playlist file");
				return new LocalStr(strings.error_media_invalid_uri);
			}

			return plistResult;
		}

		private R<Playlist, LocalStr> GetPlaylistContent(Stream stream, string url, string mime = null)
		{
			string name = null;
			List<PlaylistItem> items;
			mime = mime.ToLowerInvariant();
			url = url.ToLowerInvariant();
			string anyId = mime ?? url;

			switch (anyId)
			{
			case ".m3u":
				{
					var parser = new M3uContent();
					var list = parser.GetFromStream(stream);

					items = new List<PlaylistItem>(
						from e in list.PlaylistEntries
						select new PlaylistItem(new AudioResource(e.Path, e.Title, ResolverFor)));
					break;
				}
			case ".m3u8":
			case "application/mpegurl":
			case "application/x-mpegurl":
			case "audio/mpegurl":
			case "audio/x-mpegurl":
			case "application/vnd.apple.mpegurl":
			case "application/vnd.apple.mpegurl.audio":
				{
					var parser = new M3u8Content();
					var list = parser.GetFromStream(stream);

					items = new List<PlaylistItem>(
						from e in list.PlaylistEntries
						select new PlaylistItem(new AudioResource(e.Path, e.Title, ResolverFor)));
					break;
				}
			case ".pls":
			case "audio/x-scpls":
			case "application/x-scpls":
			case "application/pls+xml":
				{
					var parser = new PlsContent();
					var list = parser.GetFromStream(stream);

					items = new List<PlaylistItem>(
						from e in list.PlaylistEntries
						select new PlaylistItem(new AudioResource(e.Path, e.Title, ResolverFor)));
					break;
				}
			case ".wpl":
				{
					var parser = new WplContent();
					var list = parser.GetFromStream(stream);

					items = new List<PlaylistItem>(
						from e in list.PlaylistEntries
						select new PlaylistItem(new AudioResource(e.Path, e.TrackTitle, ResolverFor)));
					name = list.Title;
					break;
				}
			case ".zpl":
				{
					var parser = new ZplContent();
					var list = parser.GetFromStream(stream);

					items = new List<PlaylistItem>(
						from e in list.PlaylistEntries
						select new PlaylistItem(new AudioResource(e.Path, e.TrackTitle, ResolverFor)));
					name = list.Title;
					break;
				}

			// ??
			case "application/jspf+json":
			// ??
			case "application/xspf+xml":
			default:
				return new LocalStr(strings.error_media_file_not_found); // TODO Loc "media not supported"
			}

			if (string.IsNullOrEmpty(name))
			{
				var index = url.LastIndexOfAny(new[] { '\\', '/' });
				name = index >= 0 ? url.Substring(index) : url;
			}
			return new Playlist(items).SetTitle(name);
		}

		private static R<Stream, LocalStr> GetStreamFromUriUnsafe(Uri uri)
		{
			if (uri.IsWeb())
				return WebWrapper.GetResponseUnsafe(uri);
			if (uri.IsFile())
				return File.OpenRead(uri.LocalPath);

			return new LocalStr(strings.error_media_invalid_uri);
		}

		public R<Stream, LocalStr> GetThumbnail(PlayResource playResource)
		{
			byte[] rawImgData;

			if (playResource is MediaPlayResource mediaPlayResource)
			{
				rawImgData = mediaPlayResource.Image;
			}
			else
			{
				var uri = new Uri(playResource.PlayUri);
				var result = GetStreamFromUriUnsafe(uri);
				if (!result)
					return result.Error;

				using (var stream = result.Value)
				{
					rawImgData = AudioTagReader.GetData(stream)?.Picture;
				}
			}

			if (rawImgData is null)
				return new LocalStr(strings.error_media_image_not_found);

			return new MemoryStream(rawImgData);
		}
	}

	internal class ResData
	{
		public string FullUri { get; }
		public string Title { get; }
		public byte[] Image { get; set; }

		public bool IsIcyStream { get; set; } = false;

		public ResData(string fullUri, string title)
		{
			FullUri = fullUri;
			Title = title;
		}
	}

	internal static class MediaExt
	{
		public static bool IsWeb(this Uri uri)
			=> uri.Scheme == Uri.UriSchemeHttp
			|| uri.Scheme == Uri.UriSchemeHttps
			|| uri.Scheme == Uri.UriSchemeFtp;

		public static bool IsFile(this Uri uri)
			=> uri.Scheme == Uri.UriSchemeFile;
	}

	public class MediaPlayResource : PlayResource
	{
		public byte[] Image { get; }
		public bool IsIcyStream { get; }

		public MediaPlayResource(string uri, AudioResource baseData, byte[] image, bool isIcyStream) : base(uri, baseData)
		{
			Image = image;
			IsIcyStream = isIcyStream;
		}
	}
}
