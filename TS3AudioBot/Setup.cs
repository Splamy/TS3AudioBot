namespace TS3AudioBot
{
	using Helper.Environment;
	using NLog;
	using System;

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
					Console.WriteLine("Do you want to continue? [Y/N]");
					while (true)
					{
						var key = Console.ReadKey().Key;
						if (key == ConsoleKey.N)
							return false;
						if (key == ConsoleKey.Y)
							break;
					}
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
				else if (SystemData.RuntimeData.SemVer < new Version(5, 0, 0))
				{
					Log.Error("You are running a mono version below 5.0.0!");
					Log.Error("This version is not supported and will not work properly.");
					Log.Error("Install the latest mono version by following https://www.mono-project.com/download/");
					return false;
				}
			}
			return true;
		}

		public static bool VerifyLibopus()
		{
			bool loaded = TS3Client.Audio.Opus.NativeMethods.PreloadLibrary();
			if (!loaded)
				Log.Error("Couldn't find libopus. Make sure it is installed or placed in the correct folder.");
			return loaded;
		}

		public static ParameterData ReadParameter(string[] args)
		{
			var data = new ParameterData { Exit = ExitType.No, };

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
				// > Ask for Uid/Group id to insert into rigths.toml template
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
					data.NonInteractive = true;
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

		public static void LogHeader()
		{
			Log.Info("[============ TS3AudioBot started =============]");
			Log.Info("[=== Date/Time: {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString());
			Log.Info("[=== Version: {0}", SystemData.AssemblyData);
			Log.Info("[=== Platform: {0}", SystemData.PlatformData);
			Log.Info("[=== Runtime: {0}", SystemData.RuntimeData.FullName);
			Log.Info("[=== Opus: {0}", TS3Client.Audio.Opus.NativeMethods.Info);
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
		public bool NonInteractive { get; set; }
	}

	internal enum ExitType
	{
		No,
		Immediately,
		AfterSetup,
	}
}
