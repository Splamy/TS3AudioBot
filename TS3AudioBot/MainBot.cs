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
			#region pm
			case "pm":
				WriteClient(client, "Hi " + client.NickName);
				break;
			#endregion
			#region kickme
			case "kickme":
				if (client != null)
					await queryConnection.TSClient.KickClient(client, KickOrigin.Channel);
				break;
			#endregion
			#region help
			case "help":
				WriteClient(client, "\n" +
					"!pm: Get private audience with the AudioBot\n" +
					"!kickme: Does exactly what you think it does...\n" +
					"!youtube: Plays a video from youtube [yt]\n" +
					//"!bassnet: Plays a mp3/wav media file from url [bnet, bn]\n" +
					"!vlcnet: Plays any media file from url [vnet, vn]\n" +
					//"!basslocal: Plays a mp3/wav from the server [blocal, bl]\n" +
					"!vlclocal: Plays any media from the server [vlocal, vl]\n" +
					"!stop: Stops the current song\n" +
					"!startbot: Connects the MusicBot to TeamSpeak\n" +
					"!stopbot: Disconnects the MusicBot from TeamSpeak\n" +
					"!history: Shows you the last played songs\n");
				break;
			#endregion
			#region youtube
			case "yt":
			case "youtube":
				if (command.Length != 2)
				{
					WriteClient(client, "Missing or too many parameter. Usage !yt <youtube-url>");
					break;
				}
				string yturl = Regex.Match(command[1], @"\[URL\](.+?)\[\/URL\]").Groups[1].Value;
				if (!youtubeFramework.ExtractedURL(yturl))
				{
					WriteClient(client, "Invalid URL or no media found...");
					break;
				}
				StringBuilder strb = new StringBuilder();
				strb.AppendLine("\nMultiple formats found please choose one with !f <number>");
				for (int i = 0; i < youtubeFramework.bufferedList.Count; i++)
				{
					strb.Append("[");
					strb.Append(i);
					strb.Append("] ");
					strb.Append(youtubeFramework.bufferedList[i].codec.ToString());
					strb.Append(" @ ");
					strb.AppendLine(youtubeFramework.bufferedList[i].qualitydesciption);
				}
				WriteClient(client, strb.ToString());
				awatingResponse = YoutubeAwait;
				break;
			#endregion
			#region vlcnet
			case "vn":
			case "vnet":
			case "vlcnet":
				if (command.Length != 2)
				{
					WriteClient(client, "Missing or too many parameter. Usage !vlcnet <url>");
					break;
				}
				string netlinkurl = Regex.Match(command[1], @"\[URL\](.+?)\[\/URL\]").Groups[1].Value;
				if (!audioFramework.OpenNetworkVLC(netlinkurl))
					WriteClient(client, "The network file could not be played...");
				break;
			#endregion
			#region vlclocal
			case "vl":
			case "vlocal":
			case "vlclocal":
				if (command.Length != 2)
				{
					WriteClient(client, "Missing or too many parameter. Usage !vlclocal <path>");
					break;
				}
				if (!audioFramework.OpenLocalVLC(command[1]))
					WriteClient(client, "The local file could not be played...");
				break;
			#endregion
			#region stop
			case "stop":
				audioFramework.Stop();
				break;
			#endregion
			#region history
			case "history":
				//TODO
				break;
			#endregion
			#region startbot
			case "startbot":
				audioFramework.StartBotClient();
				break;
			#endregion
			#region stopbot
			case "stopbot":
				audioFramework.StopBotClient();
				break;
			#endregion
			#region default
			default:
				if (client != null)
					await queryConnection.TSClient.SendMessage("Unknown command!", client);
				break;
			#endregion
			}
		}

		private async Task<bool> YoutubeAwait(TextMessage tm)
		{
			string[] command = tm.Message.Split(' ');
			if (command[0] != "!f") return false;
			if (command.Length != 2) return true;
			int entry;
			if (int.TryParse(command[1], out entry))
			{
				if (entry < 0 || entry >= youtubeFramework.bufferedList.Count)
					return true;
				string networkstr = youtubeFramework.bufferedList[entry].link;
				if (!audioFramework.OpenNetworkVLC(networkstr))
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
