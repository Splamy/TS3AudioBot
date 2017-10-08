// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper.AudioTags
{
	using ResourceFactories;
	using System;
	using System.Collections.Generic;
	using System.IO;

	internal class M3uReader
	{
		const int MaxLineLength = 4096;

		public static R<IReadOnlyList<PlaylistItem>> TryGetData(Stream stream)
		{
			var br = new BinaryReader(stream);
			int read = 1;
			int bufferLen = 0;
			var buffer = new char[MaxLineLength];
			var data = new List<PlaylistItem>();
			string trackTitle = null;
			bool extm3u = false;

			try
			{
				while (true)
				{
					if (read > 0)
					{
						read = br.Read(buffer, bufferLen, MaxLineLength - bufferLen);
						bufferLen += read;
					}

					if (bufferLen <= 0)
						break;

					// find linebreak index
					int index = Array.IndexOf(buffer, '\n');
					int lb = 1;
					if (index == -1)
						index = Array.IndexOf(buffer, '\r');
					else if (index > 0 && buffer[index - 1] == '\r')
					{
						index--;
						lb = 2;
					}

					string line;
					if (index == -1)
					{
						if (bufferLen == MaxLineLength)
							return "Max read buffer exceeded";
						line = new string(buffer, 0, bufferLen);
						bufferLen = 0;
					}
					else
					{
						line = new string(buffer, 0, index);
						index += lb;
						Array.Copy(buffer, index, buffer, 0, MaxLineLength - index);
						bufferLen -= index;
					}

					if (line.StartsWith("#"))
					{
						if (extm3u && line.StartsWith("#EXTINF:"))
						{
							var trackInfo = line.Substring(8).Split(new[] { ',' }, 2);
							if (trackInfo.Length == 2)
								trackTitle = trackInfo[1];
						}
						else if (line.StartsWith("#EXTM3U"))
						{
							extm3u = true;
						}
						// else: unsupported m3u tag
					}
					else
					{
						data.Add(new PlaylistItem(new AudioResource(line, trackTitle ?? line, "media")));
						trackTitle = null;
					}

					if (index == -1)
						return data;
				}
			}
			catch { }
			return "Unexpected m3u parsing error";
		}
	}
}
