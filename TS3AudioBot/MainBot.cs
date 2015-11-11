using System;
using System.IO;
using System.Linq;
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
				AppDomain.CurrentDomain.UnhandledException += (s, e) =>
				{
					var ex = e.ExceptionObject as Exception;
					if (ex != null)
						Log.Write(Log.Level.Error, "Critical program failure: {0}", ex);
					if (bot != null)
						bot.Dispose();
				};

				if (!bot.ReadParameter(args))
				{
					bot.InitializeBot();
					bot.InitializeCommands();
					bot.Run();
				}
			}
		}

		bool run;
		bool noInput;
		bool consoleOutput;
		bool writeLog;
		MainBotData mainBotData;
		Trie<BotCommand> commandDict;
		BotCommand[] allCommands;

		StreamWriter logStream;

		internal AudioFramework AudioFramework { get; private set; }
		internal BobController BobController { get; private set; }
		internal QueryConnection QueryConnection { get; private set; }
		internal SessionManager SessionManager { get; private set; }
		internal HistoryManager HistoryManager { get; private set; }

		IRessourceFactory mediaFactory;
		IRessourceFactory youtubeFramework;
		IRessourceFactory soundcloudFramework;

		public bool QuizMode { get; set; }

		public MainBot()
		{
			run = true;
			noInput = false;
			consoleOutput = false;
			writeLog = false;
			commandDict = new Trie<BotCommand>();
		}

		public bool ReadParameter(string[] args)
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
				return true;
			}
			noInput = launchParameter.Contains("--NoInput") || launchParameter.Contains("-I");
			consoleOutput = !(launchParameter.Contains("--Silent") || launchParameter.Contains("-S"));
			writeLog = !(launchParameter.Contains("--NoLog") || launchParameter.Contains("-L"));
			return false;
		}

		public void InitializeBot()
		{
			// Read Config File
			string configFilePath = Util.GetFilePath(FilePath.ConfigFile);
			ConfigFile cfgFile = ConfigFile.Open(configFilePath) ?? ConfigFile.Create(configFilePath) ?? ConfigFile.GetDummy();
			AudioFrameworkData afd = cfgFile.GetDataStruct<AudioFrameworkData>(typeof(AudioFramework), true);
			BobControllerData bcd = cfgFile.GetDataStruct<BobControllerData>(typeof(BobController), true);
			QueryConnectionData qcd = cfgFile.GetDataStruct<QueryConnectionData>(typeof(QueryConnection), true);
			mainBotData = cfgFile.GetDataStruct<MainBotData>(typeof(MainBot), true);
			cfgFile.Close();

			if (consoleOutput)
			{
				Log.RegisterLogger("[%T]%L: %M", "", Console.WriteLine);
			}

			if (writeLog && !string.IsNullOrEmpty(mainBotData.logFile))
			{
				var encoding = new UTF8Encoding(false);
				logStream = new StreamWriter(File.Open(mainBotData.logFile, FileMode.Append, FileAccess.Write, FileShare.Read), encoding);
				Log.RegisterLogger("[%T]%L: %M\n", "", (msg) =>
				{
					if (logStream != null)
					{
						logStream.Write(msg);
						logStream.Flush();
					}
				});
			}

			Log.Write(Log.Level.Info, "[============ TS3AudioBot started =============]");
			string dateStr = DateTime.Now.ToLongDateString();
			Log.Write(Log.Level.Info, "[=== Date: {0}{1} ===]", new string(' ', Math.Max(0, 32 - dateStr.Length)), dateStr);
			string timeStr = DateTime.Now.ToLongTimeString();
			Log.Write(Log.Level.Info, "[=== Time: {0}{1} ===]", new string(' ', Math.Max(0, 32 - timeStr.Length)), timeStr);
			Log.Write(Log.Level.Info, "[==============================================]");

			// Initialize Modules
			AudioFramework = new AudioFramework(afd);
			BobController = new BobController(bcd);
			QueryConnection = new QueryConnection(qcd);
			SessionManager = new SessionManager();
			HistoryManager = new HistoryManager();

			mediaFactory = new MediaFactory();
			youtubeFramework = new YoutubeFramework();
			soundcloudFramework = new SoundcloudFramework();

			// Register callbacks
			AudioFramework.OnRessourceStarted += HistoryManager.LogAudioRessource;
			AudioFramework.OnRessourceStarted += BobController.OnRessourceStarted;
			AudioFramework.OnRessourceStopped += BobController.OnRessourceStopped;


			// register callback for all messages happeing
			QueryConnection.OnMessageReceived += TextCallback;
			// register callback to remove open private sessions, when user disconnects
			QueryConnection.OnClientDisconnect += (s, e) => SessionManager.RemoveSession(e.InvokerId);
			// give the bobController a reference to the query so he can communicate with the queryClient
			BobController.QueryConnection = QueryConnection;
			// create a default session for all users in all chat
			SessionManager.DefaultSession = new PublicSession(this);
			// connect the query after everyting is set up
			var connectTask = QueryConnection.Connect();
		}

		public void InitializeCommands()
		{
			var allCommandsList = new List<BotCommand>();

			var builder = new BotCommand.Builder(botCommand =>
			{
				commandDict.Add(botCommand.InvokeName, botCommand);
				allCommandsList.Add(botCommand);
			});

			// [...] = Optional
			// <name> = Placeholder for a text
			// [text] = Option for fixed text
			// (a|b) = either or switch

			builder.New("add").Action(CommandAdd).Permission(CommandRights.Private).HelpData("Adds a new song to the queue.", "<link>").Finish();
			builder.New("clear").Action(CommandClear).Permission(CommandRights.Private).HelpData("Removes all songs from the current playlist.").Finish();
			builder.New("getuserid").Action(CommandGetUserId).Permission(CommandRights.Private).HelpData("Gets the unique Id of a user.", "<username>").Finish();
			builder.New("help").Action(CommandHelp).Permission(CommandRights.Private).HelpData("Shows all commands or detailed help about a specific command.", "[<command>]").Finish();
			builder.New("history").Action(CommandHistory).Permission(CommandRights.Private).HelpData("Shows recently played songs.").Finish();
			builder.New("kickme").Action(CommandKickme).Permission(CommandRights.Private).HelpData("Guess what?", "[far]").Finish();
			builder.New("link").Action(CommandLink).Permission(CommandRights.Private).HelpData("Plays any direct ressource link.", "<link>").Finish();
			builder.New("loop").Action(CommandLoop).Permission(CommandRights.Private).HelpData("Sets wether of not to loop the entire playlist.", "(on|off)").Finish();
			builder.New("next").Action(CommandNext).Permission(CommandRights.Private).HelpData("Plays the next song in the playlist.").Finish();
			builder.New("pm").Action(CommandPM).Permission(CommandRights.Public).HelpData("Reuests private session with the ServerBot so you can invoke private commands.").Finish();
			builder.New("play").Action(CommandPlay).Permission(CommandRights.Private)
				.HelpData("Automatically tries to decide wether the link is a special ressource (like youtube) or a direct ressource (like ./hello.mp3) and starts it", "<link>").Finish();
			builder.New("previous").Action(CommandPrevious).Permission(CommandRights.Private).HelpData("Plays the previous song in the playlist.").Finish();
			builder.New("quit").Action(CommandQuit).Permission(CommandRights.Admin).HelpData("Closes the TS3AudioBot application.").Finish();
			builder.New("quiz").Action(CommandQuiz).Permission(CommandRights.Public).HelpData("Enable to hide the songnames and let your friends guess the title.", "(on|off)").Finish();
			builder.New("repeat").Action(CommandRepeat).Permission(CommandRights.Private).HelpData("Sets wether or not to loop a single song", "(on|off)").Finish();
			builder.New("seek").Action(CommandSeek).Permission(CommandRights.Private).HelpData("Jumps to a timemark within the current song.", "(<time in seconds>|<seconds>:<minutes>)").Finish();
			builder.New("song").Action(CommandSong).Permission(CommandRights.AnyVisibility).HelpData("Tells you the name of the current song.").Finish();
			builder.New("soundcloud").Action(CommandSoundcloud).Permission(CommandRights.Private).HelpData("Resolves the link as a soundcloud song to play it for you.").Finish();
			builder.New("subscribe").Action(CommandSubscribe).Permission(CommandRights.Private).HelpData("Lets you hear the music independent from the channel you are in.").Finish();
			builder.New("stop").Action(CommandStop).Permission(CommandRights.Private).HelpData("Stops the current song.").Finish();
			builder.New("test").Action(CommandTest).Permission(CommandRights.Admin).HelpData("Only for debugging purposes").Finish();
			builder.New("unsubscribe").Action(CommandUnsubscribe).Permission(CommandRights.Private).HelpData("Only lets you hear the music in active channels again.").Finish();
			builder.New("volume").Action(CommandVolume).Permission(CommandRights.AnyVisibility).HelpData("Sets the volume level of the music.", "<level(0-200)>").Finish();
			builder.New("youtube").Action(CommandYoutube).Permission(CommandRights.Private).HelpData("Resolves the link as a youtube video to play it for you.").Finish();

			allCommands = allCommandsList.ToArray();
		}

		public void Run()
		{
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

		public async void TextCallback(object sender, TextMessage textMessage)
		{
			Log.Write(Log.Level.Debug, "MB Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			if (!textMessage.Message.StartsWith("!"))
				return;
			BobController.HasUpdate();

			await QueryConnection.RefreshClientBuffer(true);

			BotSession session = SessionManager.GetSession(textMessage.TargetMode, textMessage.InvokerId);
			if (textMessage.TargetMode == MessageTarget.Private && session == SessionManager.DefaultSession)
			{
				Log.Write(Log.Level.Debug, "MB User {0} created auto-private session with the bot", textMessage.InvokerName);
				session = await SessionManager.CreateSession(this, textMessage.InvokerId);
			}

			var isAdmin = AsyncLazy<bool>.CreateAsyncLazy(HasInvokerAdminRights, textMessage);

			// check if the user has an open request
			if (session.ResponseProcessor != null)
			{
				if (session.ResponseProcessor(session, textMessage, session.AdminResponse && await isAdmin.GetValue()))
				{
					session.ClearResponse();
					return;
				}
			}

			string commandSubstring = textMessage.Message.Substring(1);
			string[] commandSplit = commandSubstring.Split(new[] { ' ' }, 2);
			BotCommand command = null;
			if (!commandDict.TryGetValue(commandSplit[0], out command))
			{
				session.Write("Unknown command!");
				return;
			}

			string reason = string.Empty;
			bool allowed = false;
			// check if the command need certain rights/specs
			switch (command.CommandRights)
			{
			case CommandRights.Admin:
				reason = "Command must be invoked by an admin!";
				break;
			case CommandRights.Public:
				reason = "Command must be used in public mode!";
				allowed = textMessage.TargetMode == MessageTarget.Server;
				break;
			case CommandRights.Private:
				reason = "Command must be used in a private session!";
				allowed = textMessage.TargetMode == MessageTarget.Private;
				break;
			case CommandRights.AnyVisibility:
				allowed = true;
				break;
			}
			if (!allowed && !await isAdmin.GetValue())
			{
				session.Write(reason);
				return;
			}

			switch (command.CommandParameter)
			{
			case CommandParameter.Nothing:
				command.CommandN(session);
				break;

			case CommandParameter.Remainder:
				if (commandSplit.Length < 2)
					command.CommandS(session, string.Empty);
				else
					command.CommandS(session, commandSplit[1]);
				break;

			case CommandParameter.TextMessage:
				command.CommandTM(session, textMessage);
				break;

			case CommandParameter.MessageAndRemainder:
				if (commandSplit.Length < 2)
					command.CommandTMS(session, textMessage, string.Empty);
				else
					command.CommandTMS(session, textMessage, commandSplit[1]);
				break;

			case CommandParameter.Undefined:
			default:
				throw new InvalidOperationException("Command with no process");
			}
		}

		private async Task<bool> HasInvokerAdminRights(InvokerInformation textMessage)
		{
			Log.Write(Log.Level.Debug, "AdminCheck called!");
			GetClientsInfo client = await QueryConnection.GetClientById(textMessage.InvokerId);
			if (client == null)
				return false;
			int[] clientSgIds = await QueryConnection.GetClientServerGroups(client);
			return clientSgIds.Contains(mainBotData.adminGroupId);
		}

		// COMMANDS

		private async void CommandAdd(BotSession session, TextMessage textMessage, string parameter)
		{
			GetClientsInfo client = await QueryConnection.GetClientById(textMessage.InvokerId);
			LoadAndPlayAuto(new PlayData(session, client, parameter, true));
		}

		private void CommandClear(BotSession session, string parameter)
		{
			AudioFramework.Clear();
		}

		private async void CommandGetUserId(BotSession session, string parameter)
		{
			GetClientsInfo client = await QueryConnection.GetClientByName(parameter);
			if (client == null)
				session.Write("No user found...");
			else
				session.Write(string.Format("Client: UID:{0} DBID:{1} ChanID:{2}", client.Id, client.DatabaseId, client.ChannelId));
		}

		private void CommandHelp(BotSession session, string parameter)
		{
			var strb = new StringBuilder();
			if (string.IsNullOrEmpty(parameter))
			{
				strb.Append("\n========= Welcome to the TS3AudioBot ========="
					+ "\nIf you need any help with a special command use !help commandName."
					+ "\nHere are all possible commands:\n");
				foreach (var command in allCommands)
					strb.Append(command.InvokeName).Append(", ");
			}
			else
			{
				BotCommand command = null;
				if (!commandDict.TryGetValue(parameter, out command))
				{
					session.Write("No matching command found! Try !help to get a list of all commands.");
					return;
				}
				strb.Append(command.GetHelp());
				if (command.InvokeName == "help")
				{
					strb.Append("\nProtips:\n> You can truncate any command as long as it stays unique:"
						+ "\nfor example: !subscribe can be shortened with !sub or even !su");
				}
			}
			session.Write(strb.ToString());
		}

		private void CommandHistory(BotSession session, string parameter)
		{
			//TODO
		}

		private async void CommandKickme(BotSession session, TextMessage textMessage, string parameter)
		{
			try
			{
				if (string.IsNullOrEmpty(parameter))
					await QueryConnection.TSClient.KickClient(textMessage.InvokerId, KickOrigin.Channel);
				else if (parameter == "far")
					await QueryConnection.TSClient.KickClient(textMessage.InvokerId, KickOrigin.Server);
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Info, "Could not kick: {0}", ex);
			}
		}

		private async void CommandLink(BotSession session, TextMessage textMessage, string parameter)
		{
			GetClientsInfo client = await QueryConnection.GetClientById(textMessage.InvokerId);
			LoadAndPlay(mediaFactory, new PlayData(session, client, parameter, false));
		}

		private void CommandLoop(BotSession session, string parameter)
		{
			if (parameter == "on")
				AudioFramework.Loop = true;
			else if (parameter == "off")
				AudioFramework.Loop = false;
			else
				CommandHelp(session, "loop");
		}

		private void CommandNext(BotSession session)
		{
			AudioFramework.Next();
		}

		private async void CommandPM(BotSession session, TextMessage textMessage)
		{
			BotSession ownSession = await SessionManager.CreateSession(this, textMessage.InvokerId);
			ownSession.Write("Hi " + textMessage.InvokerName);
		}

		private async void CommandPlay(BotSession session, TextMessage textMessage, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				AudioFramework.Play();
			else
			{
				GetClientsInfo client = await QueryConnection.GetClientById(textMessage.InvokerId);
				LoadAndPlayAuto(new PlayData(session, client, parameter, false));
			}
		}

		private void CommandPrevious(BotSession session, string parameter)
		{
			AudioFramework.Previous();
		}

		private void CommandQuit(BotSession session)
		{
			if (!noInput)
			{
				session.Write("The TS3AudioBot is open in console-mode. Please close it in the opened terminal.");
				return;
			}
			Task.WaitAll(new[] { Task.Run(() => session.Write("Goodbye!")) }, 500);
			this.Dispose();
			Log.Write(Log.Level.Info, "Exiting...");
		}

		private void CommandQuiz(BotSession session, string parameter)
		{
			if (parameter == "on")
				QuizMode = true;
			else if (parameter == "off")
				QuizMode = false;
			else
				CommandHelp(session, "quiz");
		}

		private void CommandRepeat(BotSession session, string parameter)
		{
			if (parameter == "on")
				AudioFramework.Repeat = true;
			else if (parameter == "off")
				AudioFramework.Repeat = false;
			else
				CommandHelp(session, "repeat");
		}

		private void CommandSeek(BotSession session, string parameter)
		{
			int seconds = -1;
			bool parsed = false;
			if (parameter.Contains(":"))
			{
				string[] splittime = parameter.Split(':');
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
				parsed = int.TryParse(parameter, out seconds);
			}

			if (!parsed)
			{
				CommandHelp(session, "seek");
				return;
			}

			if (!AudioFramework.Seek(seconds))
				session.Write("The point of time is not within the songlenth.");
		}

		private void CommandSong(BotSession session, TextMessage textMessage)
		{
			if (AudioFramework.currentRessource == null)
			{
				session.Write("There is nothing on right now...");
				return;
			}

			if (QuizMode && AudioFramework.currentRessource.InvokingUser.Id != textMessage.InvokerId)
				session.Write("Sorry, you have to guess!");
			else
				session.Write(AudioFramework.currentRessource.RessourceTitle);
		}

		private async void CommandSoundcloud(BotSession session, TextMessage textMessage, string parameter)
		{
			GetClientsInfo client = await QueryConnection.GetClientById(textMessage.InvokerId);
			LoadAndPlay(soundcloudFramework, new PlayData(session, client, parameter, false));
		}

		private void CommandStop(BotSession session, string parameter)
		{
			AudioFramework.Stop();
		}

		private void CommandSubscribe(BotSession session, TextMessage textMessage)
		{
			BobController.WhisperClientSubscribe(textMessage.InvokerId);
		}

		private void CommandTest(BotSession session)
		{
			// stresstest
			for (int i = 0; i < 10; i++)
				session.Write(i.ToString());

			return;
			PrivateSession ps = session as PrivateSession;
			if (ps == null)
			{
				session.Write("Please use as private, admins too!");
				return;
			}
			else
			{
				ps.Write("Good boy!");
				//await queryConnection.GetClientServerGroups(ps.client);
			}
		}

		private void CommandUnsubscribe(BotSession session, TextMessage textMessage)
		{
			BobController.WhisperClientUnsubscribe(textMessage.InvokerId);
		}

		private void CommandVolume(BotSession session, string parameter)
		{
			int volume;
			if (int.TryParse(parameter, out volume) && volume >= 0)
			{
				if (volume <= AudioFramework.MAXUSERVOLUME || volume < AudioFramework.Volume)
				{
					AudioFramework.Volume = volume;
					return;
				}
				else if (volume <= AudioFramework.MAXVOLUME)
				{
					session.Write("Careful you are requesting a very high volume! Do you want to apply this? !(y|n)");
					session.SetResponse(ResponseVolume, volume, true);
					return;
				}
			}

			CommandHelp(session, "volume");
		}

		private async void CommandYoutube(BotSession session, TextMessage textMessage, string parameter)
		{
			GetClientsInfo client = await QueryConnection.GetClientById(textMessage.InvokerId);
			LoadAndPlay(youtubeFramework, new PlayData(session, client, parameter, false));
		}

		// HELPER

		private static string ExtractUrlFromBB(string ts3link)
		{
			if (ts3link.Contains("[URL]"))
				return Regex.Match(ts3link, @"\[URL\](.+?)\[\/URL\]").Groups[1].Value;
			else
				return ts3link;
		}

		private void LoadAndPlayAuto(PlayData data)
		{
			string netlinkurl = ExtractUrlFromBB(data.Message);
			IRessourceFactory factory = null;
			if (Regex.IsMatch(netlinkurl, @"^(https?\:\/\/)?(www\.)?(youtube\.|youtu\.be)"))
				factory = youtubeFramework;
			else if (Regex.IsMatch(netlinkurl, @"^https?\:\/\/(www\.)?soundcloud\."))
				factory = soundcloudFramework;
			else
				factory = mediaFactory;
			LoadAndPlay(factory, data);
		}

		private void LoadAndPlay(IRessourceFactory factory, PlayData data)
		{
			string netlinkurl = ExtractUrlFromBB(data.Message);

			if (data.Ressource == null)
			{
				AudioRessource ressource;
				if (!factory.GetRessource(netlinkurl, out ressource))
				{
					data.Session.Write("Invalid URL or no media found...");
					return;
				}
				data.Ressource = ressource;
			}

			bool abortPlay;
			factory.PostProcess(data, out abortPlay);
			Play(data);
		}

		internal void Play(PlayData data)
		{
			data.Ressource.Enqueue = data.Enqueue;
			var result = AudioFramework.StartRessource(data.Ressource, data.Invoker);
			if (result != AudioResultCode.Success)
				data.Session.Write(string.Format("The ressource could not be played ({0}).", result));
		}

		// RESPONSES

		private bool ResponseVolume(BotSession session, TextMessage tm, bool isAdmin)
		{
			string[] command = tm.Message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (command[0] == "!y" || command[0] == "!n")
			{
				if (isAdmin)
				{
					if (!(session.ResponseData is int))
					{
						Log.Write(Log.Level.Error, "responseData is not an int.");
						return true;
					}
					if (command[0] == "!y")
					{
						AudioFramework.Volume = (int)session.ResponseData;
					}
				}
				else
				{
					session.Write("Command can only be answered by an admin.");
				}
				return true;
			}
			return false;
		}

		public void Dispose()
		{
			run = false;
			if (AudioFramework != null)
			{
				AudioFramework.Dispose();
				AudioFramework = null;
			}
			if (BobController != null)
			{
				BobController.Dispose();
				BobController = null;
			}
			if (QueryConnection != null)
			{
				QueryConnection.Dispose();
				QueryConnection = null;
			}
			if (youtubeFramework != null)
			{
				youtubeFramework.Dispose();
				youtubeFramework = null;
			}
			if (SessionManager != null)
			{
				//sessionManager.Dispose();
				SessionManager = null;
			}
			if (logStream != null)
			{
				logStream.Dispose();
				logStream = null;
			}
		}
	}

	class PlayData
	{
		public BotSession Session { get; private set; }
		public GetClientsInfo Invoker { get; private set; }
		public string Message { get; private set; }
		public bool Enqueue { get; private set; }
		public AudioRessource Ressource { get; set; }

		public PlayData(BotSession session, GetClientsInfo invoker, string message, bool enqueue)
		{
			Session = session;
			Invoker = invoker;
			Message = message;
			Enqueue = enqueue;
			Ressource = null;
		}
	}

	class BotCommand
	{
		public string InvokeName { get; private set; }

		public Action<BotSession> CommandN { get; private set; }
		public Action<BotSession, string> CommandS { get; private set; }
		public Action<BotSession, TextMessage> CommandTM { get; private set; }
		public Action<BotSession, TextMessage, string> CommandTMS { get; private set; }
		public CommandParameter CommandParameter { get; private set; }
		public CommandRights CommandRights { get; private set; }

		private string outputCache = null;
		public string Description { get; private set; }
		public string[] ParameterList { get; private set; }

		private BotCommand() { }

		public string GetHelp()
		{
			if (outputCache == null)
			{
				StringBuilder strb = new StringBuilder();
				strb.Append("\nUsage: ").Append('!').Append(InvokeName);
				foreach (string para in ParameterList)
					strb.Append(" ").Append(para);
				strb.Append('\n').Append(Description);
				outputCache = strb.ToString();
			}
			return outputCache;
		}

		public override string ToString()
		{
			return string.Format("!{0} - {1} - {2} : {3}", InvokeName, CommandParameter, CommandRights, ParameterList);
		}

		public class Builder
		{
			private bool buildMode;
			private Action<BotCommand> registerAction;

			// Default values
			private const CommandRights defaultCommandRights = CommandRights.Admin;
			private const string defaultDescription = "<no info>";
			private static readonly string[] defaultParameters = new string[0];

			// List of configurations for each command
			private string name;

			private bool setAction = false;
			private Action<BotSession> commandN;
			private Action<BotSession, string> commandS;
			private Action<BotSession, TextMessage> commandTM;
			private Action<BotSession, TextMessage, string> commandTMS;
			private CommandParameter commandParameter;

			private bool setRights = false;
			private CommandRights commandRights;

			private bool setHelp = false;
			private string description;
			private string[] parameters;

			private Builder(Action<BotCommand> finishAction, bool buildMode)
			{
				this.buildMode = buildMode;
				this.registerAction = finishAction;
			}

			public Builder(Action<BotCommand> finishAction) : this(finishAction, false) { }

			public Builder New(string invokeName)
			{
				var cb = new Builder(registerAction, true);
				cb.name = invokeName;
				return cb;
			}

			private void CheckAction()
			{
				if (setAction) throw new InvalidOperationException();
				setAction = true;
			}

			public Builder Action(Action<BotSession> commandN)
			{
				CheckAction();
				this.commandN = commandN;
				commandParameter = CommandParameter.Nothing;
				return this;
			}

			public Builder Action(Action<BotSession, string> commandS)
			{
				CheckAction();
				this.commandS = commandS;
				commandParameter = CommandParameter.Remainder;
				return this;
			}

			public Builder Action(Action<BotSession, TextMessage> commandTM)
			{
				CheckAction();
				this.commandTM = commandTM;
				commandParameter = CommandParameter.TextMessage;
				return this;
			}

			public Builder Action(Action<BotSession, TextMessage, string> commandTMS)
			{
				CheckAction();
				this.commandTMS = commandTMS;
				commandParameter = CommandParameter.MessageAndRemainder;
				return this;
			}

			public Builder Permission(CommandRights requiredRights)
			{
				if (setRights) throw new InvalidOperationException();
				commandRights = requiredRights;
				setRights = true;
				return this;
			}

			public Builder HelpData(string description, params string[] parameters)
			{
				if (setHelp) throw new InvalidOperationException();
				this.description = description;
				this.parameters = parameters;
				setHelp = true;
				return this;
			}

			public BotCommand Finish()
			{
				if (!setAction) throw new InvalidProgramException("No action defined for " + name);

				var command = new BotCommand()
				{
					InvokeName = name,
					CommandN = commandN,
					CommandS = commandS,
					CommandTM = commandTM,
					CommandTMS = commandTMS,
					CommandParameter = commandParameter,
					CommandRights = setRights ? commandRights : defaultCommandRights,
					Description = setHelp ? description : defaultDescription,
					ParameterList = setHelp ? parameters : defaultParameters,
				};
				if (registerAction != null)
					registerAction(command);
				return command;
			}
		}
	}

	enum CommandParameter
	{
		Undefined,
		Nothing,
		Remainder,
		TextMessage,
		MessageAndRemainder
	}

	[Flags]
	enum CommandRights
	{
		Admin = 0,
		Public = 1 << 0,
		Private = 1 << 1,
		AnyVisibility = Public | Private,
	}

	struct MainBotData
	{
		[InfoAttribute("path to the logfile", "log_ts3audiobot")]
		public string logFile;
		[InfoAttribute("group able to execute admin commands from the bot")]
		public int adminGroupId;
	}
}
