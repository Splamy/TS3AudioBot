// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using NLog;
using System;
using System.Threading.Tasks;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Environment;
using TS3AudioBot.Helper;
using TS3AudioBot.Plugins;
using TS3AudioBot.ResourceFactories;
using TS3AudioBot.Rights;
using TS3AudioBot.Sessions;
using TS3AudioBot.Web;

namespace TS3AudioBot
{
	public sealed class Core
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();
		private readonly string configFilePath;
		private bool forceNextExit;
		private readonly CoreInjector injector;

		public Core(string? configFilePath = null)
		{
			// setting defaults
			this.configFilePath = configFilePath ?? FilesConst.CoreConfig;

			injector = new CoreInjector();
		}

		public async Task<E<string>> Run(ParameterData setup)
		{
			AppDomain.CurrentDomain.UnhandledException += ExceptionHandler;
			TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
			Console.CancelKeyPress += ConsoleInterruptHandler;

			var config = ConfRoot.OpenOrCreate(configFilePath);
			if (config is null)
				return "Could not create config";
			ConfigUpgrade2.Upgrade(config.Configs.BotsPath.Value);
			config.Save();

			var builder = new DependencyBuilder(injector);

			builder.AddModule(this);
			builder.AddModule(config);
			builder.AddModule(injector);
			builder.AddModule(config.Db);
			builder.RequestModule<SystemMonitor>();
			builder.RequestModule<DbStore>();
			builder.AddModule(config.Plugins);
			builder.RequestModule<PluginManager>();
			builder.AddModule(config.Web);
			builder.AddModule(config.Web.Interface);
			builder.AddModule(config.Web.Api);
			builder.RequestModule<WebServer>();
			builder.AddModule(config.Rights);
			builder.RequestModule<RightsManager>();
			builder.RequestModule<BotManager>();
			builder.RequestModule<TokenManager>();
			builder.RequestModule<CommandManager>();
			builder.AddModule(config.Factories);
			builder.RequestModule<ResourceResolver>();
			builder.RequestModule<Stats>();

			if (!builder.Build())
				return "Could not load all core modules";

			YoutubeDlHelper.DataObj = config.Tools.YoutubeDl;

			builder.GetModuleOrThrow<SystemMonitor>().StartTimedSnapshots();
			builder.GetModuleOrThrow<CommandManager>().RegisterCollection(MainCommands.Bag);
			builder.GetModuleOrThrow<RightsManager>().CreateConfigIfNotExists(setup.Interactive);
			builder.GetModuleOrThrow<WebServer>().StartWebServer();
			builder.GetModuleOrThrow<Stats>().StartTimer(setup.SendStats);
			await builder.GetModuleOrThrow<BotManager>().RunBots(setup.Interactive);

			return R.Ok;
		}

		public void ExceptionHandler(object sender, UnhandledExceptionEventArgs e)
		{
			Log.Fatal(e.ExceptionObject as Exception, "Critical program failure!");
			Stop().Wait();
			System.Environment.Exit(-1);
		}

		public static void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
		{
			Log.Error(e.Exception, "Unobserved Task error!");
		}

		public void ConsoleInterruptHandler(object sender, ConsoleCancelEventArgs e)
		{
			if (e.SpecialKey == ConsoleSpecialKey.ControlC)
			{
				if (!forceNextExit)
				{
					Log.Info("Got interrupt signal, trying to soft-exit.");
					e.Cancel = true;
					forceNextExit = true;
					Stop().Wait();
				}
				else
				{
					Log.Info("Got multiple interrupt signals, trying to force-exit.");
					System.Environment.Exit(0);
				}
			}
		}

		public async Task Stop()
		{
			Log.Info("TS3AudioBot shutting down.");

			var botManager = injector.GetModule<BotManager>();
			if (botManager != null)
				await botManager.StopBots();
			injector.GetModule<PluginManager>()?.Dispose();
			injector.GetModule<WebServer>()?.Dispose();
			injector.GetModule<DbStore>()?.Dispose();
			injector.GetModule<ResourceResolver>()?.Dispose();
		}
	}
}
