namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;

	using TS3AudioBot.Algorithm;
	using TS3AudioBot.Helper;
	using TS3AudioBot.History;
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
		private bool writeLogStack;
		private MainBotData mainBotData;
		private ICommandFilter<BotCommand> commandDict;
		private BotCommand[] allCommands;

		private StreamWriter logStream;

		public AudioFramework AudioFramework { get; private set; }
		public BobController BobController { get; private set; }
		public IQueryConnection QueryConnection { get; private set; }
		public SessionManager SessionManager { get; private set; }
		internal HistoryManager HistoryManager { get; private set; }
		public ResourceFactoryManager FactoryManager { get; private set; }

		public bool QuizMode { get; set; }

		public MainBot()
		{
			consoleOutput = false;
			writeLog = false;
			commandDict = new XCommandFilter<BotCommand>();
		}

		private bool ReadParameter(string[] args)
		{
			HashSet<string> launchParameter = new HashSet<string>();
			foreach (string parameter in args)
				launchParameter.Add(parameter);
			if (launchParameter.Contains("--help") || launchParameter.Contains("-h"))
			{
				Console.WriteLine(" --Quiet -q       Deactivates all output to stdout.");
				Console.WriteLine(" --NoLog -L       Deactivates writing to the logfile.");
				Console.WriteLine(" --Stack -s       Adds the stacktrace to all log writes.");
				Console.WriteLine(" --help -h        Prints this help....");
				return true;
			}
			consoleOutput = !(launchParameter.Contains("--Quiet") || launchParameter.Contains("-q"));
			writeLog = !(launchParameter.Contains("--NoLog") || launchParameter.Contains("-L"));
			writeLogStack = (launchParameter.Contains("--Stack") || launchParameter.Contains("-s"));
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
				Log.RegisterLogger("[%T]%L: %M\n" + (writeLogStack ? "%S\n" : ""), "", (msg) =>
				{
					if (logStream != null)
						try
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

			Log.Write(Log.Level.Info, "[============ Initializing Modules ============]");
			QueryConnection = new QueryConnection(qcd);
			BobController = new BobController(bcd, QueryConnection);
			// old: new VLCConnection(afd.vlcLocation);
			// new: BobController
			AudioFramework = new AudioFramework(afd, BobController);
			SessionManager = new SessionManager();
			HistoryManager = new HistoryManager(hmd);

			Log.Write(Log.Level.Info, "[=========== Initializing Factories ===========]");
			FactoryManager = new ResourceFactoryManager(AudioFramework);
			FactoryManager.DefaultFactorty = new MediaFactory();
			FactoryManager.AddFactory(new YoutubeFactory());
			FactoryManager.AddFactory(new SoundcloudFactory());
			FactoryManager.AddFactory(new TwitchFactory());

			Log.Write(Log.Level.Info, "[=========== Registering callbacks ============]");
			// Inform our HistoryManager when a new resource started successfully
			AudioFramework.OnResourceStarted += HistoryManager.LogAudioResource;
			// Inform the BobClient on start/stop
			AudioFramework.OnResourceStarted += BobController.OnResourceStarted;
			AudioFramework.OnResourceStopped += BobController.OnResourceStopped;
			// Register callback for all messages happeing
			QueryConnection.OnMessageReceived += TextCallback;
			// Register callback to remove open private sessions, when user disconnects
			QueryConnection.OnClientDisconnect += (s, e) => SessionManager.RemoveSession(e.InvokerId);

			Log.Write(Log.Level.Info, "[================= Finalizing =================]");
			// Create a default session for all users in all chat
			SessionManager.DefaultSession = new PublicSession(this);
			// Connect the query after everyting is set up
			QueryConnection.Connect();
			Log.Write(Log.Level.Info, "[============== Connected & Done ==============]");
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

			builder.New("add").Action(CommandAdd).Permission(CommandRights.Private)
				.HelpData("Adds a new song to the queue.")
				.Parameter("<link>", "Any link that is also recognized by !play").Finish();
			builder.New("clear").Action(CommandClear).Permission(CommandRights.Private)
				.HelpData("Removes all songs from the current playlist.").Finish();
			builder.New("getuserid").Action(CommandGetUserId).Permission(CommandRights.Admin)
				.HelpData("Gets the unique Id of a user.")
				.Parameter("<username>", "A user which is currently logged in to the server").Finish();
			builder.New("help").Action(CommandHelp).Permission(CommandRights.Private)
				.HelpData("Shows all commands or detailed help about a specific command.")
				.Parameter("[<command>]", "Any currently accepted command").Finish();
			builder.New("history").Action(CommandHistory).Permission(CommandRights.Private)
				.HelpData("Shows recently played songs.")
				.Parameter("from <user-dbid> <count>", "Gets the last <count> songs from the user with the given <user-dbid>")
				.Parameter("help", "You know...")
				.Parameter("id <id>", "Displays all saved informations about the song with <id>")
				.Parameter("id (last|next)", "Gets the highest|next song id")
				.Parameter("last", "Plays the last song again")
				.Parameter("last <count>", "Gets the last <count> played songs.")
				.Parameter("play <id>", "Playes the song with <id>")
				.Parameter("till <time>", "Gets all songs plyed until <time>. Special options are: (hour|today|yesterday|week)")
				.Parameter("title <string>", "Gets all songs which title contains <string>").Finish();
			builder.New("kickme").Action(CommandKickme).Permission(CommandRights.Private)
				.HelpData("Guess what?")
				.Parameter("[far]", "Optional attribute for the extra punch stenght").Finish();
			builder.New("link").Action(CommandLink).Permission(CommandRights.Private)
				.HelpData("Gets a link to the origin of the current song.").Finish();
			builder.New("loop").Action(CommandLoop).Permission(CommandRights.Private)
				.HelpData("Sets whether or not to loop the entire playlist.")
				.Parameter("(on|off)", "on or off").Finish();
			builder.New("next").Action(CommandNext).Permission(CommandRights.Private)
				.HelpData("Plays the next song in the playlist.").Finish();
			builder.New("pm").Action(CommandPM).Permission(CommandRights.Public)
				.HelpData("Requests a private session with the ServerBot so you can invoke private commands.").Finish();
			builder.New("parse").Action(CommandParse).Permission(CommandRights.Admin)
				.HelpData("Displays the AST of the requested command.")
				.Parameter("<command>", "The comand to be parsed").Finish();
			builder.New("pause").Action(CommandPause).Permission(CommandRights.Private)
				.HelpData("Well, pauses the song. Undo with !play").Finish();
			builder.New("play").Action(CommandPlay).Permission(CommandRights.Private)
				.HelpData("Automatically tries to decide whether the link is a special resource (like youtube) or a direct resource (like ./hello.mp3) and starts it")
				.Parameter("<link>", "Youtube, Soundcloud, local path or file link").Finish();
			builder.New("previous").Action(CommandPrevious).Permission(CommandRights.Private)
				.HelpData("Plays the previous song in the playlist.").Finish();
			builder.New("quit").Action(CommandQuit).Permission(CommandRights.Admin)
				.HelpData("Closes the TS3AudioBot application.").Finish();
			builder.New("quiz").Action(CommandQuiz).Permission(CommandRights.Public)
				.HelpData("Enable to hide the songnames and let your friends guess the title.")
				.Parameter("(on|off)", "on or off").Finish();
			builder.New("repeat").Action(CommandRepeat).Permission(CommandRights.Private)
				.HelpData("Sets whether or not to loop a single song.")
				.Parameter("(on|off)", "on or off").Finish();
			builder.New("rng").Action(CommandRng).Permission(CommandRights.AnyVisibility)
				.HelpData("Gets a random number.")
				.Parameter(string.Empty, "Gets a number between 0 and " + int.MaxValue)
				.Parameter("<max>", "Gets a number between 0 and <max>")
				.Parameter("<min> <max>", "Gets a number between <min> and <max>").Finish();
			builder.New("seek").Action(CommandSeek).Permission(CommandRights.Private)
				.HelpData("Jumps to a timemark within the current song.")
				.Parameter("<sec>", "Time in seconds")
				.Parameter("<min:sec>", "Time in Minutes:Seconds").Finish();
			builder.New("song").Action(CommandSong).Permission(CommandRights.AnyVisibility)
				.HelpData("Tells you the name of the current song.").Finish();
			builder.New("soundcloud").Action(CommandSoundcloud).Permission(CommandRights.Private)
				.HelpData("Resolves the link as a soundcloud song to play it for you.").Finish();
			builder.New("subscribe").Action(CommandSubscribe).Permission(CommandRights.Private)
				.HelpData("Lets you hear the music independent from the channel you are in.").Finish();
			builder.New("stop").Action(CommandStop).Permission(CommandRights.Private)
				.HelpData("Stops the current song.").Finish();
			builder.New("test").Action(CommandTest).Permission(CommandRights.Admin)
				.HelpData("Only for debugging purposes").Finish();
			builder.New("twitch").Action(CommandSoundcloud).Permission(CommandRights.Private)
				.HelpData("Resolves the link as a twitch stream to play it for you.").Finish();
			builder.New("unsubscribe").Action(CommandUnsubscribe).Permission(CommandRights.Private)
				.HelpData("Only lets you hear the music in active channels again.").Finish();
			builder.New("volume").Action(CommandVolume).Permission(CommandRights.AnyVisibility)
				.HelpData("Sets the volume level of the music.")
				.Parameter("<level>", "A new volume level between 0 and " + AudioFramework.MaxVolume).Finish();
			builder.New("youtube").Action(CommandYoutube).Permission(CommandRights.Private)
				.HelpData("Resolves the link as a youtube video to play it for you.").Finish();

			allCommands = allCommandsList.ToArray();
		}

		private void Run()
		{
			Thread.CurrentThread.Name = "Main/Eventloop";
			var qc = (QueryConnection)QueryConnection;
			qc.tsClient.EventDispatcher.EnterEventLoop();
		}

		private void TextCallback(object sender, TextMessage textMessage)
		{
			Log.Write(Log.Level.Debug, "MB Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			textMessage.Message = textMessage.Message.TrimStart(new[] { ' ' });
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

			ASTNode parsedAst = CommandParser.ParseCommandRequest(textMessage.Message);
			if (parsedAst.Type == NodeType.Error)
			{
				StringBuilder strb = new StringBuilder();
				strb.AppendLine();
				parsedAst.Write(strb, 0);
				session.Write(strb.ToString());
			}
			else if (parsedAst.Type == NodeType.Command)
			{
				Validate(parsedAst, textMessage.Message);
				CallCommandTree(parsedAst, session);
			}
			else
			{
				Log.Write(Log.Level.Error, "MB Parse error with: {0}", parsedAst);
				session.Write("Internal command parsing error!");
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

		private bool Validate(ASTNode astnode, string fullRequest)
		{
			switch (astnode.Type)
			{
			case NodeType.Command:
				ASTCommand com = (ASTCommand)astnode;
				BotCommand foundCom;
				if (commandDict.TryGetValue(com.Command, out foundCom))
				{
					com.BotCommand = foundCom;
				}
				else
				{
					new ParseError(fullRequest, com, "Unknown command!");
				}
				break;

			case NodeType.Value:
				break;
			case NodeType.Error: return false;
			default: return false;
			}
		}

		private void LazyExecute(ASTNode astnode, BotSession session)
		{

		}

		private static void InvokeCommand(BotCommand command, BotSession session, TextMessage textMessage, string[] commandSplit)
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
			catch (TimeoutException tex)
			{
				Log.Write(Log.Level.Error, "Critical timeout error ({0})", tex.StackTrace);
				session.Write("Internal timout error, please try again.");
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Error, "Critical command error: {0} ({1})", ex.Message, ex.StackTrace);
				session.Write("Internal command error, please try again.");
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

		#region COMMANDS

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
				session.Write($"Client: UID:{client.Id} DBID:{client.DatabaseId} ChanID:{client.ChannelId}");
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
			if (args.Length == 0) args = new[] { "help" };

			else if (args.Length >= 1)
			{
				#region switch local variables
				uint id;
				int amount;
				DateTime tillTime;
				#endregion

				switch (args[0])
				{
				#region from
				case "from": // <user_dbid> <last x>
					if (args.Length >= 1 && uint.TryParse(args[1], out id))
					{
						SeachQuery query = new SeachQuery();
						query.UserId = id;

						if (args.Length >= 2 && int.TryParse(args[1], out amount))
							query.MaxResults = amount;

						string resultStr = HistoryManager.SearchParsed(query);
						session.Write(resultStr);
					}
					else
					{
						session.Write("Missing or invalid user DbId.");
					}
					break;
				#endregion

				#region help
				case "help":
				default:
					CommandHelp(session, "history");
					break;
				#endregion

				#region id
				case "id": // [id]
					bool arrLen2 = args.Length >= 2;
					if (arrLen2 && uint.TryParse(args[1], out id))
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
					else if (arrLen2 && args[1] == "last")
					{
						session.Write($"{HistoryManager.HighestId} is the currently highest song id.");
					}
					else if (arrLen2 && args[1] == "next")
					{
						session.Write($"{HistoryManager.HighestId + 1} will be the next song id.");
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

		private void CommandLink(BotSession session, TextMessage textMessage)
		{
			if (AudioFramework.CurrentPlayData == null)
			{
				session.Write("There is nothing on right now...");
				return;
			}

			if (QuizMode && AudioFramework.CurrentPlayData.Invoker.Id != textMessage.InvokerId)
				session.Write("Sorry, you have to guess!");
			else
				session.Write(FactoryManager.RestoreLink(AudioFramework.CurrentPlayData));
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

		private void CommandParse(BotSession session, string parameter)
		{
			if (!parameter.TrimStart().StartsWith("!"))
			{
				session.Write("This is not a command");
				return;
			}
			try
			{
				var node = CommandParser.ParseCommandRequest(parameter);
				StringBuilder strb = new StringBuilder();
				strb.AppendLine();
				node.Write(strb, 0);
				session.Write(strb.ToString());
			}
			catch
			{
				session.Write("GJ - You crashed it!!!");
			}
		}

		private void CommandPause(BotSession session, TextMessage textMessage)
		{
			AudioFramework.Pause = true;
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

		private void CommandTwitch(BotSession session, TextMessage textMessage, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
			FactoryManager.LoadAndPlay(AudioType.Twitch, new PlayData(session, client, parameter, false));
		}

		private void CommandUnsubscribe(BotSession session, TextMessage textMessage)
		{
			BobController.WhisperClientUnsubscribe(textMessage.InvokerId);
		}

		private void CommandVolume(BotSession session, string parameter)
		{
			bool relPos = parameter.StartsWith("+");
			bool relNeg = parameter.StartsWith("-");
			string numberString = (relPos || relNeg) ? parameter.Remove(0, 1) : parameter;

			int volume;
			if (!int.TryParse(numberString, out volume))
			{
				CommandHelp(session, "volume");
				return;
			}

			int newVolume;
			if (relPos) newVolume = AudioFramework.Volume + volume;
			else if (relNeg) newVolume = AudioFramework.Volume - volume;
			else newVolume = volume;

			if (newVolume < 0 || newVolume > AudioFramework.MaxVolume)
			{
				session.Write("The volume level must be between 0 and " + AudioFramework.MaxVolume);
			}
			if (newVolume <= AudioFramework.MaxUserVolume || newVolume < AudioFramework.Volume)
			{
				AudioFramework.Volume = newVolume;
			}
			else if (newVolume <= AudioFramework.MaxVolume)
			{
				session.Write("Careful you are requesting a very high volume! Do you want to apply this? !(yes|no)");
				session.SetResponse(ResponseVolume, newVolume, true);
			}
		}

		private void CommandYoutube(BotSession session, TextMessage textMessage, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(textMessage.InvokerId);
			FactoryManager.LoadAndPlay(AudioType.Youtube, new PlayData(session, client, parameter, false));
		}

		#endregion

		#region RESPONSES

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

		#endregion

		public void Dispose()
		{
			TickPool.Close();
			if (HistoryManager != null)
			{
				HistoryManager.Dispose();
				HistoryManager = null;
			}
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

	class CommandExecuteNode
	{
		public ASTCommand commandNode;
		public BotCommand botCommand;
		public string parameter;
		public bool Executed = false;
		public string Value;
	}

	class LoopSession : BotSession
	{
		public MemoryStream memStream;
		public StreamWriter writer;
		public BotCommand parentCommand;

		public LoopSession(MainBot bot) : base(bot)
		{
			memStream = new MemoryStream();
			writer = new StreamWriter(memStream);
		}

		public override bool IsPrivate => parentCommand.CommandRights == CommandRights.Private;
		public override void Write(string message) => writer.Write(message);
		public void Clear() => memStream.SetLength(0);
	}

	public class PlayData
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
			Volume = -1;
		}
	}

	public class BotCommand
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
		public Tuple<string, string>[] ParameterList { get; private set; }

		private BotCommand() { }

		public string GetHelp()
		{
			if (outputCache == null)
			{
				StringBuilder strb = new StringBuilder();
				strb.Append("\n!").Append(InvokeName).Append(": ").Append(Description);
				if (ParameterList.Length > 0)
				{
					int longest = ParameterList.Max(p => p.Item1.Length) + 1;
					foreach (var para in ParameterList)
						strb.Append("\n!").Append(InvokeName).Append(" ").Append(para.Item1)
							.Append(' ', longest - para.Item1.Length).Append(para.Item2);
				}
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
			private List<Tuple<string, string>> parameters = new List<Tuple<string, string>>();

			private Builder(Action<BotCommand> finishAction, bool buildMode)
			{
				this.buildMode = buildMode;
				this.registerAction = finishAction;
			}

			public Builder(Action<BotCommand> finishAction) : this(finishAction, false) { }

			public Builder New(string invokeName)
			{
				if (buildMode)
					throw new InvalidOperationException("A new command cannot be created when the old one isn't finished.");
				var builder = new Builder(registerAction, true);
				builder.name = invokeName;
				return builder;
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

			public Builder HelpData(string description)
			{
				if (setHelp) throw new InvalidOperationException();
				this.description = description;
				setHelp = true;
				return this;
			}

			public Builder Parameter(string option, string help)
			{
				parameters.Add(new Tuple<string, string>(option, help));
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
					ParameterList = parameters.ToArray(),
				};

				if (registerAction != null)
					registerAction(command);

				buildMode = false;
				return command;
			}
		}
	}

	public enum CommandParameter
	{
		Undefined,
		Nothing,
		Remainder,
		TextMessage,
		MessageAndRemainder
	}

	public enum CommandRights
	{
		Admin,
		Public,
		Private,
		AnyVisibility,
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
