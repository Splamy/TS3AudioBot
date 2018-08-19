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
	using System.Linq;
	using System.Net;
	using System.Text;

	public sealed class WebDisplay : WebComponent, IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly SiteMapper map = new SiteMapper();

		public ISiteProvider Site404 { get; }

		public static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>
		{
			{ ".js", "application/javascript" },
			{ ".json", "application/json" },
			{ ".html", "text/html" },
			{ ".css", "text/css" },
			{ ".ico", "image/x-icon" },
			{ ".png", "image/png" },
			{ ".svg", "	image/svg+xml" },
			// Custom
			{ ".map", "text/plain" },
			{ ".less", "text/css" },
		};

		public WebDisplay(Config.ConfWebInterface webData)
		{
			DirectoryInfo baseDir = null;
			if (string.IsNullOrEmpty(webData.Path))
			{
				for (int i = 0; i < 5; i++)
				{
					var up = Path.Combine(Enumerable.Repeat("..", i).ToArray());
					var checkDir = Path.Combine(up, "WebInterface");
					if (Directory.Exists(checkDir))
					{
						baseDir = new DirectoryInfo(checkDir);
						break;
					}
				}
			}
			else if (Directory.Exists(webData.Path))
				baseDir = new DirectoryInfo(webData.Path);

			if (baseDir == null)
			{
				Log.Error("Can't find a WebInterface path to host. Try specifying the path to host in the config");
				return;
			}

			var dir = new FolderProvider(baseDir);
			map.Map("/", dir);
			map.Map("/site/", dir);
			map.Map("/openapi/", new FolderProvider(new DirectoryInfo(Path.Combine(baseDir.FullName, "openapi"))));

			Site404 = map.TryGetSite(new Uri("http://localhost/404.html"));
			map.Map("/", map.TryGetSite(new Uri("http://localhost/index.html")));
		}

		public override void DispatchCall(HttpListenerContext context)
		{
			// GetWebsite will always return either the found website or the default 404
			var site = GetWebsite(context.Request.Url);
			if (site == null)
			{
				Log.Error("No site found");
				return;
			}

			var data = site.GetData();
			if (data == null)
			{
				Log.Error("Site has not data");
				return;
			}

			// Prepare Header
			var response = context.Response;
			response.StatusCode = (int)HttpStatusCode.OK;
			response.ContentLength64 = data.Length;
			response.ContentEncoding = Encoding.UTF8;
			response.ContentType = site.MimeType ?? "text/plain";

			try
			{
				// Write Data
				using (var responseStream = response.OutputStream)
					responseStream.Write(data, 0, data.Length);
			}
			catch (IOException) { }
			catch (Exception ex) { Log.Warn(ex, "Problem handling web request: {0}", ex.Message); }
		}

		private ISiteProvider GetWebsite(Uri url)
		{
			if (url == null) return Site404;
			return map.TryGetSite(url) ?? Site404;
		}

		public void Dispose()
		{
		}
	}
}
