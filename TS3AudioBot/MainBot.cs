using System;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot
{
	public class MainBot
	{
		static void Main(string[] args)
		{
			MainBot bot = new MainBot();
			bot.Run();
		}

		private const string configFilePath = "configTS3AudioBot.cfg";

		AudioFramework audioFramework;
		BobController bobController;
		QueryConnection queryConnection;
		YoutubeFramework youtubeFramework;
		Func<TextMessage, Task<bool>> awatingResponse = null;

		public MainBot()
		{
			// Read Config File
			ConfigFile cfgFile = ConfigFile.Open(configFilePath);
			if (cfgFile == null)
				cfgFile = ConfigFile.Create(configFilePath);
			if (cfgFile == null)
				cfgFile = ConfigFile.GetDummy();
			QueryConnectionData qcd = cfgFile.GetDataStruct<QueryConnectionData>(typeof(QueryConnection), true);
			BobControllerData bcd = cfgFile.GetDataStruct<BobControllerData>(typeof(BobController), true);
			cfgFile.Close();

			// Initialize Modules
			audioFramework = new AudioFramework();

			youtubeFramework = new YoutubeFramework();

			bobController = new BobController(bcd);
			audioFramework.RessourceStarted += (audioRessource) =>
			{
				bobController.Start();
				bobController.Sending = true;
				//bobController.Quality = true;
			};
			audioFramework.RessourceStopped += () =>
			{
				bobController.StartEndTimer();
				bobController.Sending = false;
			};
			queryConnection = new QueryConnection(qcd);
			queryConnection.Callback = TextCallback;
			queryConnection.Connect();
		}

		public void Run()
		{
			bool run = true;
			while (run)
			{
				string input;
				try
				{
					input = Console.ReadLine();
				}
				catch
				{
					Task.Delay(1000).Wait();
					continue;
				}
				if (input == null)
				{
					Task.Delay(1000).Wait();
					continue;
				}
				bobController.HasUpdate();

				string[] command = input.Split(' ');

				switch (command[0])
				{
				case "help":
					Console.WriteLine("nOTHING!1!!!!!");
					break;

				case "q":
				case "quit":
					Console.WriteLine("Exiting...");
					run = false;
					audioFramework.Close();
					queryConnection.Close();
					bobController.Stop();
					continue;

				case "vlctest":
					Console.WriteLine(audioFramework.playerConnection.IsPlaying());
					break;

				default:
					Console.WriteLine("Unknow command type help for more info.");
					break;
				}
			}
		}

		public async void TextCallback(TextMessage tm)
		{
			if (awatingResponse != null)
			{
				if (await awatingResponse(tm))
				{
					awatingResponse = null;
					return;
				}
			}

			if (!tm.Message.StartsWith("!"))
				return;
			string[] command = tm.Message.Substring(1).Split(' ');
			bobController.HasUpdate();

			GetClientsInfo client = await queryConnection.GetClientById(tm.InvokerId);

			switch (command[0])
			{
			case "pm":
				WriteClient(client, "Hi " + client.NickName);
				break;
			case "kickme":
				if (client != null)
					await queryConnection.TSClient.KickClient(client, KickOrigin.Channel);
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
			case "p":
			case "play":
				if (command.Length != 2)
				{
					WriteClient(client, "Missing or too many parameter. Usage !play <url/path>");
					break;
				}
				PlayAuto(client, command[1]);
				break;
			case "yt":
			case "youtube":
				if (command.Length != 2)
				{
					WriteClient(client, "Missing or too many parameter. Usage !yt <youtube-url>");
					break;
				}
				PlayYoutube(client, command[1]);
				break;
			case "l":
			case "link":
				if (command.Length != 2)
				{
					WriteClient(client, "Missing or too many parameter. Usage !link <url/path>");
					break;
				}
				PlayLink(client, command[1]);
				break;
			case "stop":
				audioFramework.Stop();
				break;
			case "seek":
				if (command.Length != 2)
				{
					WriteClient(client, "Missing or too many parameter. Usage !seek <int>");
					break;
				}
				AudioSeek(client, command[1]);
				break;
			case "history":
				//TODO
				break;
			case "startbot":
				bobController.Start();
				break;
			case "stopbot":
				bobController.Stop();
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
				WriteClient(client, "The parameter is not a valid second integer.");
				return;
			}

			// TODO: move method call to audioframework
			if (seconds < 0 || seconds > audioFramework.playerConnection.GetLength())
			{
				WriteClient(client, "The point of time is not within the songlenth.");
				return;
			}
			audioFramework.playerConnection.SetPosition(seconds);
		}

		private void PlayAuto(GetClientsInfo client, string message)
		{
			string netlinkurl = ExtractUrlFromBB(message);
			if (Regex.IsMatch(netlinkurl, @"^(https?\:\/\/)?(www\.)?(youtube\.|youtu\.be)"))
			{
				//Is a youtube link
				PlayYoutube(client, message);
			}
			else
			{
				//Is a youtube link
				PlayLink(client, message);
			}
		}

		private void PlayLink(GetClientsInfo client, string message)
		{
			string netlinkurl = ExtractUrlFromBB(message);
			if (!audioFramework.StartRessource(new MediaRessource(netlinkurl)))
				WriteClient(client, "The local file could not be played...");
		}

		private void PlayYoutube(GetClientsInfo client, string message)
		{
			string netlinkurl = ExtractUrlFromBB(message);
			// TODO: lookup in history...
			if (!youtubeFramework.ExtractedURL(netlinkurl))
			{
				WriteClient(client, "Invalid URL or no media found...");
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
			awatingResponse = YoutubeAwait;
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
	}

}
