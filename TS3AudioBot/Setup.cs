// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using CommandLine;
using CommandLine.Text;
using NLog;
using System;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Environment;
using TS3AudioBot.Helper;
using TSLib.Helper;
using TSLib.Scheduler;

namespace TS3AudioBot
{
	internal static class Setup
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		public const int ExitCodeOk = 0;
		public const int ExitCodeMalformedArguments = 1;
		public const int ExitCodeLibopusLoadError = 2;

		public static int Main(string[] args)
		{
			Thread.CurrentThread.Name = "TAB Main";
			Tools.SetLogId("Core");

			var parsedArgs = new Parser(with =>
			{
				with.AutoHelp = true;
				with.AutoVersion = false;
			}).ParseArguments<ParameterData?>(args);
			ParameterData? setup = parsedArgs.MapResult(ok => ok, _ => null);

			if (setup is null)
			{
				Console.WriteLine(HelpText.AutoBuild(parsedArgs, h =>
				{
					h.Heading = $"TS3AudioBot {SystemData.AssemblyData}";
					h.Copyright = "";
					return HelpText.DefaultParsingErrorsHandler(parsedArgs, h);
				}));
				return ExitCodeMalformedArguments;
			}

			if (setup.ShowVersion)
			{
				Console.WriteLine(SystemData.AssemblyData.ToLongString());
				return ExitCodeOk;
			}

			if (setup.ShowStatsExample)
			{
				Console.WriteLine("The bot will contribute to the stats counter about once per day.");
				Console.WriteLine("We do NOT store any IP or identifiable information.");
				Console.WriteLine("Please keep this feature enabled to help us improve and grow.");
				Console.WriteLine("An example stats packet looks like this:");
				Console.WriteLine(Stats.CreateExample());
				return ExitCodeOk;
			}

			SetupLog();
			if (!SetupLibopus())
				return ExitCodeLibopusLoadError;

			if (setup.Llgc)
				EnableLlgc();

			if (!setup.HideBanner)
				LogHeader();

			DedicatedTaskScheduler.FromCurrentThread(() => StartBot(setup));
			return ExitCodeOk;
		}

		private static async void StartBot(ParameterData setup)
		{
			// Initialize the actual core
			var core = new Core((DedicatedTaskScheduler)TaskScheduler.Current, setup.ConfigFile);

			await core.Run(setup);
		}

		public static void SetupLog()
		{
			if (LogManager.Configuration is null)
			{
				var configFileInfo = new FileInfo("NLog.config");
				if (!configFileInfo.Exists)
				{
					using var configStream = Util.GetEmbeddedFile("TS3AudioBot.Resources.NLog.config")!;
					using var configFileStream = configFileInfo.OpenWrite();
					configStream.CopyTo(configFileStream);
				}
				LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(configFileInfo.FullName);
			}
			else if (LogManager.Configuration.AllTargets.Count == 0)
			{
				Console.WriteLine("Your NLog target config is empty. Nothing will be logged!");
				Console.WriteLine("Please refer to https://github.com/NLog/NLog/wiki/Configuration-file to learn more how to set up your own logging configuration.");
			}
		}

		public static bool SetupLibopus()
		{
			bool loaded = TSLib.Audio.Opus.NativeMethods.PreloadLibrary();
			if (!loaded)
				Log.Error("Couldn't find libopus. Make sure it is installed or placed in the correct folder.");
			return loaded;
		}

		public static void EnableLlgc()
		{
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
		}

		public static void LogHeader()
		{
			Log.Info("[============ TS3AudioBot started =============]");
			Log.Info("[ Date: {0}", DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss", CultureInfo.InvariantCulture));
			Log.Info("[ Version: {0}", SystemData.AssemblyData);
			Log.Info("[ Build: {0}", SystemData.AssemblyData.BuildConfiguration);
			Log.Info("[ Platform: {0}", SystemData.PlatformData);
			Log.Info("[ Runtime: {0} ServerGC:{1} GC:{2}", SystemData.RuntimeData.FullName, GCSettings.IsServerGC, GCSettings.LatencyMode);
			Log.Info("[ Opus: {0}", TSLib.Audio.Opus.NativeMethods.Info);
			// ffmpeg
			// youtube-dl
			Log.Info("[==============================================]");
		}
	}

	public class ParameterData
	{
		[Option('c', "config", Default = null, HelpText = "Specify the path to the ts3audiobot.toml config file.")]
		public string? ConfigFile { get; set; }
		[Option("skip-checks", HelpText = "Skips checking the system for all required tools.")]
		public bool SkipVerifications { get; set; }
		[Option("hide-banner", HelpText = "Do not print the version information header.")]
		public bool HideBanner { get; set; }
		[Option("non-interactive", HelpText = "Disables console prompts from setup tools.")]
		public bool NonInteractive { get; set; }
		public bool Interactive => !NonInteractive;
		[Option("no-llgc", Hidden = true)]
		public bool NoLlgc { get; set; }
		public bool Llgc => !NoLlgc;
		[Option("stats-disabled", HelpText = "Disables sending to the global stats tracker.")]
		public bool StatsDisabled { get; set; }
		public bool SendStats => !StatsDisabled;
		[Option("stats-example", HelpText = "Shows you what the bot sends to the global stats tracker.")]
		public bool ShowStatsExample { get; set; }
		[Option('V', "version", HelpText = "Gets the bot version.")]
		public bool ShowVersion { get; set; }

		// -i --interactive, minimal ui/console tool to execute basic stuff like
		// create bot, excute commands

		// --setup setup the entire environment (-y to skip for user input?)
		// > libopus (self-compile/apt-get)
		// > ffmpeg (apt-get)
		// > youtube-dl (repo/apt-get)
		// > check NLog.config exists
		// > Crete new bot (see --new-bot)

		// --new-bot name={} address={} server_password={} ?

	}
}
