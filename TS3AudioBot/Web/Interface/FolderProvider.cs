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

namespace TS3AudioBot.Web.Interface
{
	using System.IO;

	class FolderProvider : IFolderProvider
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
