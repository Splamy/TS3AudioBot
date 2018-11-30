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
	using AudioTags;
	using Config;
	using Helper;
	using Localization;
	using Playlists;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	public sealed class MediaFactory : IResourceFactory, IPlaylistFactory, IThumbnailFactory
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly ConfPath config;

		public MediaFactory(ConfPath config)
		{
			this.config = config;
		}

		public string FactoryFor => "media";

		public MatchCertainty MatchResource(string uri) =>
			File.Exists(uri)
			? MatchCertainty.Maybe
			: MatchCertainty.OnlyIfLast;

		public MatchCertainty MatchPlaylist(string uri) =>
			Directory.Exists(uri) ? MatchCertainty.Probably :
			File.Exists(uri) ? MatchCertainty.Maybe
			: MatchCertainty.OnlyIfLast;

		public R<PlayResource, LocalStr> GetResource(string uri)
		{
			return GetResourceById(new AudioResource(uri, null, FactoryFor));
		}

		public R<PlayResource, LocalStr> GetResourceById(AudioResource resource)
		{
			var result = ValidateUri(resource.ResourceId);
			if (!result)
				return result.Error;

			var resData = result.Value;
			AudioResource finalResource;
			if (resource.ResourceTitle != null)
				finalResource = resource;
			else if (!string.IsNullOrWhiteSpace(resData.Title))
				finalResource = resource.WithName(resData.Title);
			else
				finalResource = resource.WithName(resource.ResourceId);
			return new MediaPlayResource(resData.FullUri, finalResource, resData.Image);
		}

		public string RestoreLink(string id) => id;

		private R<ResData, LocalStr> ValidateUri(string uri)
		{
			if (Uri.TryCreate(uri, UriKind.Absolute, out Uri uriResult))
			{
				if (uriResult.Scheme == Uri.UriSchemeHttp
				 || uriResult.Scheme == Uri.UriSchemeHttps
				 || uriResult.Scheme == Uri.UriSchemeFtp)
					return ValidateWeb(uriResult);
				if (uriResult.Scheme == Uri.UriSchemeFile)
					return ValidateFile(uri);

				return new LocalStr(strings.error_media_invalid_uri);
			}
			else
			{
				return ValidateFile(uri);
			}
		}

		private static HeaderData GetStreamHeaderData(Stream stream)
		{
			var headerData = AudioTagReader.GetData(stream) ?? new HeaderData();
			headerData.Title = headerData.Title ?? string.Empty;
			return headerData;
		}

		private static R<ResData, LocalStr> ValidateWeb(Uri link)
		{
			var result = WebWrapper.GetResponseUnsafe(link);
			if (!result.Ok)
				return result.Error;

			using (var stream = result.Value)
			{
				var headerData = GetStreamHeaderData(stream);
				return new ResData(link.AbsoluteUri, headerData.Title) { Image = headerData.Picture };
			}
		}

		private R<ResData, LocalStr> ValidateFile(string path)
		{
			var foundPath = FindFile(path);
			Log.Trace("FindFile check result: '{0}'", foundPath);
			if (foundPath is null)
				return new LocalStr(strings.error_media_file_not_found);

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
				Log.Warn(ex, "Failed to load song \"{0}\", because {1}", path, ex.Message);
				return new LocalStr(strings.error_io_unknown_error);
			}
		}

		private Uri FindFile(string path)
		{
			Log.Trace("Finding media path: '{0}'", path);

			try
			{
				var fullPath = Path.GetFullPath(path);
				if (File.Exists(fullPath) || Directory.Exists(fullPath))
					return new Uri(fullPath, UriKind.Absolute);
				fullPath = Path.GetFullPath(Path.Combine(config.Path.Value, path));
				if (File.Exists(fullPath) || Directory.Exists(fullPath))
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
			var foundUri = FindFile(url);

			if (foundUri != null && Directory.Exists(foundUri.OriginalString))
			{
				try
				{
					var di = new DirectoryInfo(foundUri.OriginalString);
					var plist = new Playlist(di.Name);
					var resources = from file in di.EnumerateFiles()
									select ValidateFile(file.FullName) into result
									where result.Ok
									select result.Value into val
									select new AudioResource(val.FullUri, string.IsNullOrWhiteSpace(val.Title) ? val.FullUri : val.Title, FactoryFor) into res
									select new PlaylistItem(res);
					plist.Items.AddRange(resources);

					return plist;
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to load playlist \"{0}\", because {1}", url, ex.Message);
					return new LocalStr(strings.error_io_unknown_error);
				}
			}

			var m3uResult = R<IReadOnlyList<PlaylistItem>, string>.Err(string.Empty);
			if (foundUri != null && File.Exists(foundUri.OriginalString))
			{
				using (var stream = File.OpenRead(foundUri.OriginalString))
					m3uResult = M3uReader.TryGetData(stream);
			}
			else if (foundUri != null)
			{
				var status = WebWrapper.GetResponseUnsafe(foundUri);
				if (status.Ok)
					using (var stream = status.Value)
						m3uResult = M3uReader.TryGetData(stream);
				else
					return status.Error;
			}

			if (m3uResult)
			{
				var m3uList = new Playlist(PlaylistManager.CleanseName(url));
				m3uList.Items.AddRange(m3uResult.Value);
				return m3uList;
			}

			return new LocalStr(strings.error_media_file_not_found);
		}

		private static R<Stream, LocalStr> GetStreamFromUriUnsafe(Uri uri)
		{
			if (uri.Scheme == Uri.UriSchemeHttp
				|| uri.Scheme == Uri.UriSchemeHttps
				|| uri.Scheme == Uri.UriSchemeFtp)
				return WebWrapper.GetResponseUnsafe(uri);
			if (uri.Scheme == Uri.UriSchemeFile)
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

		public ResData(string fullUri, string title)
		{
			FullUri = fullUri;
			Title = title;
		}
	}

	public class MediaPlayResource : PlayResource
	{
		public byte[] Image { get; }

		public MediaPlayResource(string uri, AudioResource baseData, byte[] image) : base(uri, baseData)
		{
			Image = image;
		}
	}
}
