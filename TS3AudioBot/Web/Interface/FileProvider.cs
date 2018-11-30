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
	using Helper;
	using System;
	using System.IO;
	using System.Net;
	using System.Text;

	public class FileProvider : ISiteProvider
	{
		private byte[] rawData;
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

		public byte[] GetData(HttpListenerRequest request, HttpListenerResponse response)
		{
			LocalFile.Refresh();

			if (!LocalFile.Exists)
				return null;

			if (rawData is null || LocalFile.LastWriteTime > lastWrite)
			{
				rawData = File.ReadAllBytes(LocalFile.FullName);
				lastWrite = LocalFile.LastWriteTime;
			}

			byte[] returnData;
			if (DateTime.TryParse(request.Headers["If-Modified-Since"], out var cachedDate) && cachedDate.AddSeconds(1) >= lastWrite)
			{
				response.StatusCode = (int)HttpStatusCode.NotModified;
				returnData = Array.Empty<byte>();
			}
			else
			{
				response.StatusCode = (int)HttpStatusCode.OK;
				returnData = rawData;
			}

			int addCache = 0;
			switch (MimeType)
			{
			case "application/javascript":
			case "text/html":
			case "text/css":
				addCache = 86400; // 1 day
				break;

			case "image/svg+xml":
			case "image/png":
			case "image/x-icon":
				addCache = 86400 * 7; // 1 week
				break;
			}

			if (addCache > 0)
			{
				// Cache static files
				response.Headers[HttpResponseHeader.CacheControl] = $"max-age={addCache},public";
				response.Headers[HttpResponseHeader.Expires]
					= Util.GetNow().AddSeconds(addCache).ToUniversalTime().ToString("r");
				response.Headers[HttpResponseHeader.LastModified]
					= lastWrite.ToUniversalTime().ToString("r");
				response.Headers[HttpResponseHeader.ETag] = $"\"{lastWrite.ToUnix()}\"";
			}

			response.ContentLength64 = returnData.Length;
			response.ContentEncoding = Encoding.UTF8;
			response.ContentType = MimeType ?? "text/plain";

			return returnData;
		}
	}
}
