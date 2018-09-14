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
	using Config;
	using Dependency;
	using Helper;
	using Helper.Environment;
	using System;
	using System.Globalization;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;

	public sealed class WebServer : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public const string WebRealm = "ts3ab";

		private Uri localhost;
		private Uri[] hostPaths;

		private HttpListener webListener;
		private Thread serverThread;
		private bool startWebServer;
		private readonly ConfWeb config;

		public CoreInjector Injector { get; set; }

		public Api.WebApi Api { get; private set; }
		public Interface.WebDisplay Display { get; private set; }

		public WebServer(ConfWeb config)
		{
			this.config = config;
		}

		public void Initialize()
		{
			Injector.RegisterType<Api.WebApi>();
			Injector.RegisterType<Interface.WebDisplay>();

			InitializeSubcomponents();

			StartServerThread();
		}

		private void InitializeSubcomponents()
		{
			startWebServer = false;
			if (config.Api.Enabled)
			{
				Api = new Api.WebApi();
				Injector.RegisterModule(Api);
				startWebServer = true;
			}
			if (config.Interface.Enabled)
			{
				Display = new Interface.WebDisplay(config.Interface);
				Injector.RegisterModule(Display);
				startWebServer = true;
			}

			if (startWebServer)
			{
				webListener = new HttpListener
				{
					AuthenticationSchemes = AuthenticationSchemes.Anonymous | AuthenticationSchemes.Basic,
					Realm = WebRealm,
					AuthenticationSchemeSelectorDelegate = AuthenticationSchemeSelector,
				};
			}
		}

		private void ReloadHostPaths()
		{
			localhost = new Uri($"http://localhost:{config.Port.Value}/");

			if (Util.IsAdmin || SystemData.IsLinux) // todo: hostlist
			{
				var addrs = config.Hosts.Value;
				hostPaths = new Uri[addrs.Count + 1];
				hostPaths[0] = localhost;

				for (int i = 0; i < addrs.Count; i++)
				{
					var uriBuilder = new UriBuilder(addrs[i]) { Port = config.Port };
					hostPaths[i + 1] = uriBuilder.Uri;
				}
			}
			else
			{
				Log.Warn("App launched without elevated rights. Only localhost will be availbale as api server.");
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

		public void StartServerThread()
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
				Log.Error(ex, "The webserver could not be started");
				return;
			} // TODO

			Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

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
							continue;
						if(context.Request.IsLocal
							&& !string.IsNullOrEmpty(context.Request.Headers["X-Real-IP"])
							&& IPAddress.TryParse(context.Request.Headers["X-Real-IP"], out var realIp))
						{
							remoteAddress = realIp;
						}
					}
					// NRE catch handler is needed due to a strange mono race condition bug.
					catch (NullReferenceException) { continue; }

					var rawRequest = new Uri(WebComponent.Dummy, context.Request.RawUrl);
					Log.Info("{0} Requested: {1}", remoteAddress, rawRequest.PathAndQuery);
					if (rawRequest.AbsolutePath.StartsWith("/api/", true, CultureInfo.InvariantCulture))
						Api?.DispatchCall(context);
					else
						Display?.DispatchCall(context);
				}
				catch (SocketException ex) { Log.Debug(ex, "WebListener error"); }
				catch (HttpListenerException ex) { Log.Debug(ex, "WebListener error"); }
				catch (InvalidOperationException) { break; }
			}

			Log.Info("WebServer has closed");
		}

		public void Dispose()
		{
			Display?.Dispose();
			Display = null;

			webListener?.Stop();
			webListener?.Close();
			webListener = null;
		}
	}
}
