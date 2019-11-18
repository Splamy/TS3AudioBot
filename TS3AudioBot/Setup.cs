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
using System.Runtime;
using TS3AudioBot.Helper;
using TS3AudioBot.Helper.Environment;

namespace TS3AudioBot
{
	internal static class Setup
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		public static bool VerifyAll()
		{
			return VerifyLogSetup()
				&& VerifyMono()
				&& VerifyLibopus();
		}

		public static bool VerifyLogSetup()
		{
			if (LogManager.Configuration is null || LogManager.Configuration.AllTargets.Count == 0)
			{
				Console.WriteLine("No or empty NLog config found.\n" +
								  "You can copy the default config from TS3AudioBot/NLog.config.\n" +
								  "Please refer to https://github.com/NLog/NLog/wiki/Configuration-file " +
								  "to learn more how to set up your own logging configuration.");

				if (LogManager.Configuration is null)
				{
					Console.WriteLine("Create a default config to prevent this step.");
					Console.WriteLine("Do you want to continue? [y/N]");
					if (!Interactive.UserAgree(defaultTo: false))
						return false;
				}
			}
			return true;
		}

		public static bool VerifyMono()
		{
			if (SystemData.RuntimeData.Runtime == Runtime.Mono)
			{
				if (SystemData.RuntimeData.SemVer is null)
				{
					Log.Warn("Could not find your running mono version!");
					Log.Warn("This version might not work properly.");
					Log.Warn("If you encounter any problems, try installing the latest mono version by following https://www.mono-project.com/download/");
				}
				else if (SystemData.RuntimeData.SemVer < new Version(5, 18, 0))
				{
					Log.Error("You are running a mono version below 5.18.0!");
					Log.Error("This version is not supported and will not work properly.");
					Log.Error("Install the latest mono version by following https://www.mono-project.com/download/");
					return false;
				}
			}
			return true;
		}

		public static bool VerifyLibopus()
		{
			bool loaded = TSLib.Audio.Opus.NativeMethods.PreloadLibrary();
			if (!loaded)
				Log.Error("Couldn't find libopus. Make sure it is installed or placed in the correct folder.");
			return loaded;
		}

		public static ParameterData ReadParameter(string[] args)
		{
			var data = new ParameterData
			{
				Interactive = true,
				Llgc = true,
				Exit = ExitType.No,
			};

			ParameterData Cancel() { data.Exit = ExitType.Immediately; return data; }

			for (int i = 0; i < args.Length; i++)
			{
				// -i --interactive, minimal ui/console tool to execute basic stuff like
				// create bot, excute commands

				// --setup setup the entire environment (-y to skip for user input?)
				// > mono (apt-get/upgrade to latest version, + package upgade)
				// > libopus (self-compile/apt-get)
				// > ffmpeg (apt-get)
				// > youtube-dl (repo/apt-get)
				// > check NLog.config exists
				// > Crete new bot (see --new-bot)

				// --new-bot name={} address={} server_password={} ?

				switch (args[i])
				{
				case "?":
				case "-h":
				case "--help":
					Console.WriteLine(" --config -c <file>  Specifies the path to the config file.");
					Console.WriteLine(" --version -V        Gets the bot version.");
					Console.WriteLine(" --skip-checks       Skips checking the system for all required tools.");
					Console.WriteLine(" --hide-banner       Does not print the version information header.");
					Console.WriteLine(" --non-interactive   Disables console prompts from setup tools.");
					Console.WriteLine(" --help -h           Prints this help...");
					return Cancel();

				case "-c":
				case "--config":
					if (i + 1 >= args.Length)
					{
						Console.WriteLine("No config file specified after \"{0}\"", args[i]);
						return Cancel();
					}
					data.ConfigFile = args[++i];
					break;

				case "--skip-checks":
					data.SkipVerifications = true;
					break;

				case "--hide-banner":
					data.HideBanner = true;
					break;

				case "--non-interactive":
					data.Interactive = false;
					break;

				case "--no-llgc":
					data.Llgc = false;
					break;

				case "-V":
				case "--version":
					Console.WriteLine(SystemData.AssemblyData.ToLongString());
					return Cancel();

				default:
					Console.WriteLine("Unrecognized parameter: {0}", args[i]);
					return Cancel();
				}
			}
			return data;
		}

		public static void EnableLlgc()
		{
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
		}

		public static void LogHeader()
		{
			Log.Info("[============ TS3AudioBot started =============]");
			Log.Info("[ Date/Time: {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString());
			Log.Info("[ Version: {0}", SystemData.AssemblyData);
			Log.Info("[ Platform: {0}", SystemData.PlatformData);
			Log.Info("[ Runtime: {0} ServerGC:{1} GC:{2}", SystemData.RuntimeData.FullName, GCSettings.IsServerGC, GCSettings.LatencyMode);
			Log.Info("[ Opus: {0}", TSLib.Audio.Opus.NativeMethods.Info);
			// ffmpeg
			// youtube-dl
			Log.Info("[==============================================]");
		}
	}

	internal class ParameterData
	{
		public ExitType Exit { get; set; }
		public string ConfigFile { get; set; }
		public bool SkipVerifications { get; set; }
		public bool HideBanner { get; set; }
		public bool Interactive { get; set; }
		public bool Llgc { get; set; }
	}

	internal enum ExitType
	{
		No,
		Immediately,
		AfterSetup,
	}
}
