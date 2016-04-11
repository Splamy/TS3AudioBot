namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;

	using CommandSystem;
	using Helper;
	using History;
	using ResourceFactories;
	using static CommandRights;

	using TS3Query;
	using TS3Query.Messages;

	// Todo:
	// - implement history missing features
	public sealed class MainBot : MarshalByRefObject, IDisposable
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

				if (!bot.ReadParameter(args)) return;
				if (!bot.InitializeBot()) return;
				bot.Run();
			}
		}

		private bool consoleOutput;
		private bool writeLog;
		private bool writeLogStack;
		private MainBotData mainBotData;

		private StreamWriter logStream;

		internal PluginManager PluginManager { get; private set; }
		public CommandManager CommandManager { get; private set; }
		public AudioFramework AudioFramework { get; private set; }
		public BobController BobController { get; private set; }
		public QueryConnection QueryConnection { get; private set; }
		public SessionManager SessionManager { get; private set; }
		public HistoryManager HistoryManager { get; private set; }
		public ResourceFactoryManager FactoryManager { get; private set; }

		public bool QuizMode { get; set; }

		public MainBot()
		{
			consoleOutput = false;
			writeLog = false;
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
				return false;
			}
			consoleOutput = !(launchParameter.Contains("--Quiet") || launchParameter.Contains("-q"));
			writeLog = !(launchParameter.Contains("--NoLog") || launchParameter.Contains("-L"));
			writeLogStack = (launchParameter.Contains("--Stack") || launchParameter.Contains("-s"));
			return true;
		}

		private bool InitializeBot()
		{
			// Read Config File
			const string configFilePath = "configTS3AudioBot.cfg";
			ConfigFile cfgFile = ConfigFile.Open(configFilePath) ?? ConfigFile.Create(configFilePath) ?? ConfigFile.GetDummy();
			var afd = cfgFile.GetDataStruct<AudioFrameworkData>(typeof(AudioFramework), true);
			var bcd = cfgFile.GetDataStruct<BobControllerData>(typeof(BobController), true);
			var qcd = cfgFile.GetDataStruct<QueryConnectionData>(typeof(QueryConnection), true);
			var hmd = cfgFile.GetDataStruct<HistoryManagerData>(typeof(HistoryManager), true);
			var pmd = cfgFile.GetDataStruct<PluginManagerData>(typeof(PluginManager), true);
			var pld = cfgFile.GetDataStruct<PlaylistManagerData>(typeof(PlaylistManager), true);
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

			Log.Write(Log.Level.Info, "[============ Initializing Commands ===========]");
			CommandManager = new CommandManager();
			CommandManager.RegisterMain(this);

			Log.Write(Log.Level.Info, "[============ Initializing Modules ============]");
			QueryConnection = new QueryConnection(qcd);
			var playlistManager = new PlaylistManager(pld);
			BobController = new BobController(bcd, QueryConnection);
			// old: new VLCConnection(afd.vlcLocation);
			// new: BobController
			AudioFramework = new AudioFramework(afd, BobController, playlistManager);
			SessionManager = new SessionManager();
			HistoryManager = new HistoryManager(hmd);
			PluginManager = new PluginManager(this, pmd);

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
			// Register callback for all messages happening
			QueryConnection.OnMessageReceived += TextCallback;
			// Register callback to remove open private sessions, when user disconnects
			QueryConnection.OnClientDisconnect += (s, e) => SessionManager.RemoveSession(e.InvokerId);

			Log.Write(Log.Level.Info, "[================= Finalizing =================]");
			// Create a default session for all users in all chat
			SessionManager.DefaultSession = new PublicSession(this);
			// Connect the query after everyting is set up
			try { QueryConnection.Connect(); }
			catch (QueryCommandException qcex)
			{
				Log.Write(Log.Level.Error, "There is either a problem with your connection configuration, or the query has not all permissions it needs. ({0})", qcex);
				return false;
			}

			Log.Write(Log.Level.Info, "[============== Connected & Done ==============]");
			return true;
		}

		private void Run()
		{
			Thread.CurrentThread.Name = "Main/Eventloop";
			var qc = QueryConnection;
			qc.tsClient.EventDispatcher.EnterEventLoop();
		}

		#region COMMAND EXECUTING & CHAINING

		private void TextCallback(object sender, TextMessage textMessage)
		{
			Log.Write(Log.Level.Debug, "MB Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			textMessage.Message = textMessage.Message.TrimStart(new[] { ' ' });
			if (!textMessage.Message.StartsWith("!"))
				return;
			BobController.HasUpdate();

			QueryConnection.RefreshClientBuffer(true);

			// get the current session
			BotSession session = SessionManager.GetSession(textMessage.Target, textMessage.InvokerId);
			if (textMessage.Target == MessageTarget.Private && session == SessionManager.DefaultSession)
			{
				Log.Write(Log.Level.Debug, "MB User {0} created auto-private session with the bot", textMessage.InvokerName);
				try
				{
					session = SessionManager.CreateSession(this, textMessage.InvokerId);
				}
				catch (SessionManagerException smex)
				{
					Log.Write(Log.Level.Error, smex.ToString());
					return;
				}
			}

			var isAdmin = new Lazy<bool>(() => HasInvokerAdminRights(textMessage));

			// check if the user has an open request
			if (session.ResponseProcessor != null)
			{
				if (session.ResponseProcessor(session, textMessage, isAdmin))
				{
					session.ClearResponse();
					return;
				}
			}

			// parse (and execute) the command
			ASTNode parsedAst = CommandParser.ParseCommandRequest(textMessage.Message);
			if (parsedAst.Type == ASTType.Error)
			{
				PrintAstError(session, (ASTError)parsedAst);
			}
			else
			{
				var info = new ExecutionInformation
				{
					Session = session,
					TextMessage = textMessage,
					IsAdmin = isAdmin
				};
				var command = CommandManager.CommandSystem.AstToCommandResult(parsedAst);

				try
				{
					var res = command.Execute(info, Enumerable.Empty<ICommand>(),
						new[] { CommandResultType.String, CommandResultType.Empty });
					if (res.ResultType == CommandResultType.String)
					{
						var sRes = (StringCommandResult)res;
						// Write result to user
						if (!string.IsNullOrEmpty(sRes.Content))
							session.Write(sRes.Content);
					}
				}
				catch (CommandException ex)
				{
					session.Write("Error: " + ex.Message);
				}
				catch (Exception ex)
				{
					session.Write("An unexpected error occured: " + ex.Message);
					Log.Write(Log.Level.Error, "MB Unexpected command error: ", ex);
				}
			}
		}

		private void PrintAstError(BotSession session, ASTError asterror)
		{
			StringBuilder strb = new StringBuilder();
			strb.AppendLine();
			asterror.Write(strb, 0);
			session.Write(strb.ToString());
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

		#endregion

		#region COMMANDS

		// [...] = Optional
		// <name> = Placeholder for a text
		// [text] = Option for fixed text
		// (a|b) = either or switch

		[Command(Private, "add", "Adds a new song to the queue.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		string CommandAdd(ExecutionInformation info, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(info.TextMessage.InvokerId);
			return FactoryManager.LoadAndPlay(new PlayData(info.Session, client, parameter, true));
		}

		[Command(Private, "clear", "Removes all songs from the current playlist.")]
		void CommandClear()
		{
			AudioFramework.Clear();
		}

		[Command(AnyVisibility, "eval", "Executes a given command or string")]
		[Usage("<command> <arguments...>", "Executes the given command on arguments")]
		[Usage("<strings...>", "Concat the strings and execute them with the command system")]
		ICommandResult CommandEval(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			// Evaluate the first argument on the rest of the arguments
			if (!arguments.Any())
				throw new CommandException("Need at least one argument to evaluate");
			var leftArguments = arguments.Skip(1);
			var arg0 = arguments.First().Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.Command, CommandResultType.String });
			if (arg0.ResultType == CommandResultType.Command)
				return ((CommandCommandResult)arg0).Command.Execute(info, leftArguments, returnTypes);

			// We got a string back so parse and evaluate it
			var args = ((StringCommandResult)arg0).Content;

			// Add the rest of the arguments
			args += string.Join(" ", arguments.Select(a =>
				((StringCommandResult)a.Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content));

			var cmd = CommandManager.CommandSystem.AstToCommandResult(CommandParser.ParseCommandRequest(args));
			return cmd.Execute(info, leftArguments, returnTypes);
		}

		[Command(Admin, "getuserid", "Gets the unique Id of a user.")]
		[Usage("<username>", "A user which is currently logged in to the server")]
		string CommandGetUserId(ExecutionInformation info, string parameter)
		{
			ClientData client = QueryConnection.GetClientByName(parameter);
			if (client == null)
				return "No user found...";
			else
				return $"Client: UID:{client.ClientId} DBID:{client.DatabaseId} ChanID:{client.ChannelId}";
		}

		[Command(AnyVisibility, "help", "Shows all commands or detailed help about a specific command.")]
		[Usage("[<command>]", "Any currently accepted command")]
		[RequiredParameters(0)]
		string CommandHelp(ExecutionInformation info, params string[] parameter)
		{
			if (parameter.Length == 0)
			{
				var strb = new StringBuilder();
				strb.Append("\n========= Welcome to the TS3AudioBot ========="
					+ "\nIf you need any help with a special command use !help <commandName>."
					+ "\nHere are all possible commands:\n");
				foreach (var botCom in CommandManager.AllCommands.Select(c => c.InvokeName).GroupBy(n => n.Split(' ')[0]))
					strb.Append(botCom.Key).Append(", ");
				strb.Length -= 2;
				return strb.ToString();
			}
			else
			{
				CommandGroup group = CommandManager.CommandSystem.RootCommand;
				ICommand target = null;
				for (int i = 0; i < parameter.Length; i++)
				{
					var possibilities = XCommandSystem.FilterList(group.Commands, parameter[i]).ToList();
					if (possibilities.Count == 0)
						return "No matching command found! Try !help to get a list of all commands.";
					else if (possibilities.Count > 1)
						return "Requested command is ambiguous between: " + string.Join(", ", possibilities.Select(kvp => kvp.Key));
					else if (possibilities.Count == 1)
					{
						target = possibilities.First().Value;
						if (i < parameter.Length - 1)
						{
							group = target as CommandGroup;
							if (group == null)
								return "The command has no further subfunctions after " + string.Join(" ", parameter, 0, i);
						}
					}
				}

				var targetB = target as BotCommand;
				if (targetB != null)
					return targetB.GetHelp();

				var targetCG = target as CommandGroup;
				if (targetCG != null)
					return "The command contains the following subfunctions: " + string.Join(", ", targetCG.Commands.Select(g => g.Key));

				var targetOFC = target as OverloadedFunctionCommand;
				if (targetOFC != null)
				{
					var strb = new StringBuilder();
					foreach (var botCom in targetOFC.Functions.OfType<BotCommand>())
						strb.Append(botCom.GetHelp());
					return strb.ToString();
				}

				return "Seems like something went wrong. No help can be shown for this command path.";
			}
		}

		[Command(Private, "history from", "Gets the last <count> songs from the user with the given <user-dbid>")]
		[RequiredParameters(1)]
		string CommandHistoryFrom(ExecutionInformation info, uint userDbId, int? amount)
		{
			SeachQuery query = new SeachQuery();
			query.UserId = userDbId;

			if (amount.HasValue)
				query.MaxResults = amount.Value;

			return HistoryManager.SearchParsed(query);
		}

		[Command(Private, "history help", "You know...")]
		string CommandHistoryHelp(ExecutionInformation info) => CommandHelp(info, "history");

		[Command(Private, "history id", "<id> Displays all saved informations about the song with <id>")]
		string CommandHistoryId(ExecutionInformation info, uint id)
		{
			var ale = HistoryManager.GetEntryById(id);
			if (ale != null)
				return HistoryManager.Formatter.ProcessQuery(ale);
			return "Could not find track with this id";
		}

		[Command(Private, "history id", "(last|next) Gets the highest|next song id")]
		string CommandHistoryId(ExecutionInformation info, string special)
		{
			if (special == "last")
				return $"{HistoryManager.HighestId} is the currently highest song id.";
			else if (special == "next")
				return $"{HistoryManager.HighestId + 1} will be the next song id.";
			else
				return "Unrecognized name descriptor";
		}

		[Command(Private, "history last", "Plays the last song again")]
		[Usage("<count>", "Gets the last <count> played songs.")]
		[RequiredParameters(0)]
		string CommandHistoryLast(ExecutionInformation info, int? amount)
		{
			if (amount.HasValue)
			{
				var query = new SeachQuery { MaxResults = amount.Value };
				return HistoryManager.SearchParsed(query);
			}
			else
			{
				var ale = HistoryManager.Search(new SeachQuery { MaxResults = 1 }).FirstOrDefault();
				if (ale != null)
				{
					ClientData client = QueryConnection.GetClientById(info.TextMessage.InvokerId);
					return FactoryManager.RestoreAndPlay(ale, new PlayData(info.Session, client, null, false));
				}
				else return "There is no song in the history";
			}
		}

		[Command(Private, "history play", "Playes the song with <id>")]
		string CommandHistoryPlay(ExecutionInformation info, uint id)
		{
			var ale = HistoryManager.GetEntryById(id);
			if (ale != null)
			{
				ClientData client = QueryConnection.GetClientById(info.TextMessage.InvokerId);
				return FactoryManager.RestoreAndPlay(ale, new PlayData(info.Session, client, null, false));
			}
			else return "Could not find track with this id";
		}

		[Command(Private, "history till", "<date> Gets all songs played until <date>.")]
		string CommandHistoryTill(ExecutionInformation info, DateTime time)
		{
			var query = new SeachQuery { LastInvokedAfter = time };
			return HistoryManager.SearchParsed(query);
		}

		[Command(Private, "history till", "<name> Any of those desciptors: (hour|today|yesterday|week)")]
		string CommandHistoryTill(ExecutionInformation info, string time)
		{
			DateTime tillTime;
			switch (time.ToLower())
			{
			case "hour": tillTime = DateTime.Now.AddHours(-1); break;
			case "today": tillTime = DateTime.Today; break;
			case "yesterday": tillTime = DateTime.Today.AddDays(-1); break;
			case "week": tillTime = DateTime.Today.AddDays(-7); break;
			default: return "Not recognized time desciption.";
			}
			var query = new SeachQuery { LastInvokedAfter = tillTime };
			return HistoryManager.SearchParsed(query);
		}

		[Command(Private, "history title", "Gets all songs which title contains <string>")]
		string CommandHistoryTitle(ExecutionInformation info, string part)
		{
			var query = new SeachQuery { TitlePart = part };
			return HistoryManager.SearchParsed(query);
		}

		[Command(AnyVisibility, "if")]
		[Usage("<argument0> <comparator> <argument1> <then>", "Compares the two arguments and returns or executes the then-argument")]
		[Usage("<argument0> <comparator> <argument1> <then> <else>", "Same as before and return the else-arguments if the condition is false")]
		ICommandResult CommandIf(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			var argList = arguments.ToList();
			if (argList.Count < 4)
				throw new CommandException("Expected at least 4 arguments");
			var arg0 = ((StringCommandResult)argList[0].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
			var cmp = ((StringCommandResult)argList[1].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
			var arg1 = ((StringCommandResult)argList[2].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;

			Func<double, double, bool> comparer;
			switch (cmp)
			{
			case "<": comparer = (a, b) => a < b; break;
			case ">": comparer = (a, b) => a > b; break;
			case "<=": comparer = (a, b) => a <= b; break;
			case ">=": comparer = (a, b) => a >= b; break;
			case "==": comparer = (a, b) => a == b; break;
			case "!=": comparer = (a, b) => a != b; break;
			default: throw new CommandException("Unknown comparison operator");
			}

			double d0, d1;
			bool cmpResult;
			// Try to parse arguments into doubles
			if (double.TryParse(arg0, NumberStyles.Number, CultureInfo.InvariantCulture, out d0)
				&& double.TryParse(arg1, NumberStyles.Number, CultureInfo.InvariantCulture, out d1))
				cmpResult = comparer(d0, d1);
			else
				cmpResult = comparer(arg0.CompareTo(arg1), 0);

			// If branch
			if (cmpResult)
				return argList[3].Execute(info, Enumerable.Empty<ICommand>(), returnTypes);
			// Else branch
			if (argList.Count > 4)
				return argList[4].Execute(info, Enumerable.Empty<ICommand>(), returnTypes);

			// Try to return nothing
			if (returnTypes.Contains(CommandResultType.Empty))
				return new EmptyCommandResult();
			throw new CommandException("If found nothing to return");
		}

		[Command(Private, "kickme", "Guess what?")]
		[Usage("[far]", "Optional attribute for the extra punch strength")]
		[RequiredParameters(0)]
		string CommandKickme(ExecutionInformation info, string parameter)
		{
			try
			{
				if (string.IsNullOrEmpty(parameter) || parameter == "near")
					QueryConnection.KickClientFromChannel(info.TextMessage.InvokerId);
				else if (parameter == "far")
					QueryConnection.KickClientFromServer(info.TextMessage.InvokerId);
				return null;
			}
			catch (QueryCommandException ex)
			{
				Log.Write(Log.Level.Info, "Could not kick: {0}", ex);
				return "I'm not strong enough, master!";
			}
		}

		[Command(Private, "link", "Gets a link to the origin of the current song.")]
		string CommandLink(ExecutionInformation info)
		{
			if (AudioFramework.CurrentPlayData == null)
				return "There is nothing on right now...";
			else if (QuizMode && AudioFramework.CurrentPlayData.Invoker.ClientId != info.TextMessage.InvokerId)
				return "Sorry, you have to guess!";
			else
				return FactoryManager.RestoreLink(AudioFramework.CurrentPlayData);
		}

		[Command(Private, "loop", "Sets whether or not to loop the entire playlist.")]
		[Usage("(on|off)]", "on or off")]
		[RequiredParameters(0)]
		string CommandLoop(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				return "Loop is " + (AudioFramework.Loop ? "on" : "off");
			else if (parameter == "on")
				AudioFramework.Loop = true;
			else if (parameter == "off")
				AudioFramework.Loop = false;
			else
				return CommandHelp(info, "loop");
			return null;
		}

		[Command(Private, "media", "Plays any local or online media file.")]
		string CommandMedia(ExecutionInformation info, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(info.TextMessage.InvokerId);
			return FactoryManager.LoadAndPlay(AudioType.MediaLink, new PlayData(info.Session, client, parameter, false));
		}

		[Command(Private, "next", "Plays the next song in the playlist.")]
		void CommandNext()
		{
			AudioFramework.Next();
		}

		[Command(Public, "pm", "Requests a private session with the ServerBot so you can invoke private commands.")]
		void CommandPM(ExecutionInformation info)
		{
			BotSession ownSession = SessionManager.CreateSession(this, info.TextMessage.InvokerId);
			ownSession.Write("Hi " + info.TextMessage.InvokerName);
		}

		[Command(Admin, "parse", "Displays the AST of the requested command.")]
		[Usage("<command>", "The comand to be parsed")]
		string CommandParse(ExecutionInformation info, string parameter)
		{
			if (!parameter.TrimStart().StartsWith("!"))
				return "This is not a command";
			try
			{
				var node = CommandParser.ParseCommandRequest(parameter);
				StringBuilder strb = new StringBuilder();
				strb.AppendLine();
				node.Write(strb, 0);
				return strb.ToString();
			}
			catch
			{
				return "GJ - You crashed it!!!";
			}
		}

		[Command(Private, "pause", "Well, pauses the song. Undo with !play")]
		void CommandPause()
		{
			AudioFramework.Pause = true;
		}

		[Command(Private, "play", "Automatically tries to decide whether the link is a special resource (like youtube) or a direct resource (like ./hello.mp3) and starts it")]
		[Usage("<link>", "Youtube, Soundcloud, local path or file link")]
		[RequiredParameters(0)]
		string CommandPlay(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
			{
				AudioFramework.Pause = false;
				return null;
			}
			else
			{
				ClientData client = QueryConnection.GetClientById(info.TextMessage.InvokerId);
				return FactoryManager.LoadAndPlay(new PlayData(info.Session, client, parameter, false));
			}
		}

		[Command(Admin, "plugin list", "Lists all found plugins.")]
		string CommandPluginList(ExecutionInformation info)
		{
			return PluginManager.GetPluginOverview();
		}

		[Command(Admin, "plugin unload", "Unloads a plugin.")]
		string CommandPluginUnload(ExecutionInformation info, string identifier)
		{
			return PluginManager.UnloadPlugin(identifier).ToString();
		}

		[Command(Admin, "plugin load", "Unloads a plugin.")]
		string CommandPluginLoad(ExecutionInformation info, string identifier)
		{
			return PluginManager.LoadPlugin(identifier).ToString();
		}

		[Command(Private, "previous", "Plays the previous song in the playlist.")]
		void CommandPrevious()
		{
			AudioFramework.Previous();
		}

		[Command(AnyVisibility, "print", "Lets you format multiple parameter to one.")]
		string CommandPrint(ExecutionInformation info, string[] parameter)
		{
			// << Desing changes expected >>
			var strb = new StringBuilder();
			foreach (var param in parameter)
				strb.Append(param);
			return strb.ToString();
		}

		[Command(Admin, "quit", "Closes the TS3AudioBot application.")]
		string CommandQuit(ExecutionInformation info)
		{
			info.Session.SetResponse(ResponseQuit, null);
			return "Do you really want to quit? !(yes|no)";
		}

		[Command(Admin, "quit force", "Closes the TS3AudioBot application.")]
		void CommandQuitForce(ExecutionInformation info)
		{
			info.Session.Write("Goodbye!");
			Dispose();
			Log.Write(Log.Level.Info, "Exiting...");
		}

		[Command(Admin, "quit last", "Disconnects the Bob when noone is on the server anymore.")]
		void CommandQuitLast(ExecutionInformation info)
		{
			throw new NotImplementedException();
		}

		[Command(Admin, "quit reset", "Discards any \"quit last\" request.")]
		void CommandQuitReset(ExecutionInformation info)
		{
			throw new NotImplementedException();
		}

		[Command(Public, "quiz", "Enable to hide the songnames and let your friends guess the title.")]
		[Usage("(on|off)]", "on or off")]
		[RequiredParameters(0)]
		string CommandQuiz(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				return "Quizmode is " + (QuizMode ? "on" : "off");
			else if (parameter == "on")
				QuizMode = true;
			else if (parameter == "off")
			{
				if (info.Session.IsPrivate)
					return "No cheatig! Everybody has to see it!";
				QuizMode = false;
			}
			else
				CommandHelp(info, "quiz");
			return null;
		}

		[Command(Private, "repeat", "Sets whether or not to loop a single song.")]
		[Usage("(on|off)]", "on or off")]
		[RequiredParameters(0)]
		string CommandRepeat(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				return "Repeat is " + (AudioFramework.Repeat ? "on" : "off");
			else if (parameter == "on")
				AudioFramework.Repeat = true;
			else if (parameter == "off")
				AudioFramework.Repeat = false;
			else
				return CommandHelp(info, "repeat");
			return null;
		}

		[Command(AnyVisibility, "rng", "Gets a random number.")]
		[Usage("", "Gets a number between 0 and 2147483647")]
		[Usage("<max>", "Gets a number between 0 and <max>")]
		[Usage("<min> <max>", "Gets a number between <min> and <max>")]
		[RequiredParameters(0)]
		string CommandRng(ExecutionInformation info, int? first, int? second)
		{
			if (second != null)
				return Util.RngInstance.Next(first.Value, second.Value).ToString();
			else if (first != null)
				return Util.RngInstance.Next(first.Value).ToString();
			else
				return Util.RngInstance.Next().ToString();
		}

		[Command(Private, "seek", "Jumps to a timemark within the current song.")]
		[Usage("<sec>", "Time in seconds")]
		[Usage("<min:sec>", "Time in Minutes:Seconds")]
		string CommandSeek(ExecutionInformation info, string parameter)
		{
			TimeSpan span;
			bool parsed = false;
			if (parameter.Contains(":"))
			{
				string[] splittime = parameter.Split(':');
				if (splittime.Length == 2)
				{
					int seconds = -1, minutes;
					parsed = int.TryParse(splittime[0], out minutes) && int.TryParse(splittime[1], out seconds);
					if (parsed)
						span = TimeSpan.FromSeconds(seconds) + TimeSpan.FromMinutes(minutes);
					else
						span = TimeSpan.MinValue;
				}
				else span = TimeSpan.MinValue;
			}
			else
			{
				int seconds;
				parsed = int.TryParse(parameter, out seconds);
				span = TimeSpan.FromSeconds(seconds);
			}

			if (!parsed)
				return CommandHelp(info, "seek");

			if (!AudioFramework.Seek(span))
				return "The point of time is not within the songlenth.";
			return null;
		}

		[Command(AnyVisibility, "song", "Tells you the name of the current song.")]
		string CommandSong(ExecutionInformation info)
		{
			if (AudioFramework.CurrentPlayData == null)
				return "There is nothing on right now...";
			else if (QuizMode && AudioFramework.CurrentPlayData.Invoker.ClientId != info.TextMessage.InvokerId)
				return "Sorry, you have to guess!";
			else
				return $"[url={FactoryManager.RestoreLink(AudioFramework.CurrentPlayData)}]{AudioFramework.CurrentPlayData.Resource.ResourceTitle}[/url]";
		}

		[Command(Private, "soundcloud", "Resolves the link as a soundcloud song to play it for you.")]
		string CommandSoundcloud(ExecutionInformation info, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(info.TextMessage.InvokerId);
			return FactoryManager.LoadAndPlay(AudioType.Soundcloud, new PlayData(info.Session, client, parameter, false));
		}

		[Command(Private, "stop", "Stops the current song.")]
		void CommandStop()
		{
			AudioFramework.Stop();
		}

		[Command(Private, "subscribe", "Lets you hear the music independent from the channel you are in.")]
		void CommandSubscribe(ExecutionInformation info)
		{
			BobController.WhisperClientSubscribe(info.TextMessage.InvokerId);
		}

		[Command(AnyVisibility, "take", "Take a substring from a string")]
		[Usage("<count> <text>", "Take only <count> parts of the text")]
		[Usage("<count> <start> <text>", "Take <count> parts, starting with the part at <start>")]
		[Usage("<count> <start> <delimiter> <text>", "Specify another delimiter for the parts than spaces")]
		ICommandResult CommandTake(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			var argList = arguments.ToList();

			if (argList.Count < 2)
				throw new CommandException("Expected at least 2 parameters");

			int start = 0;
			int count = 0;
			string delimiter = null;

			// Get count
			var res = ((StringCommandResult)argList[0].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
			if (!int.TryParse(res, out count) || count < 0)
				throw new CommandException("Count must be an integer >= 0");

			if (argList.Count > 2)
			{
				// Get start
				res = ((StringCommandResult)argList[1].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
				if (!int.TryParse(res, out start) || start < 0)
					throw new CommandException("Start must be an integer >= 0");
			}

			if (argList.Count > 3)
				// Get delimiter
				delimiter = ((StringCommandResult)argList[2].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;

			string text = ((StringCommandResult)argList[Math.Min(argList.Count - 1, 3)]
				.Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;

			IEnumerable<string> splitted;
			if (delimiter == null)
				splitted = text.Split();
			else
				splitted = text.Split(new[] { delimiter }, StringSplitOptions.None);
			if (splitted.Count() < start + count)
				throw new CommandException("Not enough arguments to take");
			splitted = splitted.Skip(start).Take(count);

			foreach (var returnType in returnTypes)
			{
				if (returnType == CommandResultType.String)
					return new StringCommandResult(string.Join(delimiter ?? " ", splitted));
			}

			throw new CommandException("Can't find a fitting return type for take");
		}

		[Command(Admin, "test", "Only for debugging purposes")]
		string CommandTest(ExecutionInformation info)
		{
			if (!info.Session.IsPrivate)
				return "Please use as private, admins too!";
			else
			{
				info.Session.Write("Good boy!");
				// stresstest
				for (int i = 0; i < 10; i++)
					info.Session.Write(i.ToString());
				return "Test end";
			}
		}

		[Command(Private, "twitch", "Resolves the link as a twitch stream to play it for you.")]
		string CommandTwitch(ExecutionInformation info, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(info.TextMessage.InvokerId);
			return FactoryManager.LoadAndPlay(AudioType.Twitch, new PlayData(info.Session, client, parameter, false));
		}

		[Command(Private, "unsubscribe", "Only lets you hear the music in active channels again.")]
		void CommandUnsubscribe(ExecutionInformation info)
		{
			BobController.WhisperClientUnsubscribe(info.TextMessage.InvokerId);
		}

		[Command(AnyVisibility, "volume", "Sets the volume level of the music.")]
		[Usage("<level>", "A new volume level between 0 and 100")]
		string CommandVolume(ExecutionInformation info, string parameter)
		{
			bool relPos = parameter.StartsWith("+");
			bool relNeg = parameter.StartsWith("-");
			string numberString = (relPos || relNeg) ? parameter.Remove(0, 1) : parameter;

			int volume;
			if (!int.TryParse(numberString, out volume))
				return CommandHelp(info, "volume");

			int newVolume;
			if (relPos) newVolume = AudioFramework.Volume + volume;
			else if (relNeg) newVolume = AudioFramework.Volume - volume;
			else newVolume = volume;

			if (newVolume < 0 || newVolume > AudioFramework.MaxVolume)
				return "The volume level must be between 0 and " + AudioFramework.MaxVolume;

			if (newVolume <= AudioFramework.MaxUserVolume || newVolume < AudioFramework.Volume)
				AudioFramework.Volume = newVolume;
			else if (newVolume <= AudioFramework.MaxVolume)
			{
				info.Session.SetResponse(ResponseVolume, newVolume);
				return "Careful you are requesting a very high volume! Do you want to apply this? !(yes|no)";
			}
			return null;
		}

		[Command(Private, "youtube", "Resolves the link as a youtube video to play it for you.")]
		string CommandYoutube(ExecutionInformation info, string parameter)
		{
			ClientData client = QueryConnection.GetClientById(info.TextMessage.InvokerId);
			return FactoryManager.LoadAndPlay(AudioType.Youtube, new PlayData(info.Session, client, parameter, false));
		}

		#endregion

		#region RESPONSES

		private bool ResponseVolume(BotSession session, TextMessage tm, Lazy<bool> isAdmin)
		{
			Answer answer = TextUtil.GetAnswer(tm.Message);
			if (answer == Answer.Yes)
			{
				if (isAdmin.Value)
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

		private bool ResponseQuit(BotSession session, TextMessage tm, Lazy<bool> isAdmin)
		{
			Answer answer = TextUtil.GetAnswer(tm.Message);
			if (answer == Answer.Yes)
			{
				if (isAdmin.Value)
				{
					CommandQuitForce(new ExecutionInformation() { Session = session, TextMessage = tm, IsAdmin = isAdmin });
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
			if (PluginManager != null)
			{
				PluginManager.Dispose();
				PluginManager = null;
			}
			if (QueryConnection != null)
			{
				QueryConnection.Dispose();
				QueryConnection = null;
			}
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
			if (logStream != null)
			{
				logStream.Dispose();
				logStream = null;
			}
		}
	}

	public class PlayData : MarshalByRefObject
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
