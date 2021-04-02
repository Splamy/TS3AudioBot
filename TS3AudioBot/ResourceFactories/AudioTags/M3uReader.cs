// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TSLib.Helper;

namespace TS3AudioBot.ResourceFactories.AudioTags
{
	public static class M3uReader
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const int MaxLineLength = 4096;
		private const int MaxListLength = 1000;
		private static readonly byte[] ExtM3uLine = Tools.Utf8Encoder.GetBytes("#EXTM3U");
		private static readonly byte[] ExtInfLine = Tools.Utf8Encoder.GetBytes("#EXTINF");
		private static readonly byte[] ExtXStreamInfLine = Tools.Utf8Encoder.GetBytes("#EXT-X-STREAM-INF");

		public static async Task<List<M3uEntry>> TryGetData(Stream stream)
		{
			int read = 1;
			int bufferLen = 0;
			var buffer = new byte[MaxLineLength];
			var data = new List<M3uEntry>();
			string? trackTitle = null;
			string? trackStreamMeta = null;
			//bool extm3u = false;

			try
			{
				for (int i = 0; i < MaxListLength; i++)
				{
					if (read > 0)
					{
						read = await stream.ReadAsync(buffer, bufferLen, MaxLineLength - bufferLen);
						bufferLen += read;
					}

					// find linebreak index
					int index = Array.IndexOf(buffer, (byte)'\n', 0, bufferLen);
					int lb = 1;
					if (index == -1)
						index = Array.IndexOf(buffer, (byte)'\r', 0, bufferLen);
					else if (index > 0 && buffer[index - 1] == (byte)'\r')
					{
						index--;
						lb = 2;
					}

					ReadOnlyMemory<byte> line;
					bool atEnd = index == -1;
					if (atEnd)
					{
						if (bufferLen == MaxLineLength)
							throw Error.Str("Max read buffer exceeded");
						line = buffer.AsMemory(0, bufferLen);
						bufferLen = 0;
					}
					else
					{
						line = buffer.AsMemory(0, index);
					}

					if (!line.IsEmpty)
					{
						if (line.Span[0] == (byte)'#')
						{
							if (line.Span.StartsWith(ExtInfLine))
							{
								var dataSlice = line.Slice(ExtInfLine.Length + 1);
								var trackInfo = dataSlice.Span.IndexOf((byte)',');
								if (trackInfo >= 0)
									trackTitle = dataSlice.Span.Slice(trackInfo + 1).NewUtf8String();
							}
							else if (line.Span.StartsWith(ExtM3uLine))
							{
								//extm3u = true; ???
							}
							else if (line.Span.StartsWith(ExtXStreamInfLine))
							{
								trackStreamMeta = line.Span.Slice(ExtXStreamInfLine.Length + 1).NewUtf8String();
							}
							// else: unsupported m3u tag
						}
						else
						{
							var lineStr = line.Span.NewUtf8String();
							if (Uri.TryCreate(lineStr, UriKind.RelativeOrAbsolute, out _))
							{
								data.Add(new M3uEntry(
									trackUrl: lineStr,
									title: trackTitle,
									streamMeta: trackStreamMeta)
								);
							}
							else
							{
								Log.Debug("Skipping invalid playlist entry ({0})", lineStr);
							}
							trackTitle = null;
							trackStreamMeta = null;
						}
					}

					if (!atEnd)
					{
						index += lb;
						Array.Copy(buffer, index, buffer, 0, MaxLineLength - index);
						bufferLen -= index;
					}

					if (atEnd || bufferLen <= 0)
					{
						if (bufferLen < 0)
							throw Error.Str("Unexpected buffer underfill");
						return data;
					}
				}
				throw Error.Str("List too long");
			}
			catch (Exception ex) { throw Error.Exception(ex).Str("List too long"); }
		}
	}

	public class M3uEntry
	{
		public string TrackUrl { get; set; }
		public string? Title { get; set; }
		public string? StreamMeta { get; set; }

		public M3uEntry(string trackUrl, string? title, string? streamMeta)
		{
			TrackUrl = trackUrl;
			Title = title;
			StreamMeta = streamMeta;
		}
	}
}
