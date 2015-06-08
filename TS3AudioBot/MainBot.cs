using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Responses;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;

namespace TS3AudioBot
{
	public sealed class MainBot : IDisposable
	{
		static void Main(string[] args)
		{
			using (MainBot bot = new MainBot())
			{
				bot.Run(args);
			}
		}

		bool run = true;
		MainBotData mainBotData;
		AudioFramework audioFramework;
		BobController bobController;
		QueryConnection queryConnection;
		YoutubeFramework youtubeFramework;
		Func<TextMessage, Task<bool>> awaitingResponse = null;

		public MainBot()
		{
			// Read Config File
			string configFilePath = Util.GetFilePath(FilePath.ConfigFile);
			ConfigFile cfgFile = ConfigFile.Open(configFilePath) ?? ConfigFile.Create(configFilePath) ?? ConfigFile.GetDummy();
			QueryConnectionData qcd = cfgFile.GetDataStruct<QueryConnectionData>(typeof(QueryConnection), true);
			BobControllerData bcd = cfgFile.GetDataStruct<BobControllerData>(typeof(BobController), true);
			AudioFrameworkData afd = cfgFile.GetDataStruct<AudioFrameworkData>(typeof(AudioFramework), true);
			mainBotData = cfgFile.GetDataStruct<MainBotData>(typeof(MainBot), true);
			cfgFile.Close();

			// Initialize Modules
			audioFramework = new AudioFramework(afd);

			youtubeFramework = new YoutubeFramework();

			bobController = new BobController(bcd);
			audioFramework.OnRessourceStarted += (audioRessource) =>
			{
				bobController.Start();
				bobController.Sending = true;
			};
			audioFramework.OnRessourceStopped += () =>
			{
				bobController.StartEndTimer();
				bobController.Sending = false;
			};
			queryConnection = new QueryConnection(qcd);
			queryConnection.OnMessageReceived += TextCallback;
			bobController.QueryConnection = queryConnection;
			var connectTask = queryConnection.Connect();
		}

		public void Run(string[] args)
		{
			HashSet<string> launchParameter = new HashSet<string>();
			foreach (string parameter in args)
				launchParameter.Add(parameter);
			if (launchParameter.Contains("--help") || launchParameter.Contains("-h"))
			{
				Console.WriteLine(" --NoInput -I     Deactivates reading from stdin to enable background running.");
				Console.WriteLine(" --Silent -S      Deactivates all output to stdout.");
				Console.WriteLine(" --NoLog -L       Deactivates writing to the logfile.");
				Console.WriteLine(" --help -h        Prints this help....");
				return;
			}
			bool noInput = launchParameter.Contains("--NoInput") || launchParameter.Contains("-I");
			bool silent = launchParameter.Contains("--Silent") || launchParameter.Contains("-S");
			bool noLog = launchParameter.Contains("--NoLog") || launchParameter.Contains("-L");

			if (!silent)
			{
				Log.OnLog += (o, e) => { Console.WriteLine(e.InfoMessage); };
			}

			if (!noLog)
			{
				Log.OnLog += (o, e) => { File.AppendAllText(mainBotData.logFile, e.DetailedMessage, Encoding.UTF8); };
			}

			while (run)
			{
				if (noInput)
				{
					Task.Delay(1000).Wait();
				}
				else
				{
					ReadConsole();
				}
			}
		}

		private void ReadConsole()
		{
			string input;
			try
			{
				input = Console.ReadLine();
			}
			catch
			{
				Task.Delay(1000).Wait();
				return;
			}
			if (input == null)
			{
				Task.Delay(1000).Wait();
				return;
			}
			if (input == "quit")
			{
				run = false;
				return;
			}
		}

