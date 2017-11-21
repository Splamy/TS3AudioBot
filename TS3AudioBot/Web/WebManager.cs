// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Web
{
	using Helper;
	using System;
	using System.Globalization;
	using System.Net;
	using System.Threading;

	public sealed class WebManager : IDisposable
	{
		public const string WebRealm = "ts3ab";

		private Uri localhost;
		private Uri[] hostPaths;

		private HttpListener webListener;
		private Thread serverThread;
		private readonly WebData webData;
		private bool startWebServer;

		public Api.WebApi Api { get; private set; }
		public Interface.WebDisplay Display { get; private set; }

		public WebManager(Core core, WebData webd)
		{
			webData = webd;
			webListener = new HttpListener
			{
				AuthenticationSchemes = AuthenticationSchemes.Anonymous | AuthenticationSchemes.Basic,
				Realm = WebRealm,
				AuthenticationSchemeSelectorDelegate = AuthenticationSchemeSelector,
			};

			InitializeSubcomponents(core);
		}

		private void InitializeSubcomponents(Core core)
		{
			startWebServer = false;
			if (webData.EnableApi)
			{
				Api = new Api.WebApi(core);
				startWebServer = true;
			}
			if (webData.EnableWebinterface)
			{
				Display = new Interface.WebDisplay(core);
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
					var uriBuilder = new UriBuilder(addrs[i]) { Port = webData.Port };
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

		private static AuthenticationSchemes AuthenticationSchemeSelector(HttpListenerRequest httpRequest)
		{
			var headerVal = httpRequest.Headers["Authorization"];
			if (string.IsNullOrEmpty(headerVal))
				return AuthenticationSchemes.Anonymous;

			var authParts = headerVal.SplitNoEmpty(' ');
			if (authParts.Length < 2)
				return AuthenticationSchemes.Anonymous;

			var authType = authParts[0].ToUpper();
			if (authType == "BASIC")
				return AuthenticationSchemes.Basic;

			return AuthenticationSchemes.Anonymous;
		}

		public void StartServerAsync()
		{
			if (!startWebServer)
				return;

			serverThread = new Thread(EnterWebLoop) { Name = "WebInterface" };
			serverThread.Start();
		}

		public void EnterWebLoop()
		{
			if (!startWebServer)
				return;

			ReloadHostPaths();

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
					var context = webListener.GetContext();
					IPAddress remoteAddress;
					try
					{
						remoteAddress = context.Request.RemoteEndPoint?.Address;
						if (remoteAddress == null)
							return;
					}
					catch (NullReferenceException) { return; }

					Log.Write(Log.Level.Info, "{0} Requested: {1}", remoteAddress, context.Request.Url.PathAndQuery);
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
			Display?.Dispose();
			Display = null;

			webListener?.Stop();
			webListener = null;
		}
	}

	public class WebData : ConfigData
	{
		[Info("A space seperated list of all urls the web api should be possible to be accessed with", "")]
		public string HostAddress { get; set; }

		[Info("The port for the api server", "8180")]
		public ushort Port { get; set; }

		[Info("If you want to start the web api server.", "false")]
		public bool EnableApi { get; set; }

		[Info("If you want to start the webinterface server", "false")]
		public bool EnableWebinterface { get; set; }
	}
}
