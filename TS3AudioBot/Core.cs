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
	using Dependency;
	using Helper;
	using Helper.Environment;
	using History;
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
		private string configFilePath;
		private bool forceNextExit;

		internal static void Main(string[] args)
		{
			Thread.CurrentThread.Name = "TAB Main";

			if (LogManager.Configuration == null || LogManager.Configuration.AllTargets.Count == 0)
			{
				Console.WriteLine("No or empty NLog config found.\n" +
								  "You can copy the default config from TS3AudioBot/NLog.config.\n" +
								  "Please refer to https://github.com/NLog/NLog/wiki/Configuration-file " +
								  "to learn more how to set up your own logging configuration.");

				if (LogManager.Configuration == null)
				{
					Console.WriteLine("Create a default config to prevent this step.");
					Console.WriteLine("Do you want to continue? [Y/N]");
					while (true)
					{
						var key = Console.ReadKey().Key;
						if (key == ConsoleKey.N)
							return;
						if (key == ConsoleKey.Y)
							break;
					}
				}
			}

			var core = new Core();
			AppDomain.CurrentDomain.UnhandledException += core.ExceptionHandler;
			Console.CancelKeyPress += core.ConsoleInterruptHandler;

			if (!core.ReadParameter(args))
				return;

			var initResult = core.InitializeCore();
			if (!initResult)
			{
				Log.Error("Core initialization failed: {0}", initResult.Error);
				core.Dispose();
				return;
			}

			core.Run();
		}

		/// <summary>General purpose persistant storage for internal modules.</summary>
		internal DbStore Database { get; set; }
		/// <summary>Manages plugins, provides various loading and unloading mechanisms.</summary>
		internal PluginManager PluginManager { get; set; }
		/// <summary>Manages a dependency hierachy and injects required modules at runtime.</summary>
		internal CoreInjector Injector { get; set; }
		/// <summary>Manages factories which can load resources.</summary>
		public ResourceFactoryManager FactoryManager { get; set; }
		/// <summary>Minimalistic webserver hosting the api and web-interface.</summary>
		public WebManager WebManager { get; set; }
		/// <summary>Minimalistic config store for automatically serialized classes.</summary>
		public ConfigFile ConfigManager { get; set; }
		/// <summary>Management of conntected Bots.</summary>
		public BotManager Bots { get; set; }

		public Core()
		{
			// setting defaults
			configFilePath = "configTS3AudioBot.cfg";
		}

		private bool ReadParameter(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i])
				{
				case "-h":
				case "--help":
					Console.WriteLine(" --quiet -q          Deactivates all output to stdout.");
					Console.WriteLine(" --config -c <file>  Specifies the path to the config file.");
					Console.WriteLine(" --version -V        Gets the bot version.");
					Console.WriteLine(" --help -h           Prints this help....");
					return false;

				case "-c":
				case "--config":
					if (i >= args.Length - 1)
					{
						Console.WriteLine("No config file specified after \"{0}\"", args[i]);
						return false;
					}
					configFilePath = args[++i];
					break;

				case "-V":
				case "--version":
					Console.WriteLine(SystemData.AssemblyData.ToLongString());
					return false;

				default:
					Console.WriteLine("Unrecognized parameter: {0}", args[i]);
					return false;
				}
			}
			return true;
		}

		private R InitializeCore()
		{
			ConfigManager = ConfigFile.OpenOrCreate(configFilePath) ?? ConfigFile.CreateDummy();

			// TODO: DUMMY REQUESTS
			var webd = ConfigManager.GetDataStruct<WebData>("WebData", true);
			var rmd = ConfigManager.GetDataStruct<RightsManagerData>("RightsManager", true);
			ConfigManager.GetDataStruct<MainBotData>("MainBot", true);
			YoutubeDlHelper.DataObj = ConfigManager.GetDataStruct<YoutubeFactoryData>("YoutubeFactory", true);
			var pmd = ConfigManager.GetDataStruct<PluginManagerData>("PluginManager", true);
			ConfigManager.GetDataStruct<MediaFactoryData>("MediaFactory", true);
			var hmd = ConfigManager.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			ConfigManager.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			ConfigManager.GetDataStruct<Ts3FullClientData>("QueryConnection", true);
			ConfigManager.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			// END TODO
			ConfigManager.Close();

			Log.Info("[============ TS3AudioBot started =============]");
			Log.Info("[=== Date/Time: {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString());
			Log.Info("[=== Version: {0}", SystemData.AssemblyData);
			Log.Info("[=== Platform: {0}", SystemData.PlattformData);
			Log.Info("[=== Runtime: {0}", SystemData.RuntimeData.FullName);
			Log.Info("[=== Opus: {0}", TS3Client.Audio.Opus.NativeMethods.Info);
			Log.Info("[==============================================]");
			if (SystemData.RuntimeData.Runtime == Runtime.Mono)
			{
				if (SystemData.RuntimeData.SemVer == null)
				{
					Log.Warn("Could not find your running mono version!");
					Log.Warn("This version might not work properly.");
					Log.Warn("If you encounter any problems, try installing the latest mono version by following http://www.mono-project.com/download/");
				}
				else if (SystemData.RuntimeData.SemVer.Major < 5)
				{
					Log.Error("You are running a mono version below 5.0.0!");
					Log.Error("This version is not supported and will not work properly.");
					Log.Error("Install the latest mono version by following http://www.mono-project.com/download/");
				}
			}

			Log.Info("[============ Initializing Modules ============]");
			TS3Client.Messages.Deserializer.OnError += (s, e) => Log.Error(e.ToString());

			Injector = new CoreInjector();

			Injector.RegisterType<Core>();
			Injector.RegisterType<ConfigFile>();
			Injector.RegisterType<CoreInjector>();
			Injector.RegisterType<DbStore>();
			Injector.RegisterType<PluginManager>();
			Injector.RegisterType<CommandManager>();
			Injector.RegisterType<ResourceFactoryManager>();
			Injector.RegisterType<WebManager>();
			Injector.RegisterType<RightsManager>();
			Injector.RegisterType<BotManager>();
			Injector.RegisterType<TokenManager>();

			Injector.RegisterModule(this);
			Injector.RegisterModule(ConfigManager);
			Injector.RegisterModule(Injector);
			Injector.RegisterModule(new DbStore(hmd));
			Injector.RegisterModule(new PluginManager(pmd));
			Injector.RegisterModule(new CommandManager(), x => x.Initialize());
			Injector.RegisterModule(new ResourceFactoryManager(), x => x.Initialize());
			Injector.RegisterModule(new WebManager(webd), x => x.Initialize());
			Injector.RegisterModule(new RightsManager(rmd), x => x.Initialize());
			Injector.RegisterModule(new BotManager());
			Injector.RegisterModule(new TokenManager(), x => x.Initialize());

			if (!Injector.AllResolved())
			{
				Log.Debug("Cyclic core module dependency");
				Injector.ForceCyclicResolve();
				if (!Injector.AllResolved())
				{
					Log.Error("Missing core module dependency");
					return "Could not load all core modules";
				}
			}

			Log.Info("[==================== Done ====================]");
			return R.OkR;
		}

		private void Run()
		{
			Bots.WatchBots();
		}

		public void ExceptionHandler(object sender, UnhandledExceptionEventArgs e)
		{
			Log.Fatal(e.ExceptionObject as Exception, "Critical program failure!.");
			Dispose();
		}

		public void ConsoleInterruptHandler(object sender, ConsoleCancelEventArgs e)
		{
			if (e.SpecialKey == ConsoleSpecialKey.ControlC)
			{
				if (!forceNextExit)
				{
					e.Cancel = true;
					forceNextExit = true;
					Dispose();
				}
				else
				{
					Environment.Exit(0);
				}
			}
		}

		public void Dispose()
		{
			Log.Info("TS3AudioBot shutting down.");

			Bots?.Dispose();
			Bots = null;

			PluginManager?.Dispose(); // before: SessionManager, logStream,
			PluginManager = null;

			WebManager?.Dispose(); // before: logStream,
			WebManager = null;

			Database?.Dispose(); // before: logStream,
			Database = null;

			FactoryManager?.Dispose(); // before:
			FactoryManager = null;

			TickPool.Close();
		}
	}
}
