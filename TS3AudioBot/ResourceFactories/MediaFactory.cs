// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.IO;
	using Helper;
	using Helper.AudioTags;
	using CommandSystem;

	public sealed class MediaFactory : IResourceFactory
	{
		public AudioType FactoryFor => AudioType.MediaLink;

		public bool MatchLink(string uri) => true;

		public R<PlayResource> GetResource(string uri)
		{
			return GetResourceById(new AudioResource(uri, null, AudioType.MediaLink));
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
				AudioResource finalResource;
				if (resource.ResourceTitle != null)
					finalResource = resource;
				else if (!string.IsNullOrWhiteSpace(result.Value))
					finalResource = resource.WithName(result.Value);
				else
					finalResource = resource.WithName(resource.ResourceId);
				return new PlayResource(resource.ResourceId, finalResource);
			}
		}

		public string RestoreLink(string id) => id;

		private static R<string> ValidateUri(string uri)
		{
			Uri uriResult;
			if (!Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out uriResult))
				return R<string>.Err(RResultCode.MediaInvalidUri.ToString());

			string fullUri = uri;
			if (!uriResult.IsAbsoluteUri)
			{
				try
				{
					fullUri = Path.GetFullPath(uri);
					Uri.TryCreate(uri, UriKind.Absolute, out uriResult);
				}
				catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is System.Security.SecurityException) { }
			}

			string scheme = uriResult.Scheme;
			if (scheme == Uri.UriSchemeHttp
				|| scheme == Uri.UriSchemeHttps
				|| scheme == Uri.UriSchemeFtp)
				return ValidateWeb(uri);
			else if (uriResult.Scheme == Uri.UriSchemeFile)
				return ValidateFile(fullUri);
			else
				return R<string>.Err(RResultCode.MediaUnknownUri.ToString());
		}

		private static string GetStreamName(Stream stream) => AudioTagReader.GetTitle(stream);

		private static R<string> ValidateWeb(string link)
		{
			string outName = null;
			var valCode = WebWrapper.GetResponse(new Uri(link), response => { using (var stream = response.GetResponseStream()) outName = GetStreamName(stream); });
			if (valCode == ValidateCode.Ok)
			{
				return R<string>.OkR(outName);
			}
			else
			{
				return R<string>.Err(RResultCode.MediaNoWebResponse.ToString());
			}
		}

		private static R<string> ValidateFile(string path)
		{
			if (!File.Exists(path))
				return R<string>.Err(RResultCode.MediaFileNotFound.ToString());

			try
			{
				using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
				{
					return R<string>.OkR(GetStreamName(stream));
				}
			}
			catch (PathTooLongException) { return R<string>.Err(RResultCode.AccessDenied.ToString()); }
			catch (DirectoryNotFoundException) { return R<string>.Err(RResultCode.MediaFileNotFound.ToString()); }
			catch (FileNotFoundException) { return R<string>.Err(RResultCode.MediaFileNotFound.ToString()); }
			catch (IOException) { return R<string>.Err(RResultCode.AccessDenied.ToString()); }
			catch (UnauthorizedAccessException) { return R<string>.Err(RResultCode.AccessDenied.ToString()); }
			catch (NotSupportedException) { return R<string>.Err(RResultCode.AccessDenied.ToString()); }
		}

		public void Dispose()
		{

		}
	}
}
