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
using System.Threading.Tasks;
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

		public string ResolverFor => "media";

		public MatchCertainty MatchResource(ResolveContext _, string uri) =>
			File.Exists(uri)
			? MatchCertainty.Always
			: MatchCertainty.OnlyIfLast;

		public MatchCertainty MatchPlaylist(ResolveContext _, string uri) =>
			Directory.Exists(uri) ? MatchCertainty.Always :
			File.Exists(uri) ? MatchCertainty.Always
			: MatchCertainty.OnlyIfLast;

		public Task<PlayResource> GetResource(ResolveContext ctx, string uri)
		{
			return GetResourceById(ctx, new AudioResource(uri, null, ResolverFor));
		}

		public async Task<PlayResource> GetResourceById(ResolveContext ctx, AudioResource resource)
		{
			var resData = await ValidateFromString(ctx.Config, resource.ResourceId);

			if (resData.IsIcyStream)
			{
				resource.ResourceTitle = resData.Title;
				return new MediaPlayResource(resData.FullUri, resource, null, true);
			}

			if (resource.ResourceTitle is null)
			{
				if (!string.IsNullOrWhiteSpace(resData.Title))
					resource.ResourceTitle = resData.Title;
				else
					resource.ResourceTitle = resource.ResourceId;
			}
			return new MediaPlayResource(resData.FullUri, resource, resData.Image, false);
		}

		public string RestoreLink(ResolveContext _, AudioResource resource) => resource.ResourceId;

		private Task<ResData> ValidateFromString(ConfBot config, string uriStr)
		{
			var uri = GetUri(config, uriStr);
			return ValidateUri(uri);
		}

		private Task<ResData> ValidateUri(Uri uri)
		{
			if (uri.IsWeb())
				return ValidateWeb(uri);
			if (uri.IsFile())
				return Task.Run(() => ValidateFile(uri));

			throw Error.LocalStr(strings.error_media_invalid_uri);
		}

		private static HeaderData GetStreamHeaderData(Stream stream)
		{
			var headerData = AudioTagReader.GetData(stream) ?? new HeaderData();
			headerData.Title ??= string.Empty;
			return headerData;
		}

		private static async Task<ResData> ValidateWeb(Uri link)
		{
			try
			{
				return await WebWrapper.Request(link).WithHeader("Icy-MetaData", "1").ToAction(async response =>
				{
					if (response.Headers.GetSingle("icy-metaint") != null)
					{
						return new ResData(link.AbsoluteUri, null) { IsIcyStream = true };
					}
					var contentType = response.Headers.GetSingle("ContentType");
					if (contentType == "application/vnd.apple.mpegurl"
						|| contentType == "application/vnd.apple.mpegurl.audio")
					{
						return new ResData(link.AbsoluteUri, null); // No title meta info
					}
					else
					{
						using var stream = await response.Content.ReadAsStreamAsync();
						var headerData = GetStreamHeaderData(stream);
						return new ResData(link.AbsoluteUri, headerData.Title) { Image = headerData.Picture };
					}
				});
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Failed to validate song");
				throw Error.Exception(ex).LocalStr(strings.error_net_unknown);
			}
		}

		private ResData ValidateFile(Uri foundPath)
		{
			try
			{
				using var stream = File.Open(foundPath.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
				var headerData = GetStreamHeaderData(stream);
				return new ResData(foundPath.LocalPath, headerData.Title) { Image = headerData.Picture };
			}
			catch (UnauthorizedAccessException ex)
			{
				throw Error.Exception(ex).LocalStr(strings.error_io_missing_permission);
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Failed to load song \"{0}\", because {1}", foundPath.OriginalString, ex.Message);
				throw Error.Exception(ex).LocalStr(strings.error_io_unknown_error);
			}
		}

		private Uri GetUri(ConfBot conf, string uri)
		{
			if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? uriResult))
			{
				return uriResult;
			}
			else
			{
				Log.Trace("Finding media path: '{0}'", uri);

				Uri? file = null;
				if (conf.LocalConfigDir != null)
					file ??= TryInPath(Path.Combine(conf.LocalConfigDir, BotPaths.Music), uri);
				file ??= TryInPath(conf.GetParent().Factories.Media.Path.Value, uri);

				if (file is null)
					throw Error.LocalStr(strings.error_media_file_not_found);
				return file;
			}
		}

		private static Uri? TryInPath(string pathPrefix, string file)
		{
			try
			{
				var musicPathPrefix = Path.GetFullPath(pathPrefix);
				var fullPath = Path.Combine(musicPathPrefix, file);
				if (fullPath.StartsWith(musicPathPrefix) && File.Exists(fullPath))
					return new Uri(fullPath, UriKind.Absolute);
			}
			catch (Exception ex)
			when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is System.Security.SecurityException)
			{
				Log.Trace(ex, "Couldn't load resource");
			}
			return null;
		}

		public async Task<Playlist> GetPlaylist(ResolveContext ctx, string url)
		{
			if (Directory.Exists(url)) // TODO rework for security
			{
				try
				{
					var di = new DirectoryInfo(url);
					var plist = new Playlist().SetTitle(di.Name);
					foreach (var file in di.EnumerateFiles())
					{
						try
						{
							var val = await ValidateFromString(ctx.Config, file.FullName);
							var res = new AudioResource(val.FullUri, string.IsNullOrWhiteSpace(val.Title) ? val.FullUri : val.Title, ResolverFor);
							var addResult = plist.Add(new PlaylistItem(res));
							if (!addResult) break;
						}
						catch (AudioBotException) { }
					}

					return plist;
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to load playlist \"{0}\", because {1}", url, ex.Message);
					throw Error.Exception(ex).LocalStr(strings.error_io_unknown_error);
				}
			}

			var uri = GetUri(ctx.Config, url);
			try
			{
				if (uri.IsFile())
				{
					using var stream = File.OpenRead(uri.AbsolutePath);
					return await GetPlaylistContentAsync(stream, url);
				}
				else if (uri.IsWeb())
				{
					return await WebWrapper.Request(uri).ToAction(async response =>
					{
						var contentType = response.Headers.GetSingle("Content-Type");
						int index = url.LastIndexOf('.');
						string anyId = index >= 0 ? url.Substring(index) : url;

						using var stream = await response.Content.ReadAsStreamAsync();
						return await GetPlaylistContentAsync(stream, url, contentType);
					});
				}
				throw Error.LocalStr(strings.error_media_invalid_uri);
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Error opening/reading playlist file");
				throw Error.Exception(ex).LocalStr(strings.error_io_unknown_error);
			}
		}

		private Task<Playlist> GetPlaylistContentAsync(Stream stream, string url, string? mime = null)
			=> Task.Run(() => GetPlaylistContent(stream, url, mime));

		private Playlist GetPlaylistContent(Stream stream, string url, string? mime = null)
		{
			string? name = null;
			List<PlaylistItem> items;
			mime = mime?.ToLowerInvariant();
			url = url.ToLowerInvariant();
			string anyId = mime ?? url;

			switch (anyId)
			{
			case ".m3u":
			case ".m3u8":
			case "application/mpegurl":
			case "application/x-mpegurl":
			case "audio/mpegurl":
			case "audio/x-mpegurl":
			case "application/vnd.apple.mpegurl":
			case "application/vnd.apple.mpegurl.audio":
				{
					var parser = new M3uContent();
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
				throw Error.LocalStr(strings.error_media_file_not_found); // TODO Loc "media not supported"
			}

			if (string.IsNullOrEmpty(name))
			{
				var index = url.LastIndexOfAny(new[] { '\\', '/' });
				name = index >= 0 ? url.Substring(index) : url;
			}
			return new Playlist(items).SetTitle(name);
		}

		public async Task GetThumbnail(ResolveContext _, PlayResource playResource, Func<Stream, Task> action)
		{
			byte[]? rawImgData;

			if (playResource is MediaPlayResource mediaPlayResource)
			{
				rawImgData = mediaPlayResource.Image;
			}
			else
			{
				var uri = new Uri(playResource.PlayUri);

				if (uri.IsWeb())
					rawImgData = await WebWrapper.Request(uri)
						.ToAction(async response => AudioTagReader.GetData(await response.Content.ReadAsStreamAsync())?.Picture);
				else if (uri.IsFile())
					rawImgData = AudioTagReader.GetData(File.OpenRead(uri.LocalPath))?.Picture;
				else
					throw Error.LocalStr(strings.error_media_invalid_uri);
			}

			if (rawImgData is null)
				throw Error.LocalStr(strings.error_media_image_not_found);

			await action(new MemoryStream(rawImgData));
		}

		public void Dispose() { }
	}

	internal class ResData
	{
		public string FullUri { get; }
		public string? Title { get; }
		public byte[]? Image { get; set; }

		public bool IsIcyStream { get; set; } = false;

		public ResData(string fullUri, string? title)
		{
			FullUri = fullUri;
			Title = title;
			Image = null;
		}
	}

	internal static class MediaExt
	{
		public static bool IsWeb(this Uri uri)
			=> uri.Scheme == Uri.UriSchemeHttp
			|| uri.Scheme == Uri.UriSchemeHttps;

		public static bool IsFile(this Uri uri)
			=> uri.Scheme == Uri.UriSchemeFile;
	}

	public class MediaPlayResource : PlayResource
	{
		public byte[]? Image { get; }
		public bool IsIcyStream { get; }

		public MediaPlayResource(string uri, AudioResource baseData, byte[]? image, bool isIcyStream) : base(uri, baseData)
		{
			Image = image;
			IsIcyStream = isIcyStream;
		}
	}
}
