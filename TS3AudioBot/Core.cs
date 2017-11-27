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
	using History;
	using Plugins;
	using ResourceFactories;
	using Rights;
	using System;
	using System.IO;
	using System.Threading;
	using Web;

	public sealed class Core : IDisposable, ICoreModule
	{
		private bool consoleOutput;
		private bool writeLog;
		private bool writeLogStack;
		internal string configFilePath;
		private StreamWriter logStream;
		private MainBotData mainBotData;

		internal static void Main(string[] args)
		{
			Thread.CurrentThread.Name = "TAB Main";

			var core = new Core();

			AppDomain.CurrentDomain.UnhandledException += (s, e) =>
			{
				Log.Write(Log.Level.Error, "Critical program failure!. Exception:\n{0}", (e.ExceptionObject as Exception).UnrollException());
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
				Log.Write(Log.Level.Error, "Core initialization failed: {0}", initResult.Error);
				core.Dispose();
				return;
			}

			core.Run();
		}

		/// <summary>General purpose persistant storage for internal modules.</summary>
		internal DbStore Database { get; set; }
		/// <summary>Manges plugins, provides various loading and unloading mechanisms.</summary>
		internal PluginManager PluginManager { get; set; }
		/// <summary>Manges plugins, provides various loading and unloading mechanisms.</summary>
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
			consoleOutput = true;
			writeLog = true;
			writeLogStack = false;
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
					Console.WriteLine(" --no-log -L         Deactivates writing to the logfile.");
					Console.WriteLine(" --stack -s          Adds the stacktrace to all log writes.");
					Console.WriteLine(" --config -c <file>  Specifies the path to the config file.");
					Console.WriteLine(" --version -V        Gets the bot version.");
					Console.WriteLine(" --help -h           Prints this help....");
					return false;

				case "-q":
				case "--quiet":
					consoleOutput = false;
					break;

				case "-L":
				case "--no-log":
					writeLog = false;
					break;

				case "-s":
				case "--stack":
					writeLogStack = true;
					break;

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

			// TODO: DUMMY REQUESTS
			YoutubeDlHelper.DataObj = ConfigManager.GetDataStruct<YoutubeFactoryData>("YoutubeFactory", true);
			ConfigManager.GetDataStruct<PluginManagerData>("PluginManager", true);
			ConfigManager.GetDataStruct<MediaFactoryData>("MediaFactory", true);
			ConfigManager.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			ConfigManager.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			ConfigManager.GetDataStruct<Ts3FullClientData>("QueryConnection", true);
			ConfigManager.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			mainBotData = ConfigManager.GetDataStruct<MainBotData>("MainBot", true);
			// END TODO

			mainBotData = ConfigManager.GetDataStruct<MainBotData>("MainBot", true);
			ConfigManager.Close();

			if (consoleOutput)
			{
				void ColorLog(string msg, Log.Level lvl)
				{
					switch (lvl)
					{
					case Log.Level.Debug: break;
					case Log.Level.Info: Console.ForegroundColor = ConsoleColor.Cyan; break;
					case Log.Level.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
					case Log.Level.Error: Console.ForegroundColor = ConsoleColor.Red; break;
					default: throw new ArgumentOutOfRangeException(nameof(lvl), lvl, null);
					}
					Console.WriteLine(msg);
					Console.ResetColor();
				}

				Log.RegisterLogger("[%T]%L: %M", 19, ColorLog);
				Log.RegisterLogger("Error call Stack:\n%S", 19, ColorLog, Log.Level.Error);
			}

			if (writeLog && !string.IsNullOrEmpty(mainBotData.LogFile))
			{
				logStream = new StreamWriter(File.Open(mainBotData.LogFile, FileMode.Append, FileAccess.Write, FileShare.Read), Util.Utf8Encoder);
				Log.RegisterLogger("[%T]%L: %M\n" + (writeLogStack ? "%S\n" : ""), 19, (msg, lvl) =>
				{
					if (logStream == null) return;
					try
					{
						logStream.Write(msg);
						logStream.Flush();
					}
					catch (IOException) { }
				});
			}

			Log.Write(Log.Level.Info, "[============ TS3AudioBot started =============]");
			Log.Write(Log.Level.Info, "[=== Date/Time: {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString());
			Log.Write(Log.Level.Info, "[=== Version: {0}", Util.GetAssemblyData().ToString());
			Log.Write(Log.Level.Info, "[=== Plattform: {0}", Util.GetPlattformData());
			Log.Write(Log.Level.Info, "[==============================================]");

			Log.Write(Log.Level.Info, "[============ Initializing Modules ============]");
			Audio.Opus.NativeMethods.DummyLoad();

			Injector = new Injector();
			Injector.RegisterModule(this); // OK
			Injector.RegisterModule(ConfigManager); // OK
			Injector.RegisterModule(Injector); // OK
			Database = Injector.Create<DbStore>(); // OK
			PluginManager = Injector.Create<PluginManager>(); // OK
			CommandManager = Injector.Create<CommandManager>(); // OK
			FactoryManager = Injector.Create<ResourceFactoryManager>(); // OK
			WebManager = Injector.Create<WebManager>(); // OK
			RightsManager = Injector.Create<RightsManager>(); // OK
			Bots = Injector.Create<BotManager>(); // OK

			Injector.SkipInitialized(this);

			if (!Injector.AllResolved())
			{
				// TODO detailed log + for inner if
				Log.Write(Log.Level.Warning, "Cyclic module dependency");
				Injector.ForceCyclicResolve();
				if (!Injector.AllResolved())
				{
					Log.Write(Log.Level.Error, "Missing module dependency");
					return "Could not load all modules";
				}
			}

			Log.Write(Log.Level.Info, "[==================== Done ====================]");
			return R.OkR;
		}

		private void Run()
		{
			Bots.WatchBots();
		}

		public void Dispose()
		{
			Log.Write(Log.Level.Info, "TS3AudioBot shutting down.");

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

			logStream?.Dispose();
			logStream = null;
		}
	}
}
