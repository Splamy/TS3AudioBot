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
	using System.IO;

	public class FolderProvider : IFolderProvider
	{
		public DirectoryInfo LocalDirectory { get; }

		public FolderProvider(DirectoryInfo directory)
		{
			LocalDirectory = directory;
		}

		public ISiteProvider GetFile(string subPath)
		{
			var requestedFile = new FileInfo(Path.Combine(LocalDirectory.FullName, subPath.TrimStart('/')));
			// directory escaping prevention
			if (!requestedFile.FullName.StartsWith(LocalDirectory.FullName))
				return null;
			if (requestedFile.Exists)
				return new FileProvider(requestedFile);
			return null;
		}
	}
}
