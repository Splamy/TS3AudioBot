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
		// Special sites
		public WebSite Index { get; private set; }
		public WebSite Site404 { get; private set; }

		public WebDisplay()
		{
			hostPaths = new[] {
				new Uri($"http://splamy.de:{port}/"),
				new Uri($"http://localhost:{port}/"),
			};
			Util.Init(ref sites);

			Index = new WebIndexFile("index.html", new FileProvider(new FileInfo("../../WebInterface/index.html")), GetWebsite) { MimeType = "text/html" };
			PrepareSite(Index);
			PrepareSite(Index, string.Empty);
			PrepareSite(new WebStaticSite("styles.css", new FileInfo("../../WebInterface/styles.css")) { MimeType = "text/css" });
			PrepareSite(new WebStaticSite("scripts.js", new FileInfo("../../WebInterface/scripts.js")) { MimeType = "application/javascript" });
			PrepareSite(new WebStaticSite("jquery.js", "TS3AudioBot.WebInterface.jquery.js") { MimeType = "application/javascript" });
			PrepareSite(new WebStaticSite("history.html", new FileInfo("../../WebInterface/history.html")) { MimeType = "text/html" });
			PrepareSite(new WebStaticSite("favicon.ico", "TS3AudioBot.WebInterface.favicon.ico") { MimeType = "image/x-icon", Encoding = Encoding.ASCII });
			Site404 = new WebStaticSite("404.html", "TS3AudioBot.WebInterface.favicon.ico") { MimeType = "text/html" };
			PrepareSite(Site404);
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

					Console.Write("Requested: {0} (", context.Request.Url.PathAndQuery);
					var site = GetWebsite(context.Request.Url); // is not null

					using (var response = context.Response)
					{
						var prepData = site.PrepareSite(context);
						response.ContentLength64 = prepData.Length;
						response.ContentEncoding = site.Encoding ?? Encoding.UTF8;
						response.ContentType = site.MimeType ?? "text/html";
						site.GenerateSite(context, prepData);
						response.OutputStream.Flush();
					}

					Console.WriteLine(")");
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

		private WebSite GetWebsite(Uri url)
		{
			Console.Write("Fetch: {0} ", url?.PathAndQuery);
			if (url == null) return Site404;

			foreach (var host in hostPaths)
			{
				WebSite site;
				if (!sites.TryGetValue(url.AbsolutePath, out site))
					continue;

				return site;
			}
			return Site404;
		}
	}

	class FileProvider
	{
		private byte[] rawData;
		private bool loadedOnce = false;
		private FileInfo file = null;
		public FileInfo WebFile
		{
			get { return file; }
			set { file = value; lastWrite = DateTime.MinValue; }
		}
		private DateTime lastWrite = DateTime.MinValue;
		private string resourceName = null;
		public string ResourceName
		{
			get { return resourceName; }
			set { resourceName = value; loadedOnce = false; }
		}
		public bool HasChanged => !CheckFile();

		public FileProvider(FileInfo file)
		{
			if (file == null)
				throw new ArgumentNullException(nameof(file));
			WebFile = file;
		}

		public FileProvider(string resourceName)
		{
			if (resourceName == null)
				throw new ArgumentNullException(nameof(resourceName));
			ResourceName = resourceName;
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

		public abstract PreparedData PrepareSite(HttpListenerContext context);

		public abstract void GenerateSite(HttpListenerContext context, PreparedData callData);
	}

	struct PreparedData
	{
		public int Length { get; }
		public object Data { get; }

		public PreparedData(int len, object obj) { Length = len; Data = obj; }
	}

	class WebStaticSite : WebSite
	{
		protected FileProvider provider;

		public WebStaticSite(string sitePath, FileProvider provider) : base(sitePath)
		{
			this.provider = provider;
		}

		public WebStaticSite(string sitePath, FileInfo file) : this(sitePath, new FileProvider(file)) { }

		public WebStaticSite(string sitePath, string resourcePath) : this(sitePath, new FileProvider(resourcePath)) { }

		public override PreparedData PrepareSite(HttpListenerContext context)
		{
			byte[] prepData = provider.FetchFile();
			return new PreparedData(prepData.Length, prepData);
		}

		public override void GenerateSite(HttpListenerContext context, PreparedData callData)
		{
			byte[] prepData = (byte[])callData.Data;
			context.Response.OutputStream.Write(prepData, 0, callData.Length);
		}
	}

	class WebIndexFile : WebStaticSite
	{
		int preloadLen;
		byte[][] preloadedBlocks;
		private Func<Uri, WebSite> siteFinder;

		public WebIndexFile(string sitePath, FileProvider provider, Func<Uri, WebSite> finder) : base(sitePath, provider)
		{
			siteFinder = finder;
			preloadedBlocks = new byte[2][];
		}

		private void ParseHtml()
		{
			if (!provider.HasChanged)
				return;

			byte[] fetchBlock = provider.FetchFile();
			string htmlContent = Encoding.UTF8.GetString(fetchBlock);

			var hdoc = new HtmlDocument();
			hdoc.LoadHtml(htmlContent);
			var node = hdoc.DocumentNode.SelectSingleNode("//div[@id='jqblock'][1]");

			if (node == null)
			{
				preloadedBlocks[0] = fetchBlock;
				preloadedBlocks[1] = new byte[0];
			}
			else
			{
				int split1 = node.StreamPosition;
				int divLen = node.OuterHtml.Length;
				int split2 = split1 + divLen;
				int split2len = fetchBlock.Length - split2;
				preloadedBlocks[0] = new byte[split1];
				preloadedBlocks[1] = new byte[split2len];

				Array.Copy(fetchBlock, 0, preloadedBlocks[0], 0, preloadedBlocks[0].Length);
				Array.Copy(fetchBlock, split2, preloadedBlocks[1], 0, preloadedBlocks[1].Length);
			}

			preloadLen = preloadedBlocks[0].Length + preloadedBlocks[1].Length;
		}

		public override PreparedData PrepareSite(HttpListenerContext context)
		{
			ParseHtml();
			Uri reqUri = context.Request.Url;
			var query = HttpUtility.ParseQueryString(reqUri.Query);

			var querySiteName = query["page"] ?? string.Empty;

			var querySite = siteFinder(new Uri(new Uri(reqUri.GetLeftPart(UriPartial.Authority)), querySiteName));

			if (query["content"] == "true")
			{
				if (querySite == this)
				{
					querySite = siteFinder(null);
				}
				var prepInside = querySite.PrepareSite(context);
				return new PreparedData(prepInside.Length, new ContentSite(querySite, prepInside, true));
			}
			else
			{
				var prepInside = querySite.PrepareSite(context);
				return new PreparedData(preloadLen + prepInside.Length, new ContentSite(querySite, prepInside, false));
			}
		}

		public override void GenerateSite(HttpListenerContext context, PreparedData callData)
		{
			ContentSite contentData = (ContentSite)callData.Data;
			// An other site has been requested via this one
			if (contentData.ContentOnly)
			{
				contentData.Site.GenerateSite(context, callData);
			}
			// The own site is called, we have to insert the inner data
			else
			{
				// write first part
				context.Response.OutputStream.Write(preloadedBlocks[0], 0, preloadedBlocks[0].Length);
				// write the inner data
				contentData.Site.GenerateSite(context, contentData.Data);
				// write outer part
				context.Response.OutputStream.Write(preloadedBlocks[1], 0, preloadedBlocks[1].Length);
			}
		}

		class ContentSite
		{
			public WebSite Site { get; }
			public PreparedData Data { get; }
			public bool ContentOnly { get; }

			public ContentSite(WebSite site, PreparedData data, bool contentOnly)
			{
				Site = site;
				Data = data;
				ContentOnly = contentOnly;
			}
		}
	}
}
