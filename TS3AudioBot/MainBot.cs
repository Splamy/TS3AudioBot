namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;

	using TS3AudioBot.Algorithm;
	using TS3AudioBot.Helper;
	using TS3AudioBot.ResourceFactories;

	using TS3Query;
	using TS3Query.Messages;

	// Todo:
	// - make the bot more pluing-able like (for e.g. history as plugin)
	//	    method for registering commands
	//	    method for registering events
	// - implement history missing features
	// - implement command stacking
	public sealed class MainBot : IDisposable
	{
		static void Main(string[] args)
		{
			using (MainBot bot = new MainBot())
			{
				AppDomain.CurrentDomain.UnhandledException += (s, e) =>
				{
					var ex = e.ExceptionObject as Exception;
					Log.Write(Log.Level.Error, "Critical program failure: {0}", ex ?? new Exception("Unknown Exception!"));
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

		private bool consoleOutput;
		private bool writeLog;
		private MainBotData mainBotData;
		private ICommandFilter<BotCommand> commandDict;
		private BotCommand[] allCommands;

		private StreamWriter logStream;

		internal AudioFramework AudioFramework { get; private set; }
		internal BobController BobController { get; private set; }
		internal IQueryConnection QueryConnection { get; private set; }
		internal SessionManager SessionManager { get; private set; }
		internal HistoryManager HistoryManager { get; private set; }
		internal ResourceFactoryManager FactoryManager { get; private set; }

		public bool QuizMode { get; set; }

		public MainBot()
		{
			consoleOutput = false;
			writeLog = false;
			commandDict = new Trie<BotCommand>();
		}

		private bool ReadParameter(string[] args)
		{
			HashSet<string> launchParameter = new HashSet<string>();
			foreach (string parameter in args)
				launchParameter.Add(parameter);
			if (launchParameter.Contains("--help") || launchParameter.Contains("-h"))
			{
				Console.WriteLine(" --Silent -S      Deactivates all output to stdout.");
				Console.WriteLine(" --NoLog -L       Deactivates writing to the logfile.");
				Console.WriteLine(" --help -h        Prints this help....");
				return true;
			}
			consoleOutput = !(launchParameter.Contains("--Silent") || launchParameter.Contains("-S"));
			writeLog = !(launchParameter.Contains("--NoLog") || launchParameter.Contains("-L"));
			return false;
		}

		private void InitializeBot()
		{
			// Read Config File
			const string configFilePath = "configTS3AudioBot.cfg";
			ConfigFile cfgFile = ConfigFile.Open(configFilePath) ?? ConfigFile.Create(configFilePath) ?? ConfigFile.GetDummy();
			AudioFrameworkData afd = cfgFile.GetDataStruct<AudioFrameworkData>(typeof(AudioFramework), true);
			BobControllerData bcd = cfgFile.GetDataStruct<BobControllerData>(typeof(BobController), true);
			QueryConnectionData qcd = cfgFile.GetDataStruct<QueryConnectionData>(typeof(QueryConnection), true);
			HistoryManagerData hmd = cfgFile.GetDataStruct<HistoryManagerData>(typeof(HistoryManager), true);
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
						lock (logStream) try
							{
								logStream.Write(msg);
								logStream.Flush();
							}
							catch (IOException) { }
				});
			}

			Log.Write(Log.Level.Info, "[============ TS3AudioBot started =============]");
			string dateStr = DateTime.Now.ToLongDateString();
			Log.Write(Log.Level.Info, "[=== Date: {0}{1} ===]", new string(' ', Math.Max(0, 32 - dateStr.Length)), dateStr);
			string timeStr = DateTime.Now.ToLongTimeString();
			Log.Write(Log.Level.Info, "[=== Time: {0}{1} ===]", new string(' ', Math.Max(0, 32 - timeStr.Length)), timeStr);
			Log.Write(Log.Level.Info, "[==============================================]");

			// Initialize Modules
			QueryConnection = new QueryConnection(qcd);
			BobController = new BobController(bcd, QueryConnection);
			// old: new VLCConnection(afd.vlcLocation);
			// new: BobController
			AudioFramework = new AudioFramework(afd, BobController);
			SessionManager = new SessionManager();
			HistoryManager = new HistoryManager(hmd);

			FactoryManager = new ResourceFactoryManager(AudioFramework);
			FactoryManager.DefaultFactorty = new MediaFactory();
			FactoryManager.AddFactory(new YoutubeFactory());
			FactoryManager.AddFactory(new SoundcloudFactory());

			// Register callbacks
			AudioFramework.OnResourceStarted += HistoryManager.LogAudioResource;
			AudioFramework.OnResourceStarted += BobController.OnResourceStarted;
			AudioFramework.OnResourceStopped += BobController.OnResourceStopped;

			// register callback for all messages happeing
			QueryConnection.OnMessageReceived += TextCallback;
			// register callback to remove open private sessions, when user disconnects
			QueryConnection.OnClientDisconnect += (s, e) => SessionManager.RemoveSession(e.InvokerId);
			// create a default session for all users in all chat
			SessionManager.DefaultSession = new PublicSession(this);
			// connect the query after everyting is set up
			QueryConnection.Connect();
		}

		private void InitializeCommands()
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
			builder.New("getuserid").Action(CommandGetUserId).Permission(CommandRights.Admin).HelpData("Gets the unique Id of a user.", "<username>").Finish();
			builder.New("help").Action(CommandHelp).Permission(CommandRights.Private).HelpData("Shows all commands or detailed help about a specific command.", "[<command>]").Finish();
			builder.New("history").Action(CommandHistory).Permission(CommandRights.Private).HelpData("Shows recently played songs.").Finish();
			builder.New("kickme").Action(CommandKickme).Permission(CommandRights.Private).HelpData("Guess what?", "[far]").Finish();
			builder.New("link").Action(CommandLink).Permission(CommandRights.Private).HelpData("Gets a link to the origin of the current song.", "<link>").Finish();
			builder.New("loop").Action(CommandLoop).Permission(CommandRights.Private).HelpData("Sets whether or not to loop the entire playlist.", "(on|off)").Finish();
			builder.New("next").Action(CommandNext).Permission(CommandRights.Private).HelpData("Plays the next song in the playlist.").Finish();
			builder.New("pm").Action(CommandPM).Permission(CommandRights.Public).HelpData("Requests a private session with the ServerBot so you can invoke private commands.").Finish();
			builder.New("play").Action(CommandPlay).Permission(CommandRights.Private)
				.HelpData("Automatically tries to decide whether the link is a special resource (like youtube) or a direct resource (like ./hello.mp3) and starts it", "<link>").Finish();
			builder.New("previous").Action(CommandPrevious).Permission(CommandRights.Private).HelpData("Plays the previous song in the playlist.").Finish();
			builder.New("quit").Action(CommandQuit).Permission(CommandRights.Admin).HelpData("Closes the TS3AudioBot application.").Finish();
			builder.New("quiz").Action(CommandQuiz).Permission(CommandRights.Public).HelpData("Enable to hide the songnames and let your friends guess the title.", "(on|off)").Finish();
			builder.New("repeat").Action(CommandRepeat).Permission(CommandRights.Private).HelpData("Sets whether or not to loop a single song.", "(on|off)").Finish();
			builder.New("rng").Action(CommandRng).Permission(CommandRights.AnyVisibility).HelpData("Gets a random number.", "(_|<max>|<min> <max>)").Finish();
			builder.New("seek").Action(CommandSeek).Permission(CommandRights.Private).HelpData("Jumps to a timemark within the current song.", "(<time in seconds>|<seconds>:<minutes>)").Finish();
			builder.New("song").Action(CommandSong).Permission(CommandRights.AnyVisibility).HelpData("Tells you the name of the current song.").Finish();
			builder.New("soundcloud").Action(CommandSoundcloud).Permission(CommandRights.Private).HelpData("Resolves the link as a soundcloud song to play it for you.").Finish();
			builder.New("subscribe").Action(CommandSubscribe).Permission(CommandRights.Private).HelpData("Lets you hear the music independent from the channel you are in.").Finish();
			builder.New("stop").Action(CommandStop).Permission(CommandRights.Private).HelpData("Stops the current song.").Finish();
			builder.New("test").Action(CommandTest).Permission(CommandRights.Admin).HelpData("Only for debugging purposes").Finish();
			builder.New("unsubscribe").Action(CommandUnsubscribe).Permission(CommandRights.Private).HelpData("Only lets you hear the music in active channels again.").Finish();
			builder.New("volume").Action(CommandVolume).Permission(CommandRights.AnyVisibility).HelpData("Sets the volume level of the music.", "<level(0-100)>").Finish();
			builder.New("youtube").Action(CommandYoutube).Permission(CommandRights.Private).HelpData("Resolves the link as a youtube video to play it for you.").Finish();

			allCommands = allCommandsList.ToArray();
		}

		private void Run()
		{
			var qc = (QueryConnection)QueryConnection;
			qc.tsClient.EventDispatcher.EnterEventLoop();
		}

		private string ReadConsole()
		{
			try { return Console.ReadLine(); }
			catch (IOException) { return null; }
		}

		private void TextCallback(object sender, TextMessage textMessage)
		{
			Log.Write(Log.Level.Debug, "MB Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			if (!textMessage.Message.StartsWith("!"))
				return;
			BobController.HasUpdate();

			QueryConnection.RefreshClientBuffer(true);

			BotSession session = SessionManager.GetSession(textMessage.Target, textMessage.InvokerId);
			if (textMessage.Target == MessageTarget.Private && session == SessionManager.DefaultSession)
			{
				Log.Write(Log.Level.Debug, "MB User {0} created auto-private session with the bot", textMessage.InvokerName);
				session = SessionManager.CreateSession(this, textMessage.InvokerId);
			}

			var isAdmin = new Lazy<bool>(() => HasInvokerAdminRights(textMessage));

			// check if the user has an open request
			if (session.ResponseProcessor != null)
			{
				if (session.ResponseProcessor(session, textMessage, session.AdminResponse && isAdmin.Value))
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
				allowed = textMessage.Target == MessageTarget.Server;
				break;
			case CommandRights.Private:
				reason = "Command must be used in a private session!";
				allowed = textMessage.Target == MessageTarget.Private;
				break;
			case CommandRights.AnyVisibility:
				allowed = true;
				break;
			}
			if (!allowed && !isAdmin.Value)
			{
				session.Write(reason);
				return;
			}

			InvokeCommand(command, session, textMessage, commandSplit);
		}

		private void ParseCommandRequest(string request)
		{
			for (int strPtr = 0; strPtr < request.Length; strPtr++)
			{

			}
		}

		private void InvokeCommand(BotCommand command, BotSession session, TextMessage textMessage, string[] commandSplit)
		{
			try
			{
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
					Log.Write(Log.Level.Error, "Command with no process: " + command);
					break;
				}
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Error, "Critical command error: " + ex.Message);
			}
		}

		private bool HasInvokerAdminRights(TextMessage textMessage)
		{
			Log.Write(Log.Level.Debug, "AdminCheck called!");
			ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
			if (client == null)
				return false;
			int[] clientSgIds = QueryConnection.GetClientServerGroups(client);
			return clientSgIds.Contains(mainBotData.adminGroupId);
		}

		// COMMANDS

		private void CommandAdd(BotSession session, TextMessage textMessage, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
			FactoryManager.LoadAndPlay(new PlayData(session, client, parameter, true));
		}

		private void CommandClear(BotSession session)
		{
			AudioFramework.Clear();
		}

		private void CommandGetUserId(BotSession session, string parameter)
		{
			ClientData client = QueryConnection.GetClientByName(parameter);
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

		private void CommandHistory(BotSession session, TextMessage textMessage, string parameter)
		{
			var args = parameter.SplitNoEmpty(' ');
			if (args.Length == 0)
				return; // OPtionally print help
			else if (args.Length >= 1)
			{
				#region switch local variables
				uint id;
				int amount;
				DateTime tillTime;
				#endregion

				switch (args[0])
				{
				case "from": // <user> <last x>
					break;

				case "help":
					break;

				#region id
				case "id": // [id]
					if (args.Length >= 2 && uint.TryParse(args[1], out id))
					{
						var ale = HistoryManager.GetEntryById(id);
						if (ale != null)
						{
							string resultStr = HistoryManager.Formatter.ProcessQuery(ale);
							session.Write(resultStr);
						}
						else
						{
							session.Write("Could not find track with this id");
						}
					}
					else
					{
						session.Write("Missing or invalid track Id.");
					}
					break;
				#endregion

				#region last
				case "last": // [(x entries:] -> default to 1
					if (args.Length >= 2 && int.TryParse(args[1], out amount))
					{
						var query = new SeachQuery { MaxResults = amount };
						string resultStr = HistoryManager.SearchParsed(query);
						session.Write(resultStr);
					}
					else
					{
						var ale = HistoryManager.Search(new SeachQuery { MaxResults = 1 }).FirstOrDefault();
						if (ale != null)
						{
							ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
							FactoryManager.RestoreAndPlay(ale, new PlayData(session, client, null, false));
						}
					}
					break;
				#endregion

				#region play
				case "play": // [id]
					if (args.Length >= 2 && uint.TryParse(args[1], out id))
					{
						var ale = HistoryManager.GetEntryById(id);
						if (ale != null)
						{
							ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
							FactoryManager.RestoreAndPlay(ale, new PlayData(session, client, null, false));
						}
						else
						{
							session.Write("Could not find track with this id");
						}
					}
					else
					{
						session.Write("Missing or invalid track Id.");
					}
					break;
				#endregion

				#region till
				case "till": // [time]
					if (args.Length >= 2)
					{
						switch (args[1].ToLower())
						{
						case "hour": tillTime = DateTime.Now.AddHours(-1); break;
						case "today": tillTime = DateTime.Today; break;
						case "yesterday": tillTime = DateTime.Today.AddDays(-1); break;
						case "week": tillTime = DateTime.Today.AddDays(-7); break;
						default:
							string timeStr = string.Join(" ", args, 1, args.Length - 1);
							if (!DateTime.TryParse(timeStr, out tillTime))
								tillTime = DateTime.MinValue;
							break;
						}
						if (tillTime != DateTime.MinValue)
						{
							var query = new SeachQuery { LastInvokedAfter = tillTime };
							string resultStr = HistoryManager.SearchParsed(query);
							session.Write(resultStr);
						}
						else
						{
							session.Write("The date could not be parsed.");
						}
					}
					else
					{
						session.Write("Missing time or date.");
					}
					break;
				#endregion

				#region title
				case "title": // substr
					if (args.Length >= 2)
					{
						int startSubstr = parameter.IndexOf("title");
						string titleStr = parameter.Substring(startSubstr + 5).Trim(); // len of title + space
						var query = new SeachQuery { TitlePart = titleStr };
						string resultStr = HistoryManager.SearchParsed(query);
						session.Write(resultStr);
					}
					else
					{
						session.Write("Missing title to search.");
					}
					break;
				#endregion

				case "where":
					break;

				default:
					break;
				}
			}
		}

		private void CommandKickme(BotSession session, TextMessage textMessage, string parameter)
		{
			try
			{
				if (string.IsNullOrEmpty(parameter))
					QueryConnection.KickClientFromChannel(textMessage.InvokerId);
				else if (parameter == "far")
					QueryConnection.KickClientFromServer(textMessage.InvokerId);
			}
			catch (QueryCommandException ex)
			{
				Log.Write(Log.Level.Info, "Could not kick: {0}", ex);
			}
		}

		private void CommandLink(BotSession session, TextMessage textMessage, string parameter)
		{
			if (AudioFramework.CurrentPlayData == null)
			{
				session.Write("There is nothing on right now...");
				return;
			}

			if (QuizMode && AudioFramework.CurrentPlayData.Invoker.Id != textMessage.InvokerId)
				session.Write("Sorry, you have to guess!");
			else
				session.Write(AudioFramework.CurrentPlayData.Resource.ResourceTitle);
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

		private void CommandMedia(BotSession session, TextMessage textMessage, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
			FactoryManager.LoadAndPlay(AudioType.MediaLink, new PlayData(session, client, parameter, false));
		}

		private void CommandNext(BotSession session)
		{
			AudioFramework.Next();
		}

		private void CommandPM(BotSession session, TextMessage textMessage)
		{
			BotSession ownSession = SessionManager.CreateSession(this, textMessage.InvokerId);
			ownSession.Write("Hi " + textMessage.InvokerName);
		}

		private void CommandPlay(BotSession session, TextMessage textMessage, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				AudioFramework.Pause = false;
			else
			{
				ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
				FactoryManager.LoadAndPlay(new PlayData(session, client, parameter, false));
			}
		}

		private void CommandPrevious(BotSession session)
		{
			AudioFramework.Previous();
		}

		private void CommandQuit(BotSession session)
		{
			session.Write("Goodbye!");
			Dispose();
			Log.Write(Log.Level.Info, "Exiting...");
		}

		private void CommandQuiz(BotSession session, string parameter)
		{
			if (session.IsPrivate)
			{
				session.Write("No cheatig! Everybody has to see it!");
				return;
			}

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

		private void CommandRng(BotSession session, string parameter)
		{
			var rngArgs = parameter.SplitNoEmpty(' ');
			int first, second;
			if (rngArgs.Length <= 0)
				session.Write(new Random().Next().ToString());
			else if (int.TryParse(rngArgs[0], out first) && rngArgs.Length == 1)
				session.Write(new Random().Next(first).ToString());
			else if (int.TryParse(rngArgs[1], out second) && first <= second)
				session.Write(new Random().Next(first, second).ToString());
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
			if (AudioFramework.CurrentPlayData == null)
			{
				session.Write("There is nothing on right now...");
				return;
			}

			if (QuizMode && AudioFramework.CurrentPlayData.Invoker.Id != textMessage.InvokerId)
				session.Write("Sorry, you have to guess!");
			else
				session.Write(AudioFramework.CurrentPlayData.Resource.ResourceTitle);
		}

		private void CommandSoundcloud(BotSession session, TextMessage textMessage, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
			FactoryManager.LoadAndPlay(AudioType.Soundcloud, new PlayData(session, client, parameter, false));
		}

		private void CommandStop(BotSession session)
		{
			AudioFramework.Stop();
		}

		private void CommandSubscribe(BotSession session, TextMessage textMessage)
		{
			BobController.WhisperClientSubscribe(textMessage.InvokerId);
		}

		private void CommandTest(BotSession session)
		{
			PrivateSession ps = session as PrivateSession;
			if (ps == null)
			{
				session.Write("Please use as private, admins too!");
				return;
			}
			else
			{
				ps.Write("Good boy!");
				// stresstest
				for (int i = 0; i < 10; i++)
					session.Write(i.ToString());
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
				if (volume <= AudioFramework.MaxUserVolume || volume < AudioFramework.Volume)
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

		private void CommandYoutube(BotSession session, TextMessage textMessage, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
			FactoryManager.LoadAndPlay(AudioType.Youtube, new PlayData(session, client, parameter, false));
		}

		// RESPONSES

		private bool ResponseVolume(BotSession session, TextMessage tm, bool isAdmin)
		{
			Answer answer = TextUtil.GetAnswer(tm.Message);
			if (answer == Answer.Yes)
			{
				if (isAdmin)
				{
					if (!(session.ResponseData is int))
					{
						Log.Write(Log.Level.Error, "responseData is not an int.");
						return true;
					}
					AudioFramework.Volume = (int)session.ResponseData;
				}
				else
				{
					session.Write("Command can only be answered by an admin.");
				}
			}
			return answer != Answer.Unknown;
		}

		public void Dispose()
		{
			TickPool.Close();
			if (FactoryManager != null)
			{
				FactoryManager.Dispose();
				FactoryManager = null;
			}
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
			if (SessionManager != null)
			{
				//sessionManager.Dispose();
				SessionManager = null;
			}
			if (QueryConnection != null)
			{
				QueryConnection.Dispose();
				QueryConnection = null;
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
		public ClientData Invoker { get; private set; }
		public string Message { get; private set; }
		public bool Enqueue { get; private set; }
		public int Volume { get; private set; }
		public AudioResource Resource { get; set; }

		public PlayData(BotSession session, ClientData invoker, string message, bool enqueue)
		{
			Session = session;
			Invoker = invoker;
			Message = message;
			Enqueue = enqueue;
			Resource = null;
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

#pragma warning disable CS0649
	struct MainBotData
	{
		[Info("path to the logfile", "log_ts3audiobot")]
		public string logFile;
		[Info("group able to execute admin commands from the bot")]
		public int adminGroupId;
	}
#pragma warning restore CS0649
}
