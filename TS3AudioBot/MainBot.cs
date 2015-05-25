using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Reflection;

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
		QueryConnection queryConnection;
		YoutubeFramework youtubeFramework;
		Func<TextMessage, Task<bool>> awatingResponse = null;

		public MainBot()
		{
			// Read Config File
			ConfigFile cfgFile = ConfigFile.Open(configFilePath);
			if (cfgFile == null)
			{
				// Careful, cfgFile can still be null even after the Create call!
				cfgFile = ConfigFile.Create(configFilePath);
			}
			QueryConnectionData qcd = ConfigFile.GetStructData<QueryConnectionData>(cfgFile, typeof(QueryConnection), true);
			cfgFile.Close();

			// Initialize Modules
			audioFramework = new AudioFramework();

			youtubeFramework = new YoutubeFramework();

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
					continue;

				case "vlc":
					audioFramework.playerConnection.SendCommandRaw(input.Substring(command[0].Length + 1));
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

			if (!tm.Message.StartsWith("!")) return;
			string[] command = tm.Message.Substring(1).Split(' ');

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
			case "history":
				//TODO
				break;
			case "startbot":
				audioFramework.StartBotClient();
				break;
			case "stopbot":
				audioFramework.StopBotClient();
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
			if (command[0] != "!f") return false;
			if (command.Length != 2) return true;
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
