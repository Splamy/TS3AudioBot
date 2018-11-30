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
			{ ".svg", "image/svg+xml" },
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
			{
				baseDir = new DirectoryInfo(webData.Path);
			}

			if (baseDir is null)
			{
				Log.Error("Can't find a WebInterface path to host. Try specifying the path to host in the config");
				return;
			}

			map.Map("/", new FolderProvider(baseDir));

#if DEBUG
			// include debug out folder
			map.Map("/", new FolderProvider(new DirectoryInfo(Path.Combine(baseDir.FullName, "html"))));
			map.Map("/", new FolderProvider(new DirectoryInfo(Path.Combine(baseDir.FullName, "out"))));
#endif

			map.Map("/openapi/", new FolderProvider(new DirectoryInfo(Path.Combine(baseDir.FullName, "openapi"))));

			Site404 = map.TryGetSite(new Uri("http://localhost/404.html"));
			map.Map("/", new FileRedirect(map, "", "index.html"));
		}

		public override bool DispatchCall(HttpListenerContext context)
		{
			// GetWebsite will always return either the found website or the default 404
			var site = GetWebsite(context.Request.Url);
			if (site is null)
			{
				Log.Error("No site found");
				return false;
			}

			var request = context.Request;
			var response = context.Response;

			var data = site.GetData(request, response);
			if (data is null)
			{
				Log.Error("Site has no data");
				return false;
			}

			// Prepare Header
			if (site == Site404)
				response.StatusCode = (int)HttpStatusCode.NotFound;
			response.KeepAlive = true;

			// Write Data
			using (var responseStream = response.OutputStream)
			{
				try
				{
					responseStream.Write(data, 0, data.Length);
				}
				catch (IOException) { }
				catch (Exception ex) { Log.Warn(ex, "Problem handling web request: {0}", ex.Message); }
				return true;
			}
		}

		private ISiteProvider GetWebsite(Uri url)
		{
			if (url is null) return Site404;
			return map.TryGetSite(url) ?? Site404;
		}

		public void Dispose()
		{
		}
	}
}
