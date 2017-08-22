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
	using System;
	using System.IO;

	public class FileProvider : ISiteProvider
	{
		private byte[] rawData;
		private bool loadedOnce = false;
		public FileInfo LocalFile { get; }

		public string MimeType { get; }

		private DateTime lastWrite = DateTime.MinValue;

		public FileProvider(FileInfo file)
		{
			LocalFile = file ?? throw new ArgumentNullException(nameof(file));
			if (WebDisplay.MimeTypes.TryGetValue(LocalFile.Extension, out var mimeType))
				MimeType = mimeType;
			else
				MimeType = null;
		}

		public byte[] GetData()
		{
			LocalFile.Refresh();

			if (!loadedOnce || LocalFile.LastWriteTime >= lastWrite)
			{
				rawData = File.ReadAllBytes(LocalFile.FullName);
				lastWrite = LocalFile.LastWriteTime;
			}

			return rawData;
		}
	}
}
