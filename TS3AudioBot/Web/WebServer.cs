// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;

namespace TS3AudioBot.Web
{
	public sealed class WebServer : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private CancellationTokenSource cancelToken;
		private readonly ConfWeb config;
		private readonly CoreInjector coreInjector;
		private Api.WebApi api;

		public WebServer(ConfWeb config, CoreInjector coreInjector)
		{
			this.config = config;
			this.coreInjector = coreInjector;
		}

		// TODO write server to be reload-able
		public void StartWebServer()
		{
			var startWebServer = false;

			if (config.Interface.Enabled)
			{
				var baseDir = FindWebFolder();
				if (baseDir is null)
				{
					Log.Error("Can't find a WebInterface path to host. Try specifying the path to host in the config");
				}
				else
				{
					startWebServer = true;
				}
			}
			if (config.Api.Enabled || config.Interface.Enabled)
			{
				if (!config.Api.Enabled)
					Log.Warn("The api is required for the webinterface to work properly; The api is now implicitly enabled. Enable the api in the config to remove this warning.");

				if (!coreInjector.TryCreate<Api.WebApi>(out var api))
					throw new Exception("Could not create Api object.");

				this.api = api;
				startWebServer = true;
			}

			if (startWebServer)
			{
				StartWebServerInternal();
			}
		}

		private string FindWebFolder()
		{
			var webData = config.Interface;
			if (string.IsNullOrEmpty(webData.Path))
			{
				for (int i = 0; i < 5; i++)
				{
					var up = Path.Combine(Enumerable.Repeat("..", i).ToArray());
					var checkDir = Path.Combine(up, "WebInterface");
					if (Directory.Exists(checkDir))
					{
						return checkDir;
					}
				}
			}
			else if (Directory.Exists(webData.Path))
			{
				return webData.Path;
			}

			return null;
		}

		private void StartWebServerInternal()
		{
			cancelToken?.Cancel();
			cancelToken?.Dispose();
			cancelToken = new CancellationTokenSource();

			var host = new WebHostBuilder()
				.SuppressStatusMessages(true)
				.ConfigureLogging((context, logging) =>
				{
					logging.ClearProviders();
				})
				.UseKestrel(kestrel =>
				{
					kestrel.Limits.MaxRequestBodySize = 3_000_000; // 3 MiB should be enough
				})
				.ConfigureServices(services =>
				{
					services.AddCors(options =>
					{
						options.AddPolicy("TS3AB", builder =>
						{
							builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
						});
					});

				})
				.Configure(app =>
				{
					app.UseCors("TS3AB");

					app.Map(new PathString("/api"), map =>
					{
						map.Run(ctx => Task.Run(() => Log.Swallow(() => api.ProcessApiV1Call(ctx))));
					});

					if (config.Interface.Enabled)
					{
						app.UseFileServer();
					}

					var applicationLifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
					applicationLifetime.ApplicationStopping.Register(OnShutdown);
				});

			if (config.Interface.Enabled)
			{
				var baseDir = FindWebFolder();
				if (baseDir is null)
				{
					Log.Error("Can't find a WebInterface path to host. Try specifying the path to host in the config");
				}
				else
				{
					host.UseWebRoot(baseDir);
				}
			}

			var addrs = config.Hosts.Value;
			if (addrs.Contains("*"))
			{
				host.ConfigureKestrel(kestrel => { kestrel.ListenAnyIP(config.Port.Value); });
			}
			else if (addrs.Count == 1 && addrs[0] == "localhost")
			{
				host.ConfigureKestrel(kestrel => { kestrel.ListenLocalhost(config.Port.Value); });
			}
			else
			{
				host.UseUrls(addrs.Select(uri => new UriBuilder(uri) { Port = config.Port }.Uri.AbsoluteUri).ToArray());
			}

			new Func<Task>(async () =>
			{
				try
				{
					await host.Build().RunAsync(cancelToken.Token);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "The webserver could not be started");
					return;
				}
			})();
			Log.Info("Started Webserver on port {0}", config.Port.Value);
		}

		public void OnShutdown()
		{
			Log.Info("WebServer has closed");
		}

		public void Dispose()
		{
			Log.Info("WebServer is closing");

			cancelToken?.Cancel();
			cancelToken?.Dispose();
			cancelToken = null;
		}
	}
}
