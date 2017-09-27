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

	public sealed class MediaFactory : IResourceFactory, IPlaylistFactory
	{
		public string FactoryFor => "media";

		public MatchCertainty MatchResource(string uri) => MatchCertainty.OnlyIfLast;

		public MatchCertainty MatchPlaylist(string uri) => Directory.Exists(uri) ? MatchCertainty.Probably : MatchCertainty.Never;

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

		private static R<ResData> ValidateUri(string uri)
		{
			if (!Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out Uri uriResult))
				return R<ResData>.Err(RResultCode.MediaInvalidUri.ToString());

			string fullUri = uri;
			if (!uriResult.IsAbsoluteUri)
			{
				try { fullUri = Path.GetFullPath(uri); }
				catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is System.Security.SecurityException) { }

				if (!Uri.TryCreate(fullUri, UriKind.Absolute, out uriResult))
					return R<ResData>.Err(RResultCode.MediaInvalidUri.ToString());
			}

			if (uriResult.Scheme == Uri.UriSchemeHttp
			 || uriResult.Scheme == Uri.UriSchemeHttps
			 || uriResult.Scheme == Uri.UriSchemeFtp)
				return ValidateWeb(uriResult);
			else if (uriResult.Scheme == Uri.UriSchemeFile)
				return ValidateFile(fullUri);
			else
				return R<ResData>.Err(RResultCode.MediaUnknownUri.ToString());
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

		private static R<ResData> ValidateFile(string path)
		{
			if (!File.Exists(path))
				return R<ResData>.Err(RResultCode.MediaFileNotFound.ToString());

			try
			{
				using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
				{
					return R<ResData>.OkR(new ResData(path, GetStreamName(stream)));
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

		public void Dispose() { }

		public R<Playlist> GetPlaylist(string url)
		{
			if (!Directory.Exists(url))
				return R<Playlist>.Err(RResultCode.MediaFileNotFound.ToString());

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
}