		public async void TextCallback(object sender, TextMessage tm)
		{
			Log.Write(Log.Level.Debug, "MB Got from {0} message: {1}", tm.InvokerName, tm.Message);

			if (awaitingResponse != null)
			{
				if (await awaitingResponse(tm))
				{
					awaitingResponse = null;
					return;
				}
			}

			if (!tm.Message.StartsWith("!"))
				return;
			string commandSubstring = tm.Message.Substring(1);
			string[] command = commandSubstring.Split(' ');
			string argumentUncut = commandSubstring.Substring(command[0].Length);
			bobController.HasUpdate();

			GetClientsInfo client = await queryConnection.GetClientById(tm.InvokerId);

			switch (command[0].ToLower())
			{
			case "add":
				if (command.Length == 2)
					PlayAuto(client, command[1], true);
				else
					WriteClient(client, "Missing or too many parameter. Usage !add <url/path>");
				break;

			case "clear":
				audioFramework.Clear();
				break;

			case "help":
				WriteClient(client, "\n" +
					"!pm: Get private audience with the AudioBot\n" +
					"!kickme: Does exactly what you think it does...\n" +
					"!play: Plays any file or media/youtube url [p]\n" +
					"!youtube: Plays a video from youtube [yt]\n" +
					"!link: Plays any media from the server [vlocal, vl]\n" +
					"!stop: Stops the current song\n" +
					"!startbot: Connects the MusicBot to TeamSpeak\n" +
					"!stopbot: Disconnects the MusicBot from TeamSpeak\n" +
					"!history: Shows you the last played songs\n");
				break;

			case "history":
				//TODO
				break;

			case "kickme":
				if (command.Length == 1)
					KickClient(client, null);
				else if (command.Length == 2)
					KickClient(client, command[1]);
				break;

			case "l":
			case "link":
				if (command.Length >= 2)
					PlayLink(client, argumentUncut, false);
				else
					WriteClient(client, "Missing or too many parameter. Usage !link <url/path>");
				break;

			case "loop":
				if (command.Length == 2)
				{
					if (command[1] == "on")
						audioFramework.Loop = true;
					else if (command[1] == "off")
						audioFramework.Loop = false;
				}
				else
					WriteClient(client, "Missing or too many parameter. Usage !loop (on|off)");
				break;

			case "next":
				audioFramework.Next();
				break;

			case "pm":
				WriteClient(client, "Hi " + client.NickName);
				break;

			case "p":
			case "play":
				if (command.Length == 1)
					audioFramework.Play();
				else if (command.Length >= 2)
					PlayAuto(client, argumentUncut, false);
				else
					WriteClient(client, "Missing or too many parameter. Usage !play [<url/path>]");
				break;

			case "prev":
				audioFramework.Previous();
				break;

			case "repeat":
				if (command.Length == 2)
				{
					if (command[1] == "on")
						audioFramework.Repeat = true;
					else if (command[1] == "off")
						audioFramework.Repeat = false;
				}
				else
					WriteClient(client, "Missing or too many parameter. Usage !repeat (on|off)");
				break;

			case "seek":
				if (command.Length == 2)
					AudioSeek(client, command[1]);
				else
					WriteClient(client, "Missing or too many parameter. Usage !seek [<min>:]<sek>");
				break;

			case "stop":
				audioFramework.Stop();
				break;

			case "volume":
				if (command.Length == 2)
					SetVolume(client, command[1]);
				else
					WriteClient(client, "Missing or too many parameter. Usage !volume <int>(0-200)");
				break;

			case "yt":
			case "youtube":
				if (command.Length >= 2)
					PlayYoutube(client, argumentUncut, false);
				else
					WriteClient(client, "Missing or too many parameter. Usage !yt <youtube-url>");
				break;

			default:
				if (client != null)
					await queryConnection.TSClient.SendMessage("Unknown command!", client);
				break;
			}
		}

		private string ExtractUrlFromBB(string ts3link)
		{
			if (ts3link.Contains("[URL]"))
				return Regex.Match(ts3link, @"\[URL\](.+?)\[\/URL\]").Groups[1].Value;
			else
				return ts3link;
		}

		private void SetVolume(GetClientsInfo client, string message)
		{
			int volume;
			if (int.TryParse(message, out volume) && (volume >= 0 && volume <= AudioFramework.MAXVOLUME))
				audioFramework.Volume = volume;
			else
				WriteClient(client, "The parameter is not a valid integer.");
		}

		private void KickClient(GetClientsInfo client, string message)
		{
			if (client != null)
			{
				try
				{
					if (message == null)
						queryConnection.TSClient.KickClient(client, KickOrigin.Channel).Wait();
					else if (message == "far")
						queryConnection.TSClient.KickClient(client, KickOrigin.Server).Wait();
				}
				catch (Exception ex)
				{
					Log.Write(Log.Level.Info, "Could not kick: {0}", ex);
				}
			}
		}

