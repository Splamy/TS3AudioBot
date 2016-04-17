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
using HtmlAgilityPack;

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

			index = new WebFileSite("index.html", new FileInfo("../../WebInterface/index.html")) { MimeType = "text/html" };
			PrepareSite(index);
			PrepareSite(index, string.Empty);
			PrepareSite(new WebFileSite("styles.css", new FileInfo("../../WebInterface/styles.css")) { MimeType = "text/css" });
			PrepareSite(new WebFileSite("scripts.js", new FileInfo("../../WebInterface/scripts.js")) { MimeType = "application/javascript" });
			PrepareSite(new WebStaticSite("jquery.js", Util.GetResource("TS3AudioBot.WebInterface.jquery.js")) { MimeType = "application/javascript" });
			PrepareSite(new WebFileSite("history.html", new FileInfo("../../WebInterface/history.html")) { MimeType = "text/html" });
			PrepareSite(new WebStaticSite("favicon.ico", Util.GetResource("TS3AudioBot.WebInterface.favicon.ico")) { MimeType = "image/x-icon", Encoding = Encoding.ASCII });
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

		private static readonly Uri helpUri = new Uri("http://localhost");
		public void PrepareSite(WebSite site) => PrepareSite(site, site.SitePath);
		public void PrepareSite(WebSite site, string page)
		{
			var genUrl = new Uri(helpUri, page);
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

				return site.GenerateSite(context);
			}

			return Encoding.UTF8.GetBytes("<div>Wrong turn!</div>");
		}
	}

	class FileProvider
	{
		private byte[] rawData;
		private bool loadedOnce = false;
		private FileInfo file = null;
		private DateTime lastWrite = DateTime.MinValue;
		private string resourceName = null;
		public bool HasChanged => !CheckFile();

		public FileProvider(FileInfo file)
		{
			if (file == null)
				throw new ArgumentNullException(nameof(file));
		}

		public FileProvider(string resourceName)
		{
			if (resourceName == null)
				throw new ArgumentNullException(nameof(resourceName));
		}

		private bool CheckFile()
		{
			if (resourceName != null)
				return loadedOnce;
			else if (file != null)
				return file.LastWriteTime == lastWrite;
			else
				throw new InvalidOperationException();
		}

		public byte[] FetchFile()
		{
			if (!HasChanged)
				return rawData;

			if (resourceName != null)
			{
				rawData = Util.GetResource(resourceName);
				loadedOnce = true;
			}
			else if (file != null)
			{
				rawData = File.ReadAllBytes(file.FullName);
				lastWrite = file.LastWriteTime;
			}
			return rawData;
		}
	}

	abstract class WebSite
	{
		public string SitePath { get; }
		private string mimeType = "text/html";
		public string MimeType
		{
			get { return mimeType + (Encoding == Encoding.UTF8 ? "; charset=utf-8" : ""); }
			set { mimeType = value; }
		}
		public Encoding Encoding { get; set; } = Encoding.UTF8;

		public WebSite(string sitePath)
		{
			SitePath = sitePath;
		}

		public byte[] GenerateSite(HttpListenerContext context)
		{
			if (MimeType != null) context.Response.ContentType = MimeType;
			if (Encoding != null) context.Response.ContentEncoding = Encoding;
			return GenerateSite();
		}

		protected abstract byte[] GenerateSite();
	}

	class WebStaticSite : WebSite
	{
		private byte[] content;

		public WebStaticSite(string sitePath, byte[] siteContent) : base(sitePath)
		{
			content = siteContent;
		}

		protected override byte[] GenerateSite()
		{
			return content;
		}
	}

	class WebFileSite : WebSite
	{
		private byte[] preGenerated;
		private FileInfo liveFile;
		private DateTime lastAccess;

		public WebFileSite(string sitePath, FileInfo siteFile) : base(sitePath)
		{
			liveFile = siteFile;
			lastAccess = DateTime.MinValue;
		}

		protected override byte[] GenerateSite()
		{
			if (liveFile.LastAccessTime > lastAccess)
			{
				preGenerated = File.ReadAllBytes(liveFile.FullName);
			}
			return preGenerated;
		}
	}

	class WebIndexFile : WebSite
	{
		List<byte[]> preloadedBlocks;

		public WebIndexFile(string sitePath) : base(sitePath)
		{
			var hdoc = new HtmlDocument();
			hdoc.Load(sitePath);
			var nodes = hdoc.DocumentNode.SelectNodes("/html/body/...");
		}

		protected override byte[] GenerateSite()
		{
			throw new NotImplementedException();
		}
	}
}
