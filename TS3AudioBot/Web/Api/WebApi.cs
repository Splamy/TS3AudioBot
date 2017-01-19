namespace TS3AudioBot.Web.Api
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Helper;
	using History;
	using HtmlAgilityPack;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Threading;
	using System.Web;
	using TS3Client.Messages;

	class WebApi
	{
		private WebApiData webApiData;
		private readonly Uri localhost;
		private readonly Uri[] hostPaths;
		private HttpListener webListener;
		private Thread serverThread;

		public WebApi(WebApiData wad)
		{
			webApiData = wad;

			localhost = new Uri($"http://localhost:{wad.Port}/");
		}

		public void StartServerAsync()
		{
			serverThread = new Thread(EnterWebLoop);
			serverThread.Name = "WebInterface";
			serverThread.Start();
		}

		public void EnterWebLoop()
		{
			if (!webApiData.Enabled)
				return;

			using (webListener = new HttpListener())
			{
				foreach (var host in hostPaths)
					webListener.Prefixes.Add(host.AbsoluteUri);

				try { webListener.Start(); }
				catch (HttpListenerException ex)
				{
					Log.Write(Log.Level.Error, "The web api server could not be started ({0})", ex.Message);
					return;
				} // TODO

				while (webListener.IsListening)
				{
					HttpListenerContext context;
					try { context = webListener.GetContext(); }
					catch (HttpListenerException) { break; }
					catch (InvalidOperationException) { break; }

					Log.Write(Log.Level.Info, "API Request: {0}", context.Request.Url.PathAndQuery);
					var requestUrl = new UriExt(context.Request.Url);

					// TODO process work here
				}
			}
		}
	}

	class WebApiData : ConfigData
	{
		[Info("the port for the api server", "8080")]
		public ushort Port { get; set; }

		[Info("if you want to start the web api server.", "false")]
		public bool Enabled { get; set; }

		[Info("a comma seperated list of all urls the web api should be possible to be accessed with", "")]
		public string HostAddress { get; set; }


	}
}
