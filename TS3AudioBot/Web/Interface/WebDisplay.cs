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
	using System.Collections.Generic;
	using System.IO;
	using System.Net;
	using System.Text;

	public sealed class WebDisplay : WebComponent, IDisposable
	{
		public ISiteProvider Site404 { get; private set; }
		private SiteMapper map = new SiteMapper();

		public static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>()
		{
			{ ".js", "application/javascript" },
			{ ".html", "text/html" },
			{ ".css", "text/css" },
			{ ".ico", "image/x-icon" },
			{ ".png", "image/png" },
			{ ".svg", "	image/svg+xml" },
			// Custom
			{ ".map", "text/plain" },
			{ ".less", "text/css" },
		};

		public WebDisplay(Core core) : base(core)
		{
			var baseDir = new DirectoryInfo(Path.Combine("..", "..", "Web", "Interface"));
			var dir = new FolderProvider(baseDir);
			map.Map("/", dir);
			map.Map("/site/", dir);

			Site404 = map.TryGetSite(new Uri("http://localhost/404.html"));
			map.Map("/", map.TryGetSite(new Uri("http://localhost/index.html")));
		}

		public override void DispatchCall(HttpListenerContext context)
		{
			// GetWebsite will always return either the found website or the default 404
			var site = GetWebsite(context.Request.Url);

			var data = site.GetData();

			// Prepare Header
			var response = context.Response;
			response.StatusCode = (int)HttpStatusCode.OK;
			response.ContentLength64 = data.Length;
			response.ContentEncoding = Encoding.UTF8;
			response.ContentType = site.MimeType ?? "text/plain";

			// Write Data
			context.Response.OutputStream.Write(data, 0, data.Length);

			// Close Stream
			context.Response.OutputStream.Flush();
			context.Response.OutputStream.Dispose();
		}

		private ISiteProvider GetWebsite(Uri url)
		{
			if (url == null) return Site404;

			var site = map.TryGetSite(url);
			if (site != null)
				return site;

			return Site404;
		}

		public void Dispose()
		{
		}
	}
}
