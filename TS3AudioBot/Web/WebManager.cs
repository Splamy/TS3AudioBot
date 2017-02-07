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

namespace TS3AudioBot.Web
{
	using Helper;
	using System;
	using System.Globalization;
	using System.Net;
	using System.Threading;

	public class WebManager : IDisposable
	{
		const string webRealm = "ts3ab";

		private Uri localhost;
		private Uri[] hostPaths;

		private HttpListener webListener;
		private Thread serverThread;
		private WebData webData;
		private bool startWebServer;

		public Api.WebApi Api { get; private set; }
		public Interface.WebDisplay Display { get; private set; }

		public WebManager(MainBot mainBot, WebData webd)
		{
			webData = webd;
			webListener = new HttpListener
			{
				AuthenticationSchemes = AuthenticationSchemes.Anonymous | AuthenticationSchemes.Basic | AuthenticationSchemes.Digest,
				Realm = webRealm,
			};

			ReloadHostPaths();
			InitializeSubcomponents(mainBot);
		}

		private void InitializeSubcomponents(MainBot mainBot)
		{
			startWebServer = false;
			if (webData.EnableApi)
			{
				Api = new Api.WebApi(mainBot);
				startWebServer = true;
			}
			if (webData.EnableWebinterface)
			{
				Display = new Interface.WebDisplay(mainBot);
				startWebServer = true;
			}
		}

		private void ReloadHostPaths()
		{
			localhost = new Uri($"http://localhost:{webData.Port}/");

			if (Util.IsAdmin || Util.IsLinux) // todo: hostlist
			{
				var addrs = webData.HostAddress.SplitNoEmpty(' ');
				hostPaths = new Uri[addrs.Length + 1];
				hostPaths[0] = localhost;

				for (int i = 0; i < addrs.Length; i++)
				{
					var uriBuilder = new UriBuilder(addrs[i]);
					uriBuilder.Port = webData.Port;
					hostPaths[i + 1] = uriBuilder.Uri;
				}
			}
			else
			{
				Log.Write(Log.Level.Warning, "App launched without elevated rights. Only localhost will be availbale as api server.");
				hostPaths = new[] { localhost };
			}

			webListener.Prefixes.Clear();
			foreach (var host in hostPaths)
				webListener.Prefixes.Add(host.AbsoluteUri);
		}

		public void StartServerAsync()
		{
			if (!startWebServer)
				return;

			serverThread = new Thread(EnterWebLoop);
			serverThread.Name = "WebInterface";
			serverThread.Start();
		}

		public void EnterWebLoop()
		{
			if (!startWebServer)
				return;

			try { webListener.Start(); }
			catch (HttpListenerException ex)
			{
				Log.Write(Log.Level.Error, "The webserver could not be started ({0})", ex.Message);
				return;
			} // TODO

			while (webListener?.IsListening ?? false)
			{
				try
				{
					HttpListenerContext context = webListener.GetContext();

					Log.Write(Log.Level.Info, "{0} Requested: {1}", context.Request.RemoteEndPoint.Address, context.Request.Url.PathAndQuery);
					if (context.Request.Url.AbsolutePath.StartsWith("/api/", true, CultureInfo.InvariantCulture))
						Api?.DispatchCall(context);
					else
						Display?.DispatchCall(context);
				}
				catch (HttpListenerException) { break; }
				catch (InvalidOperationException) { break; }
			}
		}

		public void Dispose()
		{
			webListener?.Stop();
			webListener = null;
		}
	}

	public class WebData : ConfigData
	{
		[Info("a space seperated list of all urls the web api should be possible to be accessed with", "")]
		public string HostAddress { get; set; }

		[Info("the port for the api server", "8180")]
		public ushort Port { get; set; }

		[Info("if you want to start the web api server.", "false")]
		public bool EnableApi { get; set; }

		[Info("if you want to start the webinterface server", "false")]
		public bool EnableWebinterface { get; set; }
	}
}
