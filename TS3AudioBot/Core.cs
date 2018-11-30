// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	using CommandSystem;
	using Config;
	using Dependency;
	using Helper;
	using NLog;
	using Plugins;
	using ResourceFactories;
	using Rights;
	using Sessions;
	using System;
	using System.Threading;
	using Web;

	public sealed class Core : IDisposable
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();
		private const string DefaultConfigFileName = "ts3audiobot.toml";
		private readonly string configFilePath;
		private bool forceNextExit;

		public DateTime StartTime { get; }
		public Helper.Environment.SystemMonitor SystemMonitor { get; }

		/// <summary>General purpose persistant storage for internal modules.</summary>
		internal DbStore Database { get; set; }
		/// <summary>Manages plugins, provides various loading and unloading mechanisms.</summary>
		internal PluginManager PluginManager { get; set; }
		/// <summary>Manages factories which can load resources.</summary>
		public ResourceFactoryManager FactoryManager { get; set; }
		/// <summary>Minimalistic webserver hosting the api and web-interface.</summary>
		public WebServer WebManager { get; set; }
		/// <summary>Management of conntected Bots.</summary>
		public BotManager Bots { get; set; }

		internal static void Main(string[] args)
		{
			Thread.CurrentThread.Name = "TAB Main";

			var setup = Setup.ReadParameter(args);

			if (setup.Exit == ExitType.Immediately)
				return;

			if (!setup.SkipVerifications && !Setup.VerifyAll())
				return;

			if (!setup.HideBanner)
				Setup.LogHeader();

			// Initialize the actual core
			var core = new Core(setup.ConfigFile);
			AppDomain.CurrentDomain.UnhandledException += core.ExceptionHandler;
			Console.CancelKeyPress += core.ConsoleInterruptHandler;

			var initResult = core.Run(!setup.NonInteractive);
			if (!initResult)
			{
				Log.Error("Core initialization failed: {0}", initResult.Error);
				core.Dispose();
			}
		}

		public Core(string configFilePath = null)
		{
			// setting defaults
			this.configFilePath = configFilePath ?? DefaultConfigFileName;

			StartTime = Util.GetNow();
			SystemMonitor = new Helper.Environment.SystemMonitor();
			SystemMonitor.StartTimedSnapshots();
		}

		private E<string> Run(bool interactive = false)
		{
			var configResult = ConfRoot.OpenOrCreate(configFilePath);
			if (!configResult.Ok)
				return "Could not create config";
			ConfRoot config = configResult.Value;
			Config.Deprecated.UpgradeScript.CheckAndUpgrade(config);

			var injector = new CoreInjector();

			injector.RegisterType<Core>();
			injector.RegisterType<ConfRoot>();
			injector.RegisterType<CoreInjector>();
			injector.RegisterType<DbStore>();
			injector.RegisterType<PluginManager>();
			injector.RegisterType<CommandManager>();
			injector.RegisterType<ResourceFactoryManager>();
			injector.RegisterType<WebServer>();
			injector.RegisterType<RightsManager>();
			injector.RegisterType<BotManager>();
			injector.RegisterType<TokenManager>();

			injector.RegisterModule(this);
			injector.RegisterModule(config);
			injector.RegisterModule(injector);
			injector.RegisterModule(new DbStore(config.Db));
			injector.RegisterModule(new PluginManager(config.Plugins));
			injector.RegisterModule(new CommandManager(), x => x.Initialize());
			injector.RegisterModule(new ResourceFactoryManager(config.Factories), x => x.Initialize());
			injector.RegisterModule(new WebServer(config.Web), x => x.Initialize());
			injector.RegisterModule(new RightsManager(config.Rights));
			injector.RegisterModule(new BotManager());
			injector.RegisterModule(new TokenManager(), x => x.Initialize());

			if (!injector.AllResolved())
			{
				Log.Debug("Cyclic core module dependency");
				injector.ForceCyclicResolve();
				if (!injector.AllResolved())
				{
					Log.Error("Missing core module dependency");
					return "Could not load all core modules";
				}
			}

			YoutubeDlHelper.DataObj = config.Tools.YoutubeDl;

			Bots.RunBots(interactive);

			return R.Ok;
		}

		public void ExceptionHandler(object sender, UnhandledExceptionEventArgs e)
		{
			Log.Fatal(e.ExceptionObject as Exception, "Critical program failure!");
			Dispose();
			Environment.Exit(-1);
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
					Dispose();
				}
				else
				{
					Log.Info("Got multiple interrupt signals, trying to force-exit.");
					Environment.Exit(0);
				}
			}
		}

		public void Dispose()
		{
			Log.Info("TS3AudioBot shutting down.");

			Bots?.Dispose();
			Bots = null;

			PluginManager?.Dispose(); // before: SessionManager,
			PluginManager = null;

			WebManager?.Dispose(); // before:
			WebManager = null;

			Database?.Dispose(); // before:
			Database = null;

			FactoryManager?.Dispose(); // before:
			FactoryManager = null;

			TickPool.Close();
		}
	}
}
