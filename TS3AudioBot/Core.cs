// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.


using NLog.Targets;

namespace TS3AudioBot
{
	using CommandSystem;
	using Dependency;
	using Helper;
	using History;
	using NLog;
	using Plugins;
	using ResourceFactories;
	using Rights;
	using System;
	using System.Threading;
	using Web;

	public sealed class Core : IDisposable, ICoreModule
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();
		private string configFilePath;

		internal static void Main(string[] args)
		{
			Thread.CurrentThread.Name = "TAB Main";

			if (LogManager.Configuration.AllTargets.Count == 0)
			{
				Console.WriteLine("No or empty NLog config found. Please refer to https://github.com/NLog/NLog/wiki/Configuration-file" +
				                  "to learn more how to set up the logging configuration.");
			}

			var core = new Core();

			AppDomain.CurrentDomain.UnhandledException += (s, e) =>
			{
				Log.Fatal(e.ExceptionObject as Exception, "Critical program failure!.");
				core.Dispose();
			};

			bool forceNextExit = false;
			Console.CancelKeyPress += (s, e) =>
			{
				if (e.SpecialKey == ConsoleSpecialKey.ControlC)
				{
					if (!forceNextExit)
					{
						e.Cancel = true;
						core.Dispose();
						forceNextExit = true;
					}
					else
					{
						Environment.Exit(0);
					}
				}
			};

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
		internal Injector Injector { get; set; }
		/// <summary>Mangement for the bot command system.</summary>
		public CommandManager CommandManager { get; set; }
		/// <summary>Manages factories which can load resources.</summary>
		public ResourceFactoryManager FactoryManager { get; set; }
		/// <summary>Minimalistic webserver hosting the api and web-interface.</summary>
		public WebManager WebManager { get; set; }
		/// <summary>Minimalistic config store for automatically serialized classes.</summary>
		public ConfigFile ConfigManager { get; set; }
		/// <summary>Permission system of the bot.</summary>
		public RightsManager RightsManager { get; set; }
		/// <summary>Permission system of the bot.</summary>
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
					Console.WriteLine(Util.GetAssemblyData().ToLongString());
					return false;

				default:
					Console.WriteLine("Unrecognized parameter: {0}", args[i]);
					return false;
				}
			}
			return true;
		}

		public void Initialize() { }

		private R InitializeCore()
		{
			ConfigManager = ConfigFile.OpenOrCreate(configFilePath) ?? ConfigFile.CreateDummy();
			var webd = ConfigManager.GetDataStruct<WebData>("WebData", true);
			var rmd = ConfigManager.GetDataStruct<RightsManagerData>("RightsManager", true);
			var mbd = ConfigManager.GetDataStruct<MainBotData>("MainBot", true);

			// TODO: DUMMY REQUESTS
			YoutubeDlHelper.DataObj = ConfigManager.GetDataStruct<YoutubeFactoryData>("YoutubeFactory", true);
			ConfigManager.GetDataStruct<PluginManagerData>("PluginManager", true);
			ConfigManager.GetDataStruct<MediaFactoryData>("MediaFactory", true);
			ConfigManager.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			ConfigManager.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			ConfigManager.GetDataStruct<Ts3FullClientData>("QueryConnection", true);
			ConfigManager.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			// END TODO
			ConfigManager.Close();

			Log.Info("[============ TS3AudioBot started =============]");
			Log.Info("[=== Date/Time: {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString());
			Log.Info("[=== Version: {0}", Util.GetAssemblyData().ToString());
			Log.Info("[=== Plattform: {0}", Util.GetPlattformData());
			Log.Info("[==============================================]");

			Log.Info("[============ Initializing Modules ============]");
			Log.Info("Using opus version: {0}", TS3Client.Full.Audio.Opus.NativeMethods.Info);
			TS3Client.Messages.Deserializer.OnError += (s, e) => Log.Error(e.ToString());

			Injector = new Injector();
			Injector.RegisterModule(this);
			Injector.RegisterModule(ConfigManager);
			Injector.RegisterModule(Injector);
			Database = Injector.Create<DbStore>();
			PluginManager = Injector.Create<PluginManager>();
			CommandManager = Injector.Create<CommandManager>();
			FactoryManager = Injector.Create<ResourceFactoryManager>();
			WebManager = Injector.Create<WebManager>();
			RightsManager = Injector.Create<RightsManager>();
			Bots = Injector.Create<BotManager>();

			Injector.SkipInitialized(this);

			if (!Injector.AllResolved())
			{
				// TODO detailed log + for inner if
				Log.Warn("Cyclic module dependency");
				Injector.ForceCyclicResolve();
				if (!Injector.AllResolved())
				{
					Log.Error("Missing module dependency");
					return "Could not load all modules";
				}
			}

			Log.Info("[==================== Done ====================]");
			return R.OkR;
		}

		private void Run()
		{
			Bots.WatchBots();
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