		private void AudioSeek(GetClientsInfo client, string message)
		{
			int seconds = -1;
			bool parsed = false;
			if (message.Contains(":"))
			{
				string[] splittime = message.Split(':');
				if (splittime.Length == 2)
				{
					int minutes = -1;
					parsed = int.TryParse(splittime[0], out minutes) && int.TryParse(splittime[1], out seconds);
					if (parsed)
					{
						seconds = (int)(TimeSpan.FromSeconds(seconds) + TimeSpan.FromMinutes(minutes)).TotalSeconds;
					}
				}
			}
			else
			{
				parsed = int.TryParse(message, out seconds);
			}

			if (!parsed)
			{
				WriteClient(client, "The parameter is not a valid integer.");
				return;
			}

			if (!audioFramework.Seek(seconds))
				WriteClient(client, "The point of time is not within the songlenth.");
		}

		private void PlayAuto(GetClientsInfo client, string message, bool enqueue)
		{
			string netlinkurl = ExtractUrlFromBB(message);
			if (Regex.IsMatch(netlinkurl, @"^(https?\:\/\/)?(www\.)?(youtube\.|youtu\.be)"))
			{
				//Is a youtube link
				PlayYoutube(client, message, enqueue);
			}
			else
			{
				//Is a youtube link
				PlayLink(client, message, enqueue);
			}
		}

		private void PlayLink(GetClientsInfo client, string message, bool enqueue)
		{
			string netlinkurl = ExtractUrlFromBB(message);
			var mediaRessource = new MediaRessource(netlinkurl);
			mediaRessource.Enqueue = enqueue;
			if (!audioFramework.StartRessource(mediaRessource))
				WriteClient(client, "The ressource could not be played...");
		}

		private void PlayYoutube(GetClientsInfo client, string message, bool enqueue)
		{
			string netlinkurl = ExtractUrlFromBB(message);
			// TODO: lookup in history...
			if (youtubeFramework.ExtractURL(netlinkurl) != ResultCode.Success)
			{
				WriteClient(client, "Invalid URL or no media found...");
				return;
			}
			youtubeFramework.LoadedRessource.Enqueue = enqueue;

			if (youtubeFramework.LoadedRessource.AvailableTypes.Count == 1)
			{
				if (!audioFramework.StartRessource(youtubeFramework.LoadedRessource))
					WriteClient(client, "The ressource could not be played...");
				return;
			}

			StringBuilder strb = new StringBuilder();
			strb.AppendLine("\nMultiple formats found please choose one with !f <number>");
			int count = 0;
			foreach (var videoType in youtubeFramework.LoadedRessource.AvailableTypes)
			{
				strb.Append("[");
				strb.Append(count++);
				strb.Append("] ");
				strb.Append(videoType.codec.ToString());
				strb.Append(" @ ");
				strb.AppendLine(videoType.qualitydesciption);
			}
			WriteClient(client, strb.ToString());
			awaitingResponse = YoutubeAwait;
		}

		private async Task<bool> YoutubeAwait(TextMessage tm)
		{
			string[] command = tm.Message.Split(' ');
			if (command[0] != "!f")
				return false;
			if (command.Length != 2)
				return true;
			int entry;
			if (int.TryParse(command[1], out entry))
			{
				YoutubeRessource ytRessource = youtubeFramework.LoadedRessource;
				if (entry < 0 || entry >= ytRessource.AvailableTypes.Count)
					return true;
				ytRessource.Selected = entry;
				if (!audioFramework.StartRessource(ytRessource))
				{
					GetClientsInfo client = await queryConnection.GetClientById(tm.InvokerId);
					WriteClient(client, "The network stream could not be played...");
				}
			}
			return true;
		}

		private async void WriteClient(GetClientsInfo client, string message)
		{
			if (client != null)
				await queryConnection.TSClient.SendMessage(message, client);
		}

		public void Dispose()
		{
			Log.Write(Log.Level.Info, "Exiting...");
			run = false;
			if (audioFramework != null)
			{
				audioFramework.Dispose();
				audioFramework = null;
			}
			if (bobController != null)
			{
				bobController.Dispose();
				bobController = null;
			}
			if (queryConnection != null)
			{
				queryConnection.Dispose();
				queryConnection = null;
			}
			if (youtubeFramework != null)
			{
				youtubeFramework.Dispose();
				youtubeFramework = null;
			}
		}
	}

	struct MainBotData
	{
		[InfoAttribute("path to the logfile", "log_ts3audiobot")]
		public string logFile;
	}
}
