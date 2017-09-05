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
