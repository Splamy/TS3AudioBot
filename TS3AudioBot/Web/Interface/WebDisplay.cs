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

		public WebDisplay(MainBot bot) : base(bot)
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

	/*
	abstract class WebEvent : WebStaticSite
	{
		public override string MimeType { set { throw new InvalidOperationException(); } }
		private List<HttpListenerResponse> response;

		public WebEvent(string sitePath) : base(sitePath)
		{
			response = new List<HttpListenerResponse>();
			mimeType = "text/event-stream";
		}

		public sealed override PreparedData PrepareSite(UriExt url) => new PreparedData(long.MaxValue, null);
		public sealed override void PrepareHeader(HttpListenerContext context, PreparedData callData)
		{
			base.PrepareHeader(context, callData);
			context.Response.KeepAlive = true;
		}
		public sealed override void GenerateSite(HttpListenerContext context, PreparedData callData)
		{
			response.Add(context.Response);
			InvokeEvent();
		}

		public void InvokeEvent()
		{
			string eventText = "data: " + GetData() + "\n\n";
			var data = Encoding.GetBytes(eventText);
			for (int i = 0; i < response.Count; i++)
			{
				try
				{
					response[i].OutputStream.Write(data, 0, data.Length);
					response[i].OutputStream.Flush();
				}
				catch (Exception ex)
					when (ex is HttpListenerException || ex is InvalidOperationException || ex is IOException)
				{
					response.RemoveAt(i);
					i--;
				}
			}
		}

		protected abstract string GetData();

		public override void FinalizeResponse(HttpListenerContext context)
		{
			context.Response.OutputStream.Flush();
		}
	}
	*/
}
