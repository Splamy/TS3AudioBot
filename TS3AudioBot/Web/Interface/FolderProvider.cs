// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Web.Interface
{
	using System.Collections.Generic;
	using System.IO;
	using TS3AudioBot.Helper;

	public class FolderProvider : IFolderProvider
	{
		private readonly Dictionary<string, FileProvider> fileCache;

		public DirectoryInfo LocalDirectory { get; }

		public FolderProvider(DirectoryInfo directory)
		{
			Util.Init(out fileCache);
			LocalDirectory = directory;
		}

		public ISiteProvider GetFile(string subPath)
		{
			FileInfo requestedFile;
			try
			{
				requestedFile = new FileInfo(Path.Combine(LocalDirectory.FullName, subPath.TrimStart('/')));
			}
			catch { return null; }

			// directory escaping prevention
			if (!requestedFile.FullName.StartsWith(LocalDirectory.FullName))
				return null;

			if (!requestedFile.Exists)
				return null;

			var normalizedSubPath = requestedFile.FullName.Substring(LocalDirectory.FullName.Length);

			if (!fileCache.TryGetValue(normalizedSubPath, out var file))
			{
				file = new FileProvider(requestedFile);
				fileCache[normalizedSubPath] = file;
			}
			return file;
		}
	}
}
