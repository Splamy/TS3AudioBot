using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using HtmlAgilityPack;
using TS3AudioBot.Helper;
using TS3AudioBot.History;
using System.Threading;

namespace TS3AudioBot.WebInterface
{
	public class WebDisplay : IDisposable
	{
		private readonly Uri[] hostPaths;
		private const short port = 8080;
		private readonly Dictionary<string, WebSite> sites;
		private readonly MainBot mainBot;
		private static readonly Uri localhost = new Uri($"http://localhost:{port}/");
		private HttpListener webListener;
		private Thread serverThread;
		// Special sites
		public WebSite Index { get; private set; }
		public WebSite Site404 { get; private set; }

		public WebDisplay(MainBot bot)
		{
			mainBot = bot;

			if (Util.IsAdmin || Util.IsLinux)
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
			PrepareSite(new WebStaticSite("main", new FileInfo("../../WebInterface/main.html")) { MimeType = "text/css" });
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
			PrepareSite(new WebStaticSite("playcontrols", new FileInfo("../../WebInterface/playcontrols.html")) { MimeType = "text/html" });
			PrepareSite(new WebPlayControls("control", mainBot) { MimeType = "text/html" });
			PrepareSite(new SongChangedEvent("playdata", mainBot));
			var devupdate = new SiteChangedEvent("devupdate");
			PrepareSite(devupdate);
			if (!Util.RegisterFolderEvents(new DirectoryInfo("../../WebInterface"), (s, e) =>
			{
				if (e.ChangeType == WatcherChangeTypes.Changed && !e.Name.EndsWith("~"))
					devupdate.InvokeEvent();
			}))
				Log.Write(Log.Level.Info, "Devupdate disabled");
		}

		public void StartServerAsync()
		{
			serverThread = new Thread(EnterWebLoop);
			serverThread.Name = "WebInterface";
			serverThread.Start();
		}

		public void EnterWebLoop()
		{
			using (webListener = new HttpListener())
			{
				foreach (var host in hostPaths)
					webListener.Prefixes.Add(host.AbsoluteUri);

				try { webListener.Start(); }
				catch (HttpListenerException ex)
				{
					Log.Write(Log.Level.Info, "The webserver could not be started ({0})", ex.Message);
					return;
				} // TODO

				while (webListener.IsListening)
				{
					HttpListenerContext context;
					try { context = webListener.GetContext(); }
					catch (HttpListenerException) { break; }
					catch (InvalidOperationException) { break; }

					Log.Write(Log.Level.Info, "Requested: {0}", context.Request.Url.PathAndQuery);
					var site = GetWebsite(context.Request.Url); // is not null

					var callData = site.PrepareSite(new UriExt(context.Request.Url));
					site.PrepareHeader(context, callData);
					site.GenerateSite(context, callData);
					site.FinalizeResponse(context);
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

		public void Dispose()
		{
			webListener?.Stop();
			webListener = null;
			Util.WaitForThreadEnd(serverThread, TimeSpan.FromMilliseconds(100));
			serverThread = null;
		}
	}

	public class FileProvider
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

	public struct PreparedData
	{
		public long Length { get; }
		public object Context { get; }

		public PreparedData(long len, object obj) { Length = len; Context = obj; }
	}

	public abstract class WebSite
	{
		public string SitePath { get; }
		protected string mimeType = "text/html";
		public virtual string MimeType
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

		public virtual void PrepareHeader(HttpListenerContext context, PreparedData callData)
		{
			var response = context.Response;
			response.StatusCode = (int)HttpStatusCode.OK;
			response.ContentLength64 = callData.Length;
			response.ContentEncoding = Encoding ?? Encoding.UTF8;
			response.ContentType = MimeType ?? "text/html";
		}

		public virtual void GenerateSite(HttpListenerContext context, PreparedData callData)
		{
			byte[] prepData = (byte[])callData.Context;
			context.Response.OutputStream.Write(prepData, 0, prepData.Length);
		}

		public virtual void FinalizeResponse(HttpListenerContext context)
		{
			context.Response.OutputStream.Flush();
			context.Response.OutputStream.Dispose();
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

	abstract class WebEvent : WebSite
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

	// Specialized Sites

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
				title = HttpUtility.HtmlEncode(e.ResourceTitle),
				time = e.Timestamp,
				userid = e.UserInvokeId
			});

			string serialized = Util.Serializer.Serialize(result);
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
					.Append("</td><td class=\"fillwrap\">").Append(HttpUtility.HtmlEncode(entry.ResourceTitle))
					.Append("</td><td>Options</td></tr>");
			}
			string finString = strb.ToString();
			byte[] finBlock = Encoding.GetBytes(finString);

			return new PreparedData(finString.Length, finBlock);
		}
	}

	class WebPlayControls : WebSite
	{
		AudioFramework audio;
		public WebPlayControls(string sitePath, MainBot bot) : base(sitePath)
		{
			audio = bot.AudioFramework;
		}

		public override PreparedData PrepareSite(UriExt url)
		{
			switch (url.QueryParam["op"])
			{
			default:
			case null: break;
			case "volume":
				var volumeStr = url.QueryParam["volume"];
				int volume;
				if (int.TryParse(volumeStr, out volume))
					audio.Volume = volume;
				break;

			case "prev": audio.Previous(); break;
			case "play": audio.Pause = !audio.Pause; break;
			case "next": audio.Next(); break;
			case "loop": audio.Repeat = !audio.Repeat; break;
			case "seek":
				var seekStr = url.QueryParam["pos"];
				double seek;
				if (double.TryParse(seekStr, out seek))
				{
					var pos = TimeSpan.FromSeconds(seek);
					if (pos >= TimeSpan.Zero && pos <= audio.Length)
						audio.Position = pos;
				}
				break;
			}
			return new PreparedData(0, new byte[0]);
		}
	}

	class SongChangedEvent : WebEvent
	{
		AudioFramework audio;
		TickWorker pushUpdate;
		public SongChangedEvent(string sitePath, MainBot bot) : base(sitePath)
		{
			audio = bot.AudioFramework;
			bot.AudioFramework.OnResourceStarted += Audio_OnResourceStarted;
			//pushUpdate = TickPool.RegisterTick(InvokeEvent, TimeSpan.FromSeconds(5), true);
		}

		private void Audio_OnResourceStarted(object sender, PlayData e)
		{
			InvokeEvent();
		}

		protected override string GetData()
		{
			if (audio.IsPlaying)
			{
				var data = new
				{
					hassong = true,
					titel = audio.CurrentPlayData.Resource.ResourceTitle,
					length = audio.Length,
					position = audio.Position,
					paused = audio.Pause,
					repeat = audio.Repeat,
					loop = audio.Loop,
					volume = audio.Volume,
				};
				return Util.Serializer.Serialize(data);
			}
			else
			{
				var data = new { hassong = false, };
				return Util.Serializer.Serialize(data);
			}
		}
	}

	class SiteChangedEvent : WebEvent
	{
		public SiteChangedEvent(string sitePath) : base(sitePath) { }

		protected override string GetData() => "update";
	}

	// Helper

	public class UriExt : Uri
	{
		private NameValueCollection queryParam = null;
		public NameValueCollection QueryParam => queryParam ?? (queryParam = HttpUtility.ParseQueryString(Query));
		public UriExt(Uri copy) : base(copy.OriginalString) { }
	}
}
