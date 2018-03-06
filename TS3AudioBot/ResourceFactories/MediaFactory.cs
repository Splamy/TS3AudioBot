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
	using Helper;
	using Helper.AudioTags;
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.IO;
	using System.Linq;

	public sealed class MediaFactory : IResourceFactory, IPlaylistFactory, IThumbnailFactory
	{
		private readonly MediaFactoryData mediaFactoryData;

		public MediaFactory(MediaFactoryData mfd)
		{
			mediaFactoryData = mfd;
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

		public R<PlayResource> GetResource(string uri)
		{
			return GetResourceById(new AudioResource(uri, null, FactoryFor));
		}

		public R<PlayResource> GetResourceById(AudioResource resource)
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

		private R<ResData> ValidateUri(string uri)
		{
			if (Uri.TryCreate(uri, UriKind.Absolute, out Uri uriResult))
			{
				if (uriResult.Scheme == Uri.UriSchemeHttp
				 || uriResult.Scheme == Uri.UriSchemeHttps
				 || uriResult.Scheme == Uri.UriSchemeFtp)
					return ValidateWeb(uriResult);
				if (uriResult.Scheme == Uri.UriSchemeFile)
					return ValidateFile(uriResult.OriginalString);

				return R<ResData>.Err(RResultCode.MediaUnknownUri.ToString());
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

		private static R<ResData> ValidateWeb(Uri link)
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

		private R<ResData> ValidateFile(string path)
		{
			var foundPath = FindFile(path);
			if (foundPath == null)
				return R<ResData>.Err(RResultCode.MediaFileNotFound.ToString());

			try
			{
				using (var stream = File.Open(foundPath.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					var headerData = GetStreamHeaderData(stream);
					return R<ResData>.OkR(new ResData(foundPath.LocalPath, headerData.Title) { Image = headerData.Picture });
				}
			}
			// TODO: correct errors
			catch (PathTooLongException) { return R<ResData>.Err(RResultCode.AccessDenied.ToString()); }
			catch (DirectoryNotFoundException) { return R<ResData>.Err(RResultCode.MediaFileNotFound.ToString()); }
			catch (FileNotFoundException) { return R<ResData>.Err(RResultCode.MediaFileNotFound.ToString()); }
			catch (IOException) { return R<ResData>.Err(RResultCode.AccessDenied.ToString()); }
			catch (UnauthorizedAccessException) { return R<ResData>.Err(RResultCode.AccessDenied.ToString()); }
			catch (NotSupportedException) { return R<ResData>.Err(RResultCode.AccessDenied.ToString()); }
		}

		private Uri FindFile(string path)
		{
			if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uri))
				return null;

			if (uri.IsAbsoluteUri)
				return File.Exists(path) || Directory.Exists(path) ? uri : null;

			try
			{
				var fullPath = Path.GetFullPath(path);
				if (File.Exists(fullPath) || Directory.Exists(fullPath))
					return new Uri(fullPath, UriKind.Absolute);
				fullPath = Path.GetFullPath(Path.Combine(mediaFactoryData.DefaultPath, path));
				if (File.Exists(fullPath) || Directory.Exists(fullPath))
					return new Uri(fullPath, UriKind.Absolute);
			}
			catch (Exception ex)
			when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is System.Security.SecurityException)
			{ }
			return null;
		}

		public void Dispose() { }

		public R<Playlist> GetPlaylist(string url)
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
					plist.AddRange(resources);

					return plist;
				}
				// TODO: correct errors
				catch (PathTooLongException) { return R<Playlist>.Err(RResultCode.AccessDenied.ToString()); }
				catch (ArgumentException) { return R<Playlist>.Err(RResultCode.MediaFileNotFound.ToString()); }
			}

			var m3uResult = R<IReadOnlyList<PlaylistItem>>.Err(string.Empty);
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
				m3uList.AddRange(m3uResult.Value);
				return m3uList;
			}

			return R<Playlist>.Err(RResultCode.MediaFileNotFound.ToString());
		}

		private static R<Stream> GetStreamFromUriUnsafe(Uri uri)
		{
			if (uri.Scheme == Uri.UriSchemeHttp
				|| uri.Scheme == Uri.UriSchemeHttps
				|| uri.Scheme == Uri.UriSchemeFtp)
				return WebWrapper.GetResponseUnsafe(uri);
			if (uri.Scheme == Uri.UriSchemeFile)
				return File.OpenRead(uri.LocalPath);

			return RResultCode.MediaUnknownUri.ToString();
		}

		public R<Image> GetThumbnail(PlayResource playResource)
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

			if (rawImgData == null)
				return "No image found";

			using (var memStream = new MemoryStream(rawImgData))
			{
				try
				{
					return new Bitmap(memStream);
				}
				catch (ArgumentException)
				{
					return "Inavlid image data";
				}
			}
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

	public class MediaFactoryData : ConfigData
	{
		[Info("The default path to look for local resources.", "")]
		public string DefaultPath { get; set; }
	}
}
