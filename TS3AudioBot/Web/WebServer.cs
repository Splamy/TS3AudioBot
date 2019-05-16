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
	using System;
	using System.Globalization;
	using System.Linq;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;
	using Unosquare.Labs.EmbedIO;

	public sealed class WebServer : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private CancellationTokenSource cancelToken;
		private IHttpListener webListener;
		private Thread serverThread;
		private bool startWebServer;
		private readonly ConfWeb config;
		private readonly CoreInjector coreInjector;

		public Api.WebApi Api { get; private set; }
		public Interface.WebDisplay Display { get; private set; }

		public WebServer(ConfWeb config, CoreInjector coreInjector)
		{
			this.config = config;
			this.coreInjector = coreInjector;
			Unosquare.Swan.Terminal.Settings.DisplayLoggingMessageType = Unosquare.Swan.LogMessageType.None;
		}

		// TODO write server to be reloada-able
		public void Initialize()
		{
			InitializeSubcomponents();

			StartServerThread();
		}

		private void InitializeSubcomponents()
		{
			startWebServer = false;
			if (config.Interface.Enabled)
			{
				if (!coreInjector.TryCreate<Interface.WebDisplay>(out var display))
					throw new Exception();

				Display = display;
				coreInjector.AddModule(display);
				startWebServer = true;
			}
			if (config.Api.Enabled || config.Interface.Enabled)
			{
				if (!config.Api.Enabled)
					Log.Warn("The api is required for the webinterface to work properly; The api is now implicitly enabled. Enable the api in the config to get rid this error message.");

				if (!coreInjector.TryCreate<Api.WebApi>(out var api))
					throw new Exception();

				Api = api;
				coreInjector.AddModule(api);

				startWebServer = true;
			}

			if (startWebServer)
			{
				webListener = HttpListenerFactory.Create(HttpListenerMode.EmbedIO);
			}
		}

		private void ReloadHostPaths()
		{
			webListener.Prefixes.Clear();
			var addrs = config.Hosts.Value;

			if (addrs.Contains("*"))
			{
				webListener.AddPrefix($"http://*:{config.Port.Value}/");
				return;
			}

			foreach (var uri in addrs)
			{
				var uriBuilder = new UriBuilder(uri) { Port = config.Port };
				webListener.AddPrefix(uriBuilder.Uri.AbsoluteUri);
			}
		}

		public void StartServerThread()
		{
			if (!startWebServer)
				return;

			serverThread = new Thread(EnterWebLoop) { Name = "WebInterface" };
			serverThread.Start();
		}

		public async void EnterWebLoop()
		{
			if (!startWebServer)
				return;

			cancelToken?.Dispose();
			cancelToken = new CancellationTokenSource();

			ReloadHostPaths();

			try { webListener.Start(); }
			catch (Exception ex)
			{
				Log.Error(ex, "The webserver could not be started");
				webListener = null;
				return;
			}

			Log.Info("Started Webserver on port {0}", config.Port.Value);

			Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

			while (true)
			{
				IHttpContext context;
				try
				{
					if (cancelToken.IsCancellationRequested
						|| !(webListener?.IsListening ?? false))
						break;

					context = await webListener.GetContextAsync(cancelToken.Token);
					context.Response.KeepAlive = false;

					if (cancelToken.IsCancellationRequested)
						break;
				}
				catch (OperationCanceledException ex)
				{
					Log.Debug(ex, "WebServer exit requested");
					break;
				}
				catch (Exception ex)
				{
					Log.Error(ex, "WebServer exception");
					continue;
				}

				IHttpResponse response = context.Response;
				try
				{
					var remoteAddress = context.Request.RemoteEndPoint?.Address;
					if (remoteAddress is null)
						continue;
					if (context.Request.IsLocal
						&& !string.IsNullOrEmpty(context.Request.Headers["X-Real-IP"])
						&& IPAddress.TryParse(context.Request.Headers["X-Real-IP"], out var realIp))
					{
						remoteAddress = realIp;
					}

					var rawRequest = new Uri(WebComponent.Dummy, context.Request.RawUrl);
					Log.Info("{0} Requested: {1}", remoteAddress, rawRequest.PathAndQuery);

					bool handled = false;
					if (rawRequest.AbsolutePath.StartsWith("/api/", true, CultureInfo.InvariantCulture))
					{
						context.Items.Add("ip", remoteAddress);
						context.Items.Add("req", rawRequest);
						handled |= Api?.DispatchCall(context) ?? false;
					}
					else
					{
						handled |= Display?.DispatchCall(context) ?? false;
					}

					if (!handled)
					{
						using (var outputStream = response.OutputStream)
						{
							response.ContentLength64 = WebUtil.Default404Data.Length;
							response.StatusCode = (int)HttpStatusCode.NotFound;
							outputStream.Write(WebUtil.Default404Data, 0, WebUtil.Default404Data.Length);
						}
					}
				}
				// These seem to happen on connections which are closed too fast or failed to open correctly.
				catch (Exception ex) when (ex is SocketException || ex is System.IO.IOException) { Log.Debug(ex, "WebServer exception"); }
				// Catch everything to keep the webserver running, but print a warning.
				catch (Exception ex) { Log.Warn(ex, "WebServer exception"); }
				finally
				{
					response.Close();
				}
			}

			Log.Info("WebServer has closed");
		}

		public void Dispose()
		{
			Display?.Dispose();
			Display = null;

			cancelToken?.Cancel();
			cancelToken?.Dispose();
			cancelToken = null;

			webListener?.Stop();
			webListener?.Dispose();
			webListener = null;
		}
	}
}
