using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using HtmlAgilityPack;
using TS3AudioBot.Helper;
using TS3AudioBot.History;

namespace TS3AudioBot.WebInterface
{
	class WebDisplay
	{
		private readonly Uri[] hostPaths;
		private const short port = 8080;
		private readonly Dictionary<string, WebSite> sites;
		public static readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
		private readonly MainBot mainBot;
		private static readonly Uri localhost = new Uri($"http://localhost:{port}/");
		// Special sites
		public WebSite Index { get; private set; }
		public WebSite Site404 { get; private set; }

		public WebDisplay(MainBot bot)
		{
			mainBot = bot;

			if (Util.IsAdmin)
			{
				hostPaths = new[] {
					new Uri($"http://splamy.de:{port}/"),
					localhost,
				};
			}
			else
			{
				Log.Write(Log.Level.Warning, "App launched without elevated rights. Only localhost will be availbale as webserver.");
				hostPaths = new[] { localhost };
			}
			Util.Init(ref sites);

			Index = new WebIndexFile("index.html", new FileProvider(new FileInfo("../../WebInterface/index.html")), GetWebsite) { MimeType = "text/html" };
			PrepareSite(Index);
			PrepareSite(Index, string.Empty);
			PrepareSite(new WebStaticSite("styles.css", new FileInfo("../../WebInterface/styles.css")) { MimeType = "text/css" });
			PrepareSite(new WebStaticSite("scripts.js", new FileInfo("../../WebInterface/scripts.js")) { MimeType = "application/javascript" });
			PrepareSite(new WebStaticSite("jquery.js", "TS3AudioBot.WebInterface.jquery.js") { MimeType = "application/javascript" });
			PrepareSite(new WebStaticSite("favicon.ico", "TS3AudioBot.WebInterface.favicon.ico") { MimeType = "image/x-icon", Encoding = Encoding.ASCII });
			Site404 = new WebStaticSite("404", "TS3AudioBot.WebInterface.favicon.ico") { MimeType = "text/plain" };
			PrepareSite(Site404);
			PrepareSite(new WebHistorySearch("historysearch", mainBot) { MimeType = "text/plain" });
			var historystatic = new WebHistorySearchList("historystatic", mainBot) { MimeType = "text/html" };
			PrepareSite(historystatic);
			PrepareSite(new WebJSFillSite("history", new FileProvider(new FileInfo("../../WebInterface/history.html")), historystatic) { MimeType = "text/html" });
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
						var prepData = site.PrepareSite(new UriExt(context.Request.Url));
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

		public void PrepareSite(WebSite site) => PrepareSite(site, site.SitePath);
		public void PrepareSite(WebSite site, string page)
		{
			var genUrl = new Uri(localhost, page);
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
			{
				file.Refresh();
				return file.LastWriteTime == lastWrite;
			}
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

	struct PreparedData
	{
		public int Length { get; }
		public object Context { get; }

		public PreparedData(int len, object obj) { Length = len; Context = obj; }
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

		public abstract PreparedData PrepareSite(UriExt url);

		public virtual void GenerateSite(HttpListenerContext context, PreparedData callData)
		{
			byte[] prepData = (byte[])callData.Context;
			context.Response.OutputStream.Write(prepData, 0, callData.Length);
		}
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

		public override PreparedData PrepareSite(UriExt url)
		{
			byte[] prepData = provider.FetchFile();
			return new PreparedData(prepData.Length, prepData);
		}
	}

	class WebJSFillSite : WebStaticSite
	{
		protected int startB;
		protected int lengthA, lengthB;
		protected int PrepLen => lengthA + lengthB;
		readonly WebSite innerSite;

		public WebJSFillSite(string sitePath, FileProvider provider) : base(sitePath, provider) { }
		public WebJSFillSite(string sitePath, FileProvider provider, WebSite innerSite) : this(sitePath, provider)
		{
			this.innerSite = innerSite;
		}

		protected void ParseHtml()
		{
			if (!provider.HasChanged)
				return;

			byte[] fetchBlock = provider.FetchFile();
			string htmlContent = Encoding.UTF8.GetString(fetchBlock);

			var hdoc = new HtmlDocument();
			hdoc.LoadHtml(htmlContent);
			var node = hdoc.DocumentNode.SelectSingleNode("//div[@data-jqload][1]");

			if (node == null)
			{
				lengthA = fetchBlock.Length;
				startB = 0; lengthB = 0;
			}
			else
			{
				int split1 = node.StreamPosition;
				int divLen = node.NextSibling.StreamPosition - node.StreamPosition;
				int split2 = split1 + divLen;
				int split2len = fetchBlock.Length - split2;

				// startA is always 0
				startB = split2;
				lengthA = split1;
				lengthB = split2len;
			}
		}

		public override PreparedData PrepareSite(UriExt url)
		{
			ParseHtml();
			byte[] fetchBlock = provider.FetchFile();

			var prepInside = innerSite.PrepareSite(url);
			return new PreparedData(PrepLen + prepInside.Length, new ContentSite(fetchBlock, innerSite, prepInside));
		}

		public override void GenerateSite(HttpListenerContext context, PreparedData callData)
		{
			ContentSite contentData = (ContentSite)callData.Context;
			// write first part
			context.Response.OutputStream.Write(contentData.OwnContent, 0, lengthA);
			// write the inner data
			contentData.Site.GenerateSite(context, contentData.PreparedData);
			// write outer part
			context.Response.OutputStream.Write(contentData.OwnContent, startB, lengthB);
		}

		protected class ContentSite
		{
			public byte[] OwnContent { get; }
			public WebSite Site { get; }
			public PreparedData PreparedData { get; }

			public ContentSite(byte[] fetchBlock, WebSite site, PreparedData data)
			{
				OwnContent = fetchBlock;
				Site = site;
				PreparedData = data;
			}
		}
	}

	class WebIndexFile : WebJSFillSite
	{
		private readonly Func<Uri, WebSite> siteFinder;

		public WebIndexFile(string sitePath, FileProvider provider, Func<Uri, WebSite> finder) : base(sitePath, provider)
		{
			siteFinder = finder;
		}

		public override PreparedData PrepareSite(UriExt url)
		{
			ParseHtml();
			byte[] fetchBlock = provider.FetchFile();
			var query = url.QueryParam;

			var querySiteName = query["page"] ?? string.Empty;

			var querySite = siteFinder(new Uri(new Uri(url.GetLeftPart(UriPartial.Authority)), querySiteName));
			if (querySite == this)
				querySite = siteFinder(null);

			var prepInside = querySite.PrepareSite(url);
			return new PreparedData(PrepLen + prepInside.Length, new ContentSite(fetchBlock, querySite, prepInside));
		}
	}

	// Specialized Sites

	class WebHistorySearch : WebSite
	{
		private readonly HistoryManager history;

		public WebHistorySearch(string sitePath, MainBot bot) : base(sitePath)
		{
			history = bot.HistoryManager;
		}

		public override PreparedData PrepareSite(UriExt url)
		{
			var search = ParseSearchQuery(url);
			var result = history.Search(search).Select(e => new
			{
				id = e.Id,
				atype = e.AudioType,
				playcnt = e.PlayCount,
				title = e.ResourceTitle,
				time = e.Timestamp,
				userid = e.UserInvokeId
			});

			string serialized = WebDisplay.serializer.Serialize(result);
			var dataArray = Encoding.GetBytes(serialized);

			return new PreparedData(dataArray.Length, dataArray);
		}

		public static SeachQuery ParseSearchQuery(UriExt url)
		{
			var query = url.QueryParam;

			string tmpValue;
			var search = new SeachQuery();

			DateTime lastinvoked;
			if ((tmpValue = query["lastinvoked"]) != null && DateTime.TryParse(tmpValue, out lastinvoked))
				search.LastInvokedAfter = lastinvoked;

			uint userId;
			if ((tmpValue = query["userid"]) != null && uint.TryParse(tmpValue, out userId))
				search.UserId = userId;

			int max;
			if ((tmpValue = query["max"]) != null && int.TryParse(tmpValue, out max))
				search.MaxResults = max;

			search.TitlePart = query["title"];

			return search;
		}
	}

	class WebHistorySearchList : WebSite
	{
		private readonly HistoryManager history;

		public WebHistorySearchList(string sitePath, MainBot bot) : base(sitePath) { history = bot.HistoryManager; }

		public override PreparedData PrepareSite(UriExt url)
		{
			var search = WebHistorySearch.ParseSearchQuery(url);
			var result = history.Search(search);

			var strb = new StringBuilder();
			foreach (var entry in result)
			{
				strb.Append("<tr><td>").Append(entry.Id)
					.Append("</td><td>").Append(entry.UserInvokeId)
					.Append("</td><td class=\"fillwrap\">").Append(entry.ResourceTitle)
					.Append("</td><td>Options</td></tr>");
			}
			string finString = strb.ToString();
			byte[] finBlock = Encoding.GetBytes(finString);

			return new PreparedData(finString.Length, finBlock);
		}
	}

	// Helper

	class UriExt : Uri
	{
		private NameValueCollection queryParam = null;
		public NameValueCollection QueryParam => queryParam ?? (queryParam = HttpUtility.ParseQueryString(Query));
		public UriExt(Uri copy) : base(copy.OriginalString) { }
	}
}
