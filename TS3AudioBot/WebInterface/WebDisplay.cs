using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.IO;
using TS3AudioBot.Helper;
using System.Web;

namespace TS3AudioBot.WebInterface
{
	class WebDisplay
	{
		private readonly Uri[] hostPaths;
		private readonly short port = 8080;
		private Dictionary<string, WebSite> sites;
		private WebSite index;

		public WebDisplay()
		{
			hostPaths = new[] {
				new Uri($"http://splamy.de:{port}/"),
				new Uri($"http://localhost:{port}/"),
			};
			Util.Init(ref sites);

			index = new WebFileSite("index.html", "text/html", new FileInfo("../../WebInterface/index.html"));
			PrepareSite(index);
			PrepareSite(index, string.Empty);
			PrepareSite(new WebFileSite("styles.css", "text/css", new FileInfo("../../WebInterface/styles.css")));
			PrepareSite(new WebFileSite("scripts.js", "application/javascript", new FileInfo("../../WebInterface/scripts.js")));
			PrepareSite(new WebStaticSite("jquery.js", "application/javascript", Util.GetResource("TS3AudioBot.WebInterface.jquery.js")));
			PrepareSite(new WebFileSite("history.html", "text/html", new FileInfo("../../WebInterface/history.html")));
			PrepareSite(new WebStaticSite("favicon.ico", "image/x-icon", Util.GetResource("TS3AudioBot.WebInterface.favicon.ico")));
		}

		public void EnterWebLoop()
		{
			using (var webListener = new HttpListener())
			{
				foreach (var host in hostPaths)
					webListener.Prefixes.Add(host.AbsoluteUri);
				//webListener.Prefixes.Add($"http://+:{port}/");

				try { webListener.Start(); }
				catch (HttpListenerException) { throw; } // TODO

				while (webListener.IsListening)
				{
					HttpListenerContext context;
					try { context = webListener.GetContext(); }
					catch (HttpListenerException) { throw; } // TODO
					catch (InvalidOperationException) { continue; } // TODO

					byte[] buffer = DisplayWebsite(context);

					using (var response = context.Response)
					{
						response.ContentEncoding = Encoding.UTF8;
						response.ContentLength64 = buffer.Length;
						response.OutputStream.Write(buffer, 0, buffer.Length);
					}
				}
			}
		}

		public void PrepareSite(WebSite site) => PrepareSite(site, site.SitePath);
		public void PrepareSite(WebSite site, string page)
		{
			var genUrl = new Uri(new Uri("http://localhost"), page);
			sites.Add(genUrl.AbsolutePath, site);
		}

		private byte[] DisplayWebsite(HttpListenerContext context)
		{
			var request = context.Request;
			Console.WriteLine("Requested: {0}", request.Url.PathAndQuery);

			foreach (var host in hostPaths)
			{
				WebSite site;
				if (!sites.TryGetValue(request.Url.AbsolutePath, out site))
					continue;

				if (site == index)
				{
					var query = HttpUtility.ParseQueryString(request.Url.Query);

					bool isContent = query["content"] == "true";
					if (isContent)
					{
						var qSite = query["page"] ?? "menu";
						if (!sites.TryGetValue("/" + qSite, out site))
							continue;
					}
					else
					{
						// TODO: prepare index.html with querypage combined
					}
				}

				if (site.MimeType != null) context.Response.ContentType = site.MimeType;
				return site.GenerateSite();
			}

			return Encoding.UTF8.GetBytes("<div>Wrong turn!</div>");
		}
	}

	abstract class WebSite
	{
		public string SitePath { get; }
		public string MimeType { get; }

		public WebSite(string sitePath, string mimeType)
		{
			SitePath = sitePath;
			MimeType = mimeType + "; charset=utf-8";
		}

		public abstract byte[] GenerateSite();
	}

	class WebStaticSite : WebSite
	{
		private byte[] preGenerated;
		private string originalSite;

		public WebStaticSite(string sitePath, string mimetype, string siteContent) : base(sitePath, mimetype)
		{
			preGenerated = Encoding.UTF8.GetBytes(siteContent);
			originalSite = siteContent;
		}

		public override byte[] GenerateSite()
		{
			return preGenerated;
		}
	}

	class WebFileSite : WebSite
	{
		private byte[] preGenerated;
		private FileInfo liveFile;
		private DateTime lastAccess;

		public WebFileSite(string sitePath, string mimetype, FileInfo siteFile) : base(sitePath, mimetype)
		{
			liveFile = siteFile;
			lastAccess = DateTime.MinValue;
		}

		public override byte[] GenerateSite()
		{
			if (liveFile.LastAccessTime > lastAccess)
			{
				preGenerated = File.ReadAllBytes(liveFile.FullName);
			}
			return preGenerated;
		}
	}
}
