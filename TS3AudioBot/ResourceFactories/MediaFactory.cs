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
	using System;
	using System.IO;
	using System.Linq;
	using Helper;
	using Helper.AudioTags;
	using System.Collections.Generic;

	public sealed class MediaFactory : IResourceFactory, IPlaylistFactory
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
			{
				return result.Message;
			}
			else
			{
				var resData = result.Value;
				AudioResource finalResource;
				if (resource.ResourceTitle != null)
					finalResource = resource;
				else if (!string.IsNullOrWhiteSpace(resData.Title))
					finalResource = resource.WithName(resData.Title);
				else
					finalResource = resource.WithName(resource.ResourceId);
				return new PlayResource(resData.FullUri, finalResource);
			}
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
				else if (uriResult.Scheme == Uri.UriSchemeFile)
					return ValidateFile(uriResult.OriginalString);
				else
					return R<ResData>.Err(RResultCode.MediaUnknownUri.ToString());
			}
			else
			{
				return ValidateFile(uri);
			}
		}

		private static string GetStreamName(Stream stream)
			=> AudioTagReader.GetTitle(stream) ?? string.Empty;

		private static R<ResData> ValidateWeb(Uri link)
		{
			string outName = null;
			var valCode = WebWrapper.GetResponse(link, response =>
			{
				using (var stream = response.GetResponseStream())
					outName = GetStreamName(stream);
			});

			if (valCode == ValidateCode.Ok)
			{
				return R<ResData>.OkR(new ResData(link.AbsoluteUri, outName));
			}
			else
			{
				return R<ResData>.Err(RResultCode.MediaNoWebResponse.ToString());
			}
		}

		private R<ResData> ValidateFile(string path)
		{
			var foundPath = FindFile(path);
			if (foundPath == null)
				return R<ResData>.Err(RResultCode.MediaFileNotFound.ToString());

			try
			{
				using (var stream = File.Open(foundPath.LocalPath, FileMode.Open, FileAccess.Read))
				{
					return R<ResData>.OkR(new ResData(foundPath.LocalPath, GetStreamName(stream)));
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
			if (Directory.Exists(url))
			{
				try
				{
					var di = new DirectoryInfo(url);
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

			var m3uresult = R<IReadOnlyList<PlaylistItem>>.Err(string.Empty);
			if (File.Exists(url))
			{
				using (var stream = File.OpenRead(url))
					m3uresult = M3uReader.TryGetData(stream);
			}
			else if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
			{
				var ret = WebWrapper.GetResponse(uri, resp =>
				{
					using (var stream = resp.GetResponseStream())
						m3uresult = M3uReader.TryGetData(stream);

				});
				if (ret != ValidateCode.Ok)
					return R<Playlist>.Err(ret.ToString());
			}

			if (m3uresult)
			{
				var m3ulist = new Playlist(PlaylistManager.CleanseName(url));
				m3ulist.AddRange(m3uresult.Value);
				return m3ulist;
			}

			return R<Playlist>.Err(RResultCode.MediaFileNotFound.ToString());
		}
	}

	internal class ResData
	{
		public string FullUri { get; }
		public string Title { get; }

		public ResData(string fullUri, string title)
		{
			FullUri = fullUri;
			Title = title;
		}
	}

	public class MediaFactoryData : ConfigData
	{
		[Info("The default path to look for local resources.", "")]
		public string DefaultPath { get; set; }
	}
}
