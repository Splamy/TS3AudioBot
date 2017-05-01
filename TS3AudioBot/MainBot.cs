// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

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
	using Sessions;
	using Web.Api;
	using Web;

	using TS3Client;
	using TS3Client.Messages;

	using static CommandRights;

	public sealed class MainBot : IDisposable
	{
		internal static void Main(string[] args)
		{
			using (var bot = new MainBot())
			{
				AppDomain.CurrentDomain.UnhandledException += (s, e) =>
				{
					Log.Write(Log.Level.Error, "Critical program failure!. Exception:\n{0}", (e.ExceptionObject as Exception).UnrollException());
					bot?.Dispose();
				};

				if (!bot.ReadParameter(args)) return;
				if (!bot.InitializeBot()) return;
				bot.Run();
			}
		}

		private bool isDisposed;
		private bool consoleOutput;
		private bool writeLog;
		private bool writeLogStack;
		internal MainBotData mainBotData;

		private StreamWriter logStream;

		internal PluginManager PluginManager { get; private set; }
		public CommandManager CommandManager { get; private set; }
		public AudioFramework AudioFramework { get; private set; }
		public PlaylistManager PlaylistManager { get; private set; }
		public TeamspeakControl QueryConnection { get; private set; }
		public SessionManager SessionManager { get; private set; }
		public HistoryManager HistoryManager { get; private set; }
		public ResourceFactoryManager FactoryManager { get; private set; }
		public WebManager WebManager { get; private set; }
		public PlayManager PlayManager { get; private set; }
		public ITargetManager TargetManager { get; private set; }
		public ConfigFile ConfigManager { get; private set; }

		public bool QuizMode { get; set; }

		public MainBot()
		{
			isDisposed = false;
			consoleOutput = false;
			writeLog = false;
		}

		private bool ReadParameter(string[] args)
		{
			var launchParameter = new HashSet<string>();
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
			ConfigManager = ConfigFile.OpenOrCreate(configFilePath) ?? ConfigFile.CreateDummy();
			var afd = ConfigManager.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			var tfcd = ConfigManager.GetDataStruct<Ts3FullClientData>("QueryConnection", true);
			var hmd = ConfigManager.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			var pmd = ConfigManager.GetDataStruct<PluginManagerData>("PluginManager", true);
			var pld = ConfigManager.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			var yfd = ConfigManager.GetDataStruct<YoutubeFactoryData>("YoutubeFactory", true);
			var webd = ConfigManager.GetDataStruct<WebData>("WebData", true);
			mainBotData = ConfigManager.GetDataStruct<MainBotData>("MainBot", true);
			ConfigManager.Close();

			if (consoleOutput)
			{
				Log.RegisterLogger("[%T]%L: %M", "", Console.WriteLine);
				Log.RegisterLogger("Error call Stack:\n%S", "", Console.WriteLine, Log.Level.Error);
			}

			if (writeLog && !string.IsNullOrEmpty(mainBotData.LogFile))
			{
				logStream = new StreamWriter(File.Open(mainBotData.LogFile, FileMode.Append, FileAccess.Write, FileShare.Read), Util.Utf8Encoder);
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
			var teamspeakClient = new Ts3Full(tfcd);
			QueryConnection = teamspeakClient;
			PlaylistManager = new PlaylistManager(pld);
			AudioFramework = new AudioFramework(afd, teamspeakClient);
			SessionManager = new SessionManager();
			HistoryManager = new HistoryManager(hmd);
			PluginManager = new PluginManager(this, pmd);
			PlayManager = new PlayManager(this);
			WebManager = new WebManager(this, webd);
			TargetManager = teamspeakClient;

			Log.Write(Log.Level.Info, "[=========== Initializing Factories ===========]");
			YoutubeDlHelper.DataObj = yfd;
			FactoryManager = new ResourceFactoryManager();
			var mediaFactory = new MediaFactory();
			FactoryManager.AddFactory(mediaFactory);
			var youtubeFactory = new YoutubeFactory(yfd);
			FactoryManager.AddFactory(youtubeFactory);
			var soundcloudFactory = new SoundcloudFactory();
			FactoryManager.AddFactory(soundcloudFactory);
			FactoryManager.AddFactory(new TwitchFactory());
			FactoryManager.DefaultFactorty = mediaFactory;
			CommandManager.RegisterCommand(FactoryManager.CommandNode, "from");

			PlaylistManager.AddFactory(youtubeFactory);
			PlaylistManager.AddFactory(mediaFactory);
			PlaylistManager.AddFactory(soundcloudFactory);
			CommandManager.RegisterCommand(PlaylistManager.CommandNode, "list from");

			Log.Write(Log.Level.Info, "[=========== Registering callbacks ============]");
			AudioFramework.OnPlaybackStopped += PlayManager.SongStoppedHook;
			// Inform the BobClient on start/stop
			PlayManager.AfterResourceStarted += TargetManager.OnResourceStarted;
			PlayManager.AfterResourceStopped += TargetManager.OnResourceStopped;
			// In own favor update the own status text to the current song title
			PlayManager.AfterResourceStarted += LoggedUpdateBotStatus;
			PlayManager.AfterResourceStopped += LoggedUpdateBotStatus;
			// Register callback for all messages happening
			QueryConnection.OnMessageReceived += TextCallback;
			// Register callback to remove open private sessions, when user disconnects
			//QueryConnection.OnClientDisconnect += (s, e) => SessionManager.RemoveSession(e.InvokerUid);


			Log.Write(Log.Level.Info, "[================= Finalizing =================]");
			WebManager.StartServerAsync();

			Log.Write(Log.Level.Info, "[============== Connected & Done ==============]");
			return true;
		}

		// TODO rework later for multi instance feature
		private void Run()
		{
			Thread.CurrentThread.Name = "Main/Eventloop";
			// Connect the query after everyting is set up
			try { QueryConnection.Connect(); }
			catch (Ts3Exception qcex)
			{
				Log.Write(Log.Level.Error, "There is either a problem with your connection configuration, or the query has not all permissions it needs. ({0})", qcex);
				return;
			}
		}

		#region COMMAND EXECUTING & CHAINING

		private void TextCallback(object sender, TextMessage textMessage)
		{
			Log.Write(Log.Level.Debug, "MB Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			textMessage.Message = textMessage.Message.TrimStart(new[] { ' ' });
			if (!textMessage.Message.StartsWith("!", StringComparison.Ordinal))
				return;

			var refreshResult = QueryConnection.RefreshClientBuffer(true);
			if (!refreshResult.Ok)
			{
				Log.Write(Log.Level.Error, "Bot is not correctly set up: {0}", refreshResult.Message);
				return;
			}

			// get the current session
			var result = SessionManager.GetSession(textMessage.InvokerUid);
			if (!result.Ok)
			{
				var clientResult = QueryConnection.GetClientById(textMessage.InvokerId);
				if (!clientResult.Ok)
				{
					Log.Write(Log.Level.Error, clientResult.Message);
					return;
				}
				result = SessionManager.CreateSession(this, clientResult.Value);
				if (!result.Ok)
				{
					Log.Write(Log.Level.Error, result.Message);
					return;
				}
			}

			// Update session
			UserSession session = result.Value;
			var updateResult = session.UpdateClient(textMessage.InvokerId);
			if (!updateResult.Ok)
			{
				Log.Write(Log.Level.Error, "MB Failed to get user: {0}", updateResult.Message);
				return;
			}

			using (session.GetLock())
			{
				var execInfo = new ExecutionInformation(session, textMessage)
				{
					IsPrivate = textMessage.Target == TextMessageTargetMode.Private
				};

				// check if the user has an open request
				if (session.ResponseProcessor != null)
				{
					var msg = session.ResponseProcessor(execInfo);
					session.ClearResponse();
					if (!string.IsNullOrEmpty(msg))
						WriteToSession(execInfo, msg);
					return;
				}

				// parse (and execute) the command
				ASTNode parsedAst = CommandParser.ParseCommandRequest(textMessage.Message);
				if (parsedAst.Type == ASTType.Error)
				{
					var errorAst = (ASTError)parsedAst;
					var strb = new StringBuilder();
					strb.AppendLine();
					errorAst.Write(strb, 0);
					WriteToSession(execInfo, strb.ToString());
					return;
				}

				var command = CommandManager.CommandSystem.AstToCommandResult(parsedAst);
				try
				{
					var res = command.Execute(execInfo, Enumerable.Empty<ICommand>(),
						new[] { CommandResultType.String, CommandResultType.Empty });
					// Write result to user
					if (res.ResultType == CommandResultType.String)
					{
						var sRes = (StringCommandResult)res;
						if (!string.IsNullOrEmpty(sRes.Content))
							WriteToSession(execInfo, sRes.Content);
					}
					else if (res.ResultType == CommandResultType.Json)
					{
						var sRes = (JsonCommandResult)res;
						WriteToSession(execInfo, "\nJson str: \n" + sRes.JsonObject.AsStringResult);
						WriteToSession(execInfo, "\nJson val: \n" + Util.Serializer.Serialize(sRes.JsonObject));
					}
				}
				catch (CommandException ex)
				{
					WriteToSession(execInfo, "Error: " + ex.Message);
				}
				catch (Exception ex)
				{
					Log.Write(Log.Level.Error, "MB Unexpected command error: {0}", ex.UnrollException());
					WriteToSession(execInfo, "An unexpected error occured: " + ex.Message);
				}
			}
		}

		private static void WriteToSession(ExecutionInformation info, string message)
		{
			info.Session.Write(message, info.IsPrivate);
		}

		#endregion

		#region COMMANDS

		// [...] = Optional
		// <name> = Placeholder for a text
		// [text] = Option for fixed text
		// (a|b) = either or switch

		[Command(Private, "add", "Adds a new song to the queue.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public void CommandAdd(ExecutionInformation info, string parameter)
			=> PlayManager.Enqueue(new InvokerData(info.Session.Client), parameter).UnwrapThrow();

		[Command(AnyVisibility, "api token", "Generates an api token.")]
		public JsonObject CommandApiToken(ExecutionInformation info)
		{
			if (!info.IsPrivate)
				throw new CommandException("Please use this command in a private session.", CommandExceptionReason.CommandError);
			var token = info.Session.GenerateToken().UnwrapThrow();
			return new JsonSingleValue<string>(token, token);
		}

		[Command(AnyVisibility, "api nonce", "Generates an api nonce.")]
		public JsonObject CommandApiNonce(ExecutionInformation info)
		{
			if (!info.IsPrivate)
				throw new CommandException("Please use this command in a private session.", CommandExceptionReason.CommandError);
			if (!info.Session.HasActiveToken)
				throw new CommandException("No active token found.", CommandExceptionReason.CommandError);

			var nonce = info.Session.Token.CreateNonce();
			return new JsonSingleValue<string>(nonce.Value, nonce.Value);
		}

		[Command(Admin, "bot name", "Gives the bot a new name.")]
		public void CommandBotName(string name) => QueryConnection.ChangeName(name).UnwrapThrow();

		[Command(Admin, "bot setup", "Gives the bot a new name.")]
		[RequiredParameters(0)]
		public void CommandBotSetup(string adminToken)
		{
			QueryConnection.SetupRights(adminToken, mainBotData).UnwrapThrow();
		}

		[Command(Private, "clear", "Removes all songs from the current playlist.")]
		public void CommandClear()
		{
			PlaylistManager.ClearFreelist();
		}

		[Command(AnyVisibility, "eval", "Executes a given command or string")]
		[Usage("<command> <arguments...>", "Executes the given command on arguments")]
		[Usage("<strings...>", "Concat the strings and execute them with the command system")]
		public ICommandResult CommandEval(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			// Evaluate the first argument on the rest of the arguments
			if (!arguments.Any())
				throw new CommandException("Need at least one argument to evaluate", CommandExceptionReason.MissingParameter);
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

		[Command(Admin, "getuser id", "Gets the unique Id of a user.")]
		[Usage("<username>", "A user which is currently logged in to the server")]
		public JsonObject CommandGetUserByName(string username)
		{
			var result = QueryConnection.GetClientByName(username);
			if (result.Ok)
			{
				var client = result.Value;
				return new JsonSingleObject<ClientData>($"Client: UID:{client.ClientId} DBID:{client.DatabaseId} ChanID:{client.ChannelId}", client);
			}
			else
				return new JsonEmpty("No user found...");
		}

		[Command(Admin, "getuser db", "Gets the User name by dbid.")]
		[Usage("<dbid>", "Any user dbid which is known by the server")]
		public JsonObject CommandGetUserByDb(ulong parameter)
		{
			var client = QueryConnection.GetNameByDbId(parameter);
			if (client == null)
				return new JsonEmpty("No user found...");
			else
				return new JsonSingleValue<string>("Clientname: " + client, client);
		}

		[Command(AnyVisibility, "help", "Shows all commands or detailed help about a specific command.")]
		[Usage("[<command>]", "Any currently accepted command")]
		[RequiredParameters(0)]
		public JsonObject CommandHelp(ExecutionInformation info, params string[] parameter)
		{
			if (parameter.Length == 0)
			{
				var strb = new StringBuilder();
				strb.Append("\n========= Welcome to the TS3AudioBot ========="
					+ "\nIf you need any help with a special command use !help <commandName>."
					+ "\nHere are all possible commands:\n");
				var botComList = CommandManager.AllCommands.Select(c => c.InvokeName).GroupBy(n => n.Split(' ')[0]).Select(x => x.Key).ToArray();
				foreach (var botCom in botComList)
					strb.Append(botCom).Append(", ");
				strb.Length -= 2;
				return new JsonArray<string>(strb.ToString(), botComList);
			}
			else
			{
				CommandGroup group = CommandManager.CommandSystem.RootCommand;
				ICommand target = null;
				for (int i = 0; i < parameter.Length; i++)
				{
					var possibilities = XCommandSystem.FilterList(group.Commands, parameter[i]).ToList();
					if (possibilities.Count == 0)
						throw new CommandException("No matching command found! Try !help to get a list of all commands.", CommandExceptionReason.CommandError);
					else if (possibilities.Count > 1)
						throw new CommandException("Requested command is ambiguous between: " + string.Join(", ", possibilities.Select(kvp => kvp.Key)),
							CommandExceptionReason.CommandError);
					else if (possibilities.Count == 1)
					{
						target = possibilities.First().Value;
						if (i < parameter.Length - 1)
						{
							group = target as CommandGroup;
							if (group == null)
								throw new CommandException("The command has no further subfunctions after " + string.Join(" ", parameter, 0, i),
									CommandExceptionReason.CommandError);
						}
					}
				}

				var targetB = target as BotCommand;
				if (targetB != null)
					return new JsonSingleValue<string>(targetB.GetHelp(), targetB.GetHelp());

				var targetCG = target as CommandGroup;
				if (targetCG != null)
				{
					var subList = targetCG.Commands.Select(g => g.Key).ToArray();
					return new JsonArray<string>("The command contains the following subfunctions: " + string.Join(", ", subList), subList);
				}

				var targetOfc = target as OverloadedFunctionCommand;
				if (targetOfc != null)
				{
					var strb = new StringBuilder();
					foreach (var botCom in targetOfc.Functions.OfType<BotCommand>())
						strb.Append(botCom.GetHelp());
					return new JsonSingleValue<string>(strb.ToString(), strb.ToString());
				}

				throw new CommandException("Seems like something went wrong. No help can be shown for this command path.", CommandExceptionReason.CommandError);
			}
		}

		[Command(Private, "history add", "<id> Adds the song with <id> to the queue")]
		public void CommandHistoryQueue(ExecutionInformation info, uint id)
			=> PlayManager.Enqueue(new InvokerData(info.Session.Client), id).UnwrapThrow();

		[Command(Admin, "history clean", "Cleans up the history file for better startup performance.")]
		public string CommandHistoryClean(ExecutionInformation info)
		{
			if (info.ApiCall)
			{
				HistoryManager.CleanHistoryFile();
				return null;
			}
			info.Session.SetResponse(ResponseHistoryClean, null);
			return $"Do want to clean the history file now? " +
					"This might take a while and make the bot unresponsive in meanwhile. !(yes|no)";
		}

		[Command(Admin, "history clean removedefective", "Cleans up the history file for better startup performance.")]
		public string CommandHistoryCleanRemove(ExecutionInformation info)
		{
			if (info.ApiCall)
			{
				HistoryManager.RemoveBrokenLinks(info.Session);
				return null;
			}
			info.Session.SetResponse(ResponseHistoryClean, "removedefective");
			return $"Do want to remove all defective links file now? " +
					"This might(will!) take a while and make the bot unresponsive in meanwhile. !(yes|no)";
		}

		[Command(Admin, "history delete", "<id> Removes the entry with <id> from the history")]
		public string CommandHistoryDelete(ExecutionInformation info, uint id)
		{
			var ale = HistoryManager.GetEntryById(id).UnwrapThrow();

			if (info.ApiCall)
			{
				HistoryManager.RemoveEntry(ale);
				return null;
			}
			info.Session.SetResponse(ResponseHistoryDelete, ale);
			string name = ale.AudioResource.ResourceTitle;
			if (name.Length > 100)
				name = name.Substring(100) + "...";
			return $"Do you really want to delete the entry \"{name}\"\nwith the id {id}? !(yes|no)";
		}

		[Command(Private, "history from", "Gets the last <count> songs from the user with the given <user-dbid>")]
		[RequiredParameters(1)]
		public JsonObject CommandHistoryFrom(uint userDbId, int? amount)
		{
			var query = new SeachQuery { UserId = userDbId };
			if (amount.HasValue)
				query.MaxResults = amount.Value;

			var results = HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(HistoryManager.Format(results), results);
		}

		[Command(Private, "history id", "<id> Displays all saved informations about the song with <id>")]
		public JsonObject CommandHistoryId(uint id)
		{
			var result = HistoryManager.GetEntryById(id);
			if (!result)
				return new JsonEmpty("No entry found...");
			return new JsonSingleObject<AudioLogEntry>(HistoryManager.Format(result.Value), result.Value);
		}

		[Command(Private, "history id", "(last|next) Gets the highest|next song id")]
		public JsonObject CommandHistoryId(string special)
		{
			if (special == "last")
				return new JsonSingleValue<uint>($"{HistoryManager.HighestId} is the currently highest song id.", HistoryManager.HighestId);
			else if (special == "next")
				return new JsonSingleValue<uint>($"{HistoryManager.HighestId + 1} will be the next song id.", HistoryManager.HighestId + 1);
			else
				throw new CommandException("Unrecognized name descriptor", CommandExceptionReason.CommandError);
		}

		[Command(Private, "history last", "Plays the last song again")]
		[Usage("<count>", "Gets the last <count> played songs.")]
		[RequiredParameters(0)]
		public JsonObject CommandHistoryLast(ExecutionInformation info, int? amount)
		{
			if (amount.HasValue)
			{
				var query = new SeachQuery { MaxResults = amount.Value };
				var results = HistoryManager.Search(query).ToArray();
				return new JsonArray<AudioLogEntry>(HistoryManager.Format(results), results);
			}
			else
			{
				var ale = HistoryManager.Search(new SeachQuery { MaxResults = 1 }).FirstOrDefault();
				if (ale != null)
				{
					PlayManager.Play(new InvokerData(info.Session.Client), ale.AudioResource).UnwrapThrow();
					return null;
				}
				else return new JsonEmpty("There is no song in the history");
			}
		}

		[Command(Private, "history play", "<id> Playes the song with <id>")]
		public void CommandHistoryPlay(ExecutionInformation info, uint id)
			=> PlayManager.Play(new InvokerData(info.Session.Client), id).UnwrapThrow();

		[Command(Admin, "history rename", "<id> <name> Sets the name of the song with <id> to <name>")]
		public void CommandHistoryRename(uint id, string newName)
		{
			var ale = HistoryManager.GetEntryById(id).UnwrapThrow();

			if (string.IsNullOrWhiteSpace(newName))
				throw new CommandException("The new name must not be empty or only whitespaces", CommandExceptionReason.CommandError);

			HistoryManager.RenameEntry(ale, newName);
		}

		[Command(Private, "history till", "<date> Gets all songs played until <date>.")]
		public JsonObject CommandHistoryTill(DateTime time)
		{
			var query = new SeachQuery { LastInvokedAfter = time };
			var results = HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(HistoryManager.Format(results), results);
		}

		[Command(Private, "history till", "<name> Any of those desciptors: (hour|today|yesterday|week)")]
		public JsonObject CommandHistoryTill(string time)
		{
			DateTime tillTime;
			switch (time.ToLower(CultureInfo.InvariantCulture))
			{
			case "hour": tillTime = DateTime.Now.AddHours(-1); break;
			case "today": tillTime = DateTime.Today; break;
			case "yesterday": tillTime = DateTime.Today.AddDays(-1); break;
			case "week": tillTime = DateTime.Today.AddDays(-7); break;
			default: throw new CommandException("Not recognized time desciption.", CommandExceptionReason.CommandError);
			}
			var query = new SeachQuery { LastInvokedAfter = tillTime };
			var results = HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(HistoryManager.Format(results), results);
		}

		[Command(Private, "history title", "Gets all songs which title contains <string>")]
		public JsonObject CommandHistoryTitle(string part)
		{
			var query = new SeachQuery { TitlePart = part };
			var results = HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(HistoryManager.Format(results), results);
		}

		[Command(AnyVisibility, "if")]
		[Usage("<argument0> <comparator> <argument1> <then>", "Compares the two arguments and returns or executes the then-argument")]
		[Usage("<argument0> <comparator> <argument1> <then> <else>", "Same as before and return the else-arguments if the condition is false")]
		public ICommandResult CommandIf(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			var argList = arguments.ToList();
			if (argList.Count < 4)
				throw new CommandException("Expected at least 4 arguments", CommandExceptionReason.MissingParameter);
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
			default: throw new CommandException("Unknown comparison operator", CommandExceptionReason.CommandError);
			}

			double d0, d1;
			bool cmpResult;
			// Try to parse arguments into doubles
			if (double.TryParse(arg0, NumberStyles.Number, CultureInfo.InvariantCulture, out d0)
				&& double.TryParse(arg1, NumberStyles.Number, CultureInfo.InvariantCulture, out d1))
				cmpResult = comparer(d0, d1);
			else
				cmpResult = comparer(string.CompareOrdinal(arg0, arg1), 0);

			// If branch
			if (cmpResult)
				return argList[3].Execute(info, Enumerable.Empty<ICommand>(), returnTypes);
			// Else branch
			if (argList.Count > 4)
				return argList[4].Execute(info, Enumerable.Empty<ICommand>(), returnTypes);

			// Try to return nothing
			if (returnTypes.Contains(CommandResultType.Empty))
				return new EmptyCommandResult();
			throw new CommandException("If found nothing to return", CommandExceptionReason.MissingParameter);
		}

		[Command(Private, "json merge", "Allows you to combine multiple JsonResults into one")]
		public JsonObject CommandJsonMerge(ExecutionInformation info, IEnumerable<ICommand> arguments)
		{
			if (!arguments.Any())
				return new JsonEmpty(string.Empty);

			var jsonArr = arguments
				.Select(arg => arg.Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.Json }))
				.Where(arg => arg.ResultType == CommandResultType.Json)
				.OfType<JsonCommandResult>()
				.Select(arg => arg.JsonObject.GetSerializeObject())
				.ToArray();

			return new JsonArray<object>(string.Empty, jsonArr);
		}

		[Command(Private, "kickme", "Guess what?")]
		[Usage("[far]", "Optional attribute for the extra punch strength")]
		[RequiredParameters(0)]
		public void CommandKickme(ExecutionInformation info, string parameter)
		{
			if (info.ApiCall)
				throw new CommandException("This command is not available as API", CommandExceptionReason.NotSupported);

			try
			{
				if (string.IsNullOrEmpty(parameter) || parameter == "near")
					QueryConnection.KickClientFromChannel(info.Session.Client.ClientId);
				else if (parameter == "far")
					QueryConnection.KickClientFromServer(info.Session.Client.ClientId);
			}
			catch (Ts3CommandException ex)
			{
				Log.Write(Log.Level.Info, "Could not kick: {0}", ex);
				throw new CommandException("I'm not strong enough, master!", ex, CommandExceptionReason.CommandError);
			}
		}

		[Command(Private, "link", "Gets a link to the origin of the current song.")]
		public JsonObject CommandLink(ExecutionInformation info)
		{
			if (PlayManager.CurrentPlayData == null)
				return new JsonEmpty("There is nothing on right now...");
			else if (QuizMode && PlayManager.CurrentPlayData.Invoker.ClientId != info.Session.Client.ClientId && !info.ApiCall)
				return new JsonEmpty("Sorry, you have to guess!");
			else
			{
				var link = FactoryManager.RestoreLink(PlayManager.CurrentPlayData.ResourceData);
				return new JsonSingleValue<string>(link, link);
			}
		}

		[Command(Private, "list add", "Adds a link to your private playlist.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public void CommandListAdd(ExecutionInformation info, string link)
		{
			var plist = AutoGetPlaylist(info.Session);
			var playResource = FactoryManager.Load(link).UnwrapThrow();
			plist.AddItem(new PlaylistItem(playResource.BaseData, new MetaData() { ResourceOwnerDbId = info.Session.Client.DatabaseId }));
		}

		[Command(Private, "list add", "<id> Adds a link to your private playlist from the history by <id>.")]
		public void CommandListAdd(ExecutionInformation info, uint hid)
		{
			var plist = AutoGetPlaylist(info.Session);

			if (!HistoryManager.GetEntryById(hid))
				throw new CommandException("History entry not found", CommandExceptionReason.CommandError);

			plist.AddItem(new PlaylistItem(hid, new MetaData() { ResourceOwnerDbId = info.Session.Client.DatabaseId }));
		}

		[Command(Private, "list clear", "Clears your private playlist.")]
		public void CommandListClear(ExecutionInformation info) => AutoGetPlaylist(info.Session).Clear();

		[Command(Private, "list delete", "<name> Deletes the playlist with the name <name>. You can only delete playlists which you also have created. Admins can delete every playlist.")]
		public JsonObject CommandListDelete(ExecutionInformation info, string name)
		{
			if (info.ApiCall)
				PlaylistManager.DeletePlaylist(name, info.Session.Client.DatabaseId, info.IsAdmin).UnwrapThrow();

			var hresult = PlaylistManager.LoadPlaylist(name, true);
			if (!hresult)
			{
				info.Session.SetResponse(ResponseListDelete, name);
				return new JsonEmpty($"Do you really want to delete the playlist \"{name}\" (error:{hresult.Message})");
			}
			else
			{
				if (hresult.Value.CreatorDbId.HasValue
					&& hresult.Value.CreatorDbId.Value != info.Session.Client.DatabaseId
					&& !info.IsAdmin)
					throw new CommandException("You are not allowed to delete others playlists", CommandExceptionReason.MissingRights);

				info.Session.SetResponse(ResponseListDelete, name);
				return new JsonEmpty($"Do you really want to delete the playlist \"{name}\"");
			}
		}

		[Command(Private, "list get", "<link> Imports a playlist form an other plattform like youtube etc.")]
		public JsonObject CommandListGet(ExecutionInformation info, string link)
		{
			var playlist = info.Session.Bot.PlaylistManager.LoadPlaylistFrom(link).UnwrapThrow();

			playlist.CreatorDbId = info.Session.Client.DatabaseId;
			info.Session.Set<PlaylistManager, Playlist>(playlist);
			return new JsonEmpty("Ok");
		}

		[Command(Private, "list item move", "<from> <to> Moves a item in a playlist <from> <to> position.")]
		public void CommandListMove(ExecutionInformation info, int from, int to)
		{
			var plist = AutoGetPlaylist(info.Session);

			if (from < 0 || from >= plist.Count
				|| to < 0 || to >= plist.Count)
				throw new CommandException("Index must be within playlist length", CommandExceptionReason.CommandError);

			if (from == to)
				return;

			var plitem = plist.GetResource(from);
			plist.RemoveItemAt(from);
			plist.InsertItem(plitem, Math.Min(to, plist.Count));
		}

		[Command(Private, "list item delete", "<index> Removes the item at <index>.")]
		public string CommandListRemove(ExecutionInformation info, int index)
		{
			var plist = AutoGetPlaylist(info.Session);

			if (index < 0 || index >= plist.Count)
				throw new CommandException("Index must be within playlist length", CommandExceptionReason.CommandError);

			var deletedItem = plist.GetResource(index);
			plist.RemoveItemAt(index);
			return "Removed: " + deletedItem.DisplayString;
		}

		// add list item rename

		[Command(Private, "list list", "Displays all available playlists from all users.")]
		[Usage("<pattern>", "Filters all lists cantaining the given pattern.")]
		[RequiredParameters(0)]
		public JsonObject CommandListList(ExecutionInformation info, string pattern)
		{
			var files = PlaylistManager.GetAvailablePlaylists(pattern).ToArray();
			if (!files.Any())
				return new JsonArray<string>("No playlists found", files);

			var strb = new StringBuilder();
			int tokenLen = 0;
			foreach (var file in files)
			{
				int newTokenLen = tokenLen + TS3Client.Commands.Ts3String.TokenLength(file) + 3;
				if (newTokenLen < TS3Client.Commands.Ts3String.MaxMsgLength)
				{
					strb.Append(file).Append(", ");
					tokenLen = newTokenLen;
				}
				else
					break;
			}

			if (strb.Length > 2)
				strb.Length -= 2;
			return new JsonArray<string>(strb.ToString(), files);
		}

		[Command(Private, "list load", "Opens a playlist to be editable for you. This replaces your current worklist with the opened playlist.")]
		public JsonObject CommandListLoad(ExecutionInformation info, string name)
		{
			Playlist loadList = AutoGetPlaylist(info.Session);

			var playList = PlaylistManager.LoadPlaylist(name).UnwrapThrow();

			loadList.Clear();
			loadList.AddRange(playList.AsEnumerable());
			loadList.Name = playList.Name;
			return new JsonSingleObject<Playlist>($"Loaded: \"{name}\" with {loadList.Count} songs", loadList);
		}

		[Command(Private, "list merge", "Appends another playlist to yours.")]
		public void CommandListMerge(ExecutionInformation info, string name)
		{
			var plist = AutoGetPlaylist(info.Session);

			var lresult = PlaylistManager.LoadPlaylist(name);
			if (!lresult)
				throw new CommandException("The other playlist could not be found", CommandExceptionReason.CommandError);

			plist.AddRange(lresult.Value.AsEnumerable());
		}

		[Command(Private, "list name", "Displays the name of the playlist you are currently working on.")]
		[Usage("<name>", "Changes the playlist name to <name>.")]
		public JsonObject CommandListName(ExecutionInformation info, string name)
		{
			var plist = AutoGetPlaylist(info.Session);

			if (string.IsNullOrEmpty(name))
				return new JsonSingleValue<string>(plist.Name, plist.Name);

			PlaylistManager.IsNameValid(name).UnwrapThrow();

			plist.Name = name;
			return null;
		}

		[Command(Private, "list play", "Replaces the current freelist with your workinglist and plays from the beginning.")]
		[Usage("<index>", "Lets you specify the starting song index.")]
		[RequiredParameters(0)]
		public void CommandListPlay(ExecutionInformation info, int? index)
		{
			var plist = AutoGetPlaylist(info.Session);

			if (!index.HasValue || (index.Value >= 0 && index.Value < plist.Count))
			{
				PlaylistManager.PlayFreelist(plist);
				PlaylistManager.Index = index ?? 0;
			}
			else
				throw new CommandException("Invalid starting index", CommandExceptionReason.CommandError);

			PlaylistItem item = PlaylistManager.Current();
			if (item != null)
				PlayManager.Play(new InvokerData(info.Session.Client), item).UnwrapThrow();
			else
				throw new CommandException("Nothing to play...", CommandExceptionReason.CommandError);
		}

		[Command(Private, "list save", "Stores your current workinglist to disk.")]
		[Usage("<name>", "Changes the playlist name to <name> before saving.")]
		[RequiredParameters(0)]
		public JsonObject CommandListSave(ExecutionInformation info, string optNewName)
		{
			var plist = AutoGetPlaylist(info.Session);
			if (!string.IsNullOrEmpty(optNewName))
			{
				PlaylistManager.IsNameValid(optNewName).UnwrapThrow();
				plist.Name = optNewName;
			}

			PlaylistManager.SavePlaylist(plist).UnwrapThrow();
			return new JsonEmpty("Ok");
		}

		[Command(Private, "list show", "Displays all songs currently in the playlists you are working on")]
		[Usage("<index>", "Lets you specify the staring index from which songs should be listed.")]
		[RequiredParameters(0)]
		public JsonObject CommandListShow(ExecutionInformation info, int? offset) => CommandListShow(info, null, offset);

		[Command(Private, "list show", "<name> Displays all songs currently in the playlists with the name <name>")]
		[Usage("name> <index>", "Lets you specify the starting index from which songs should be listed.")]
		[RequiredParameters(0)]
		public JsonObject CommandListShow(ExecutionInformation info, string name, int? offset)
		{
			Playlist plist;
			if (!string.IsNullOrEmpty(name))
				plist = PlaylistManager.LoadPlaylist(name).UnwrapThrow();
			else
				plist = AutoGetPlaylist(info.Session);

			var strb = new StringBuilder();
			strb.Append($"Playlist: \"").Append(plist.Name).Append("\" with ").Append(plist.Count).AppendLine(" songs.");
			int from = Math.Max(offset ?? 0, 0);
			var items = plist.AsEnumerable().Skip(from).ToArray();
			foreach (var plitem in items.Take(10))
				strb.Append(from++).Append(": ").AppendLine(plitem.DisplayString);

			return new JsonArray<PlaylistItem>(strb.ToString(), items);
		}

		[Command(Private, "loop", "Gets or sets whether or not to loop the entire playlist.")]
		public JsonObject CommandLoop() => new JsonSingleValue<bool>("Loop is " + (PlaylistManager.Loop ? "on" : "off"), PlaylistManager.Loop);
		[Command(Private, "loop on", "Gets or sets whether or not to loop the entire playlist.")]
		public void CommandLoopOn() => PlaylistManager.Loop = true;
		[Command(Private, "loop off", "Gets or sets whether or not to loop the entire playlist.")]
		public void CommandLoopOff() => PlaylistManager.Loop = false;

		[Command(Private, "next", "Plays the next song in the playlist.")]
		public void CommandNext(ExecutionInformation info)
		{
			PlayManager.Next(new InvokerData(info.Session.Client)).UnwrapThrow();
		}

		[Command(Public, "pm", "Requests a private session with the ServerBot so you can invoke private commands.")]
		public string CommandPM(ExecutionInformation info)
		{
			if (info.ApiCall)
				throw new CommandException("This command is not available as API", CommandExceptionReason.NotSupported);
			info.IsPrivate = true;
			return "Hi " + info.TextMessage.InvokerName;
		}

		[Command(Admin, "parse", "Displays the AST of the requested command.")]
		[Usage("<command>", "The comand to be parsed")]
		public JsonObject CommandParse(string parameter)
		{
			if (!parameter.TrimStart().StartsWith("!", StringComparison.Ordinal))
				throw new CommandException("This is not a command", CommandExceptionReason.CommandError);
			try
			{
				var node = CommandParser.ParseCommandRequest(parameter);
				var strb = new StringBuilder();
				strb.AppendLine();
				node.Write(strb, 0);
				return new JsonSingleObject<ASTNode>(strb.ToString(), node);
			}
			catch (Exception ex)
			{
				throw new CommandException("GJ - You crashed it!!!", ex, CommandExceptionReason.CommandError);
			}
		}

		[Command(Private, "pause", "Well, pauses the song. Undo with !play.")]
		public void CommandPause() => AudioFramework.Pause = true;

		[Command(Private, "play", "Automatically tries to decide whether the link is a special resource (like youtube) or a direct resource (like ./hello.mp3) and starts it.")]
		[Usage("<link>", "Youtube, Soundcloud, local path or file link")]
		[RequiredParameters(0)]
		public void CommandPlay(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				AudioFramework.Pause = false;
			else
				PlayManager.Play(new InvokerData(info.Session.Client), parameter).UnwrapThrow();
		}

		[Command(Admin, "plugin list", "Lists all found plugins.")]
		public string CommandPluginList()
		{
			return PluginManager.GetPluginOverview(); // TODO Api callcable
		}

		[Command(Admin, "plugin unload", "Unloads a plugin.")]
		public JsonObject CommandPluginUnload(string identifier)
		{
			string ret = PluginManager.UnloadPlugin(identifier).ToString();
			return new JsonSingleValue<string>(ret, ret);
		}

		[Command(Admin, "plugin load", "Unloads a plugin.")]
		public void CommandPluginLoad(string identifier)
			=> PluginManager.LoadPlugin(identifier).UnwrapThrow();

		[Command(Private, "previous", "Plays the previous song in the playlist.")]
		public void CommandPrevious(ExecutionInformation info)
			=> PlayManager.Previous(new InvokerData(info.Session.Client)).UnwrapThrow();

		[Command(AnyVisibility, "print", "Lets you format multiple parameter to one.")]
		[RequiredParameters(0)]
		public JsonObject CommandPrint(params string[] parameter)
		{
			// << Desing changes expected >>
			var strb = new StringBuilder();
			foreach (var param in parameter)
				strb.Append(param);
			return new JsonSingleValue<string>(strb.ToString(), strb.ToString());
		}

		[Command(Admin, "quit", "Closes the TS3AudioBot application.")]
		[RequiredParameters(0)]
		public string CommandQuit(ExecutionInformation info, string param)
		{
			if (info.ApiCall)
			{
				Dispose();
				return null;
			}

			if (param == "force")
			{
				QueryConnection.OnMessageReceived -= TextCallback;
				Dispose();
				return null;
			}
			else
			{
				info.Session.SetResponse(ResponseQuit, null);
				return "Do you really want to quit? !(yes|no)";
			}
		}

		[Command(Public, "quiz", "Shows the quizmode status.")]
		public JsonObject CommandQuiz() => new JsonSingleValue<bool>("Quizmode is " + (QuizMode ? "on" : "off"), QuizMode);
		[Command(Public, "quiz on", "Enable to hide the songnames and let your friends guess the title.")]
		public void CommandQuizOn()
		{
			QuizMode = true;
			UpdateBotStatus().UnwrapThrow();
		}
		[Command(Public, "quiz off", "Disable to show the songnames again.")]
		public void CommandQuizOff(ExecutionInformation info)
		{
			if (info.IsPrivate && !info.ApiCall)
				throw new CommandException("No cheatig! Everybody has to see it!", CommandExceptionReason.CommandError);
			QuizMode = false;
			UpdateBotStatus().UnwrapThrow();
		}

		[Command(Private, "random", "Gets whether or not to play playlists in random order.")]
		public JsonObject CommandRandom() => new JsonSingleValue<bool>("Random is " + (PlaylistManager.Random ? "on" : "off"), PlaylistManager.Random);
		[Command(Private, "random on", "Enables random playlist playback")]
		public void CommandRandomOn() => PlaylistManager.Random = true;
		[Command(Private, "random off", "Disables random playlist playback")]
		public void CommandRandomOff() => PlaylistManager.Random = false;
		[Command(Private, "random seed", "Gets the unique seed for a certain playback order")]
		public JsonObject CommandRandomSeed()
		{
			string seed = Util.FromSeed(PlaylistManager.Seed);
			string strseed = string.IsNullOrEmpty(seed) ? "<empty>" : seed;
			return new JsonSingleValue<string>(strseed, strseed);
		}
		[Command(Private, "random seed", "Sets the unique seed for a certain playback order")]
		public void CommandRandomSeed(string newSeed)
		{
			if (newSeed.Any(c => !char.IsLetter(c)))
				throw new CommandException("Only letters allowed", CommandExceptionReason.CommandError);
			PlaylistManager.Seed = Util.ToSeed(newSeed.ToLowerInvariant());
		}
		[Command(Private, "random seed", "Sets the unique seed for a certain playback order")]
		public void CommandRandomSeed(int newSeed) => PlaylistManager.Seed = newSeed;

		[Command(Private, "repeat", "Gets or sets whether or not to loop a single song.")]
		public JsonObject CommandRepeat() => new JsonSingleValue<bool>("Repeat is " + (AudioFramework.Repeat ? "on" : "off"), AudioFramework.Repeat);
		[Command(Private, "repeat on", "Enables single song repeat.")]
		public void CommandRepeatOn() => AudioFramework.Repeat = true;
		[Command(Private, "repeat off", "Disables single song repeat.")]
		public void CommandRepeatOff() => AudioFramework.Repeat = false;

		[Command(AnyVisibility, "rng", "Gets a random number.")]
		[Usage("", "Gets a number between 0 and 2147483647")]
		[Usage("<max>", "Gets a number between 0 and <max>")]
		[Usage("<min> <max>", "Gets a number between <min> and <max>")]
		[RequiredParameters(0)]
		public JsonObject CommandRng(int? first, int? second)
		{
			int num;
			if (second.HasValue)
				num = Util.Random.Next(Math.Min(first.Value, second.Value), Math.Max(first.Value, second.Value));
			else if (first.HasValue)
			{
				if (second.Value < first.Value)
					throw new CommandException("Value must be 0 or positive", CommandExceptionReason.CommandError);
				num = Util.Random.Next(first.Value);
			}
			else
				num = Util.Random.Next();
			return new JsonSingleValue<int>(num.ToString(CultureInfo.InvariantCulture), num);
		}

		[Command(Private, "seek", "Jumps to a timemark within the current song.")]
		[Usage("<sec>", "Time in seconds")]
		[Usage("<min:sec>", "Time in Minutes:Seconds")]
		public void CommandSeek(ExecutionInformation info, string parameter)
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
				throw new CommandException("The time was not in a correct format, see !help seek for more information.", CommandExceptionReason.CommandError);
			else if (span < TimeSpan.Zero || span > AudioFramework.Length)
				throw new CommandException("The point of time is not within the songlenth.", CommandExceptionReason.CommandError);
			else
				AudioFramework.Position = span;
		}

		[Command(Admin, "settings", "Changes values from the settigns. Not all changes can be applied immediately.")]
		[Usage("<key>", "Get the value of a setting")]
		[Usage("<key> <value>", "Set the value of a setting")]
		[RequiredParameters(0)]
		public JsonObject CommandSettings(ExecutionInformation info, string key, string value)
		{
			var configMap = ConfigManager.GetConfigMap();
			if (string.IsNullOrEmpty(key))
				throw new CommandException("Please specify a key like: \n  " + string.Join("\n  ", configMap.Take(3).Select(kvp => kvp.Key)),
					CommandExceptionReason.MissingParameter);

			var filtered = XCommandSystem.FilterList(configMap, key);
			var filteredArr = filtered.ToArray();

			if (filteredArr.Length == 0)
			{
				throw new CommandException("No config key matching the pattern found", CommandExceptionReason.CommandError);
			}
			else if (filteredArr.Length == 1)
			{
				if (string.IsNullOrEmpty(value))
				{
					return new JsonSingleObject<KeyValuePair<string, string>>(filteredArr[0].Key + " = " + filteredArr[0].Value, filteredArr[0]);
				}
				else
				{
					var result = ConfigManager.SetSetting(filteredArr[0].Key, value);
					if (result.Ok) return null;
					else throw new CommandException(result.Message, CommandExceptionReason.CommandError);
				}
			}
			else
			{
				throw new CommandException("Found more than one matching key: \n  " + string.Join("\n  ", filteredArr.Take(3).Select(kvp => kvp.Key)),
					CommandExceptionReason.CommandError);
			}
		}

		[Command(AnyVisibility, "song", "Tells you the name of the current song.")]
		public JsonObject CommandSong(ExecutionInformation info)
		{
			if (PlayManager.CurrentPlayData == null)
				return new JsonEmpty("There is nothing on right now...");
			else if (QuizMode && PlayManager.CurrentPlayData.Invoker.ClientId != info.Session.Client.ClientId && !info.ApiCall)
				return new JsonEmpty("Sorry, you have to guess!");
			else
				return new JsonSingleValue<string>(
					$"[url={FactoryManager.RestoreLink(PlayManager.CurrentPlayData.ResourceData)}]{PlayManager.CurrentPlayData.ResourceData.ResourceTitle}[/url]",
					PlayManager.CurrentPlayData.ResourceData.ResourceTitle);
		}

		[Command(Private, "stop", "Stops the current song.")]
		public void CommandStop()
		{
			AudioFramework.Stop();
		}

		[Command(Private, "subscribe", "Lets you hear the music independent from the channel you are in.")]
		public void CommandSubscribe(ExecutionInformation info)
		{
			TargetManager.WhisperClientSubscribe(info.Session.Client.ClientId);
		}

		[Command(Admin, "subscribe channel", "Adds your current channel to the music playback.")]
		public void CommandSubscribeChannel(ExecutionInformation info)
		{
			TargetManager.WhisperChannelSubscribe(info.Session.Client.ChannelId, true);
		}

		[Command(AnyVisibility, "take", "Take a substring from a string.")]
		[Usage("<count> <text>", "Take only <count> parts of the text")]
		[Usage("<count> <start> <text>", "Take <count> parts, starting with the part at <start>")]
		[Usage("<count> <start> <delimiter> <text>", "Specify another delimiter for the parts than spaces")]
		public ICommandResult CommandTake(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			var argList = arguments.ToList();

			if (argList.Count < 2)
				throw new CommandException("Expected at least 2 parameters", CommandExceptionReason.MissingParameter);

			int start = 0;
			int count = 0;
			string delimiter = null;

			// Get count
			var res = ((StringCommandResult)argList[0].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
			if (!int.TryParse(res, out count) || count < 0)
				throw new CommandException("Count must be an integer >= 0", CommandExceptionReason.CommandError);

			if (argList.Count > 2)
			{
				// Get start
				res = ((StringCommandResult)argList[1].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
				if (!int.TryParse(res, out start) || start < 0)
					throw new CommandException("Start must be an integer >= 0", CommandExceptionReason.CommandError);
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
				throw new CommandException("Not enough arguments to take", CommandExceptionReason.CommandError);
			var splittedarr = splitted.Skip(start).Take(count).ToArray();

			foreach (var returnType in returnTypes)
			{
				if (returnType == CommandResultType.String)
					return new StringCommandResult(string.Join(delimiter ?? " ", splittedarr));
				else if (returnType == CommandResultType.Json)
					return new JsonCommandResult(new JsonArray<string>(string.Join(delimiter ?? " ", splittedarr), splittedarr));
			}

			throw new CommandException("Can't find a fitting return type for take", CommandExceptionReason.NoReturnMatch);
		}

		[Command(Admin, "test", "Only for debugging purposes.")]
		public JsonObject CommandTest(ExecutionInformation info, string privet)
		{
			//  & !info.Session.IsPrivate
			if (privet == "err")
				throw new CommandException("Please use as private, admins too!", CommandExceptionReason.CommandError);
			else
			{
				//info.Session.Write("Good boy!");
				// stresstest
				//for (int i = 0; i < 10; i++)
				//	info.Session.Write(i.ToString(CultureInfo.InvariantCulture));
				return new JsonTest("Please enable json in settings, wait what?");
			}
		}

		public class JsonTest : JsonObject
		{
			public int Num { get; set; } = 42;
			public string Awesome { get; set; } = "Nehmen Sie AWESOME!!!";

			public JsonTest(string msgval) : base(msgval)
			{

			}
		}

		[Command(Private, "unsubscribe", "Only lets you hear the music in active channels again.")]
		public void CommandUnsubscribe(ExecutionInformation info)
		{
			TargetManager.WhisperClientUnsubscribe(info.Session.Client.ClientId);
		}

		[Command(Private, "unsubscribe channel", "Removes your current channel from the music playback.")]
		public void CommandUnsubscribeChannel(ExecutionInformation info)
		{
			TargetManager.WhisperChannelUnsubscribe(info.Session.Client.ChannelId, true);
		}

		[Command(AnyVisibility, "volume", "Sets the volume level of the music.")]
		[Usage("<level>", "A new volume level between 0 and 100.")]
		[Usage("+/-<level>", "Adds or subtracts a value form the current volume.")]
		[RequiredParameters(0)]
		public JsonObject CommandVolume(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				return new JsonSingleValue<int>("Current volume: " + AudioFramework.Volume, AudioFramework.Volume);

			bool relPos = parameter.StartsWith("+", StringComparison.Ordinal);
			bool relNeg = parameter.StartsWith("-", StringComparison.Ordinal);
			string numberString = (relPos || relNeg) ? parameter.Remove(0, 1) : parameter;

			int volume;
			if (!int.TryParse(numberString, out volume))
				throw new CommandException("The new volume could not be parsed", CommandExceptionReason.CommandError);

			int newVolume;
			if (relPos) newVolume = AudioFramework.Volume + volume;
			else if (relNeg) newVolume = AudioFramework.Volume - volume;
			else newVolume = volume;

			if (newVolume < 0 || newVolume > AudioFramework.MaxVolume)
				throw new CommandException("The volume level must be between 0 and " + AudioFramework.MaxVolume, CommandExceptionReason.CommandError);

			if (newVolume <= AudioFramework.MaxUserVolume || newVolume < AudioFramework.Volume || info.ApiCall)
				AudioFramework.Volume = newVolume;
			else if (newVolume <= AudioFramework.MaxVolume)
			{
				info.Session.SetResponse(ResponseVolume, newVolume);
				return new JsonEmpty("Careful you are requesting a very high volume! Do you want to apply this? !(yes|no)");
			}
			return null;
		}

		#endregion

		#region RESPONSES

		private string ResponseVolume(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage.Message);
			if (answer == Answer.Yes)
			{
				if (info.IsAdmin)
				{
					var respInt = info.Session.ResponseData as int?;
					if (!respInt.HasValue)
					{
						Log.Write(Log.Level.Error, "responseData is not an int.");
						return "Internal error";
					}
					AudioFramework.Volume = respInt.Value;
				}
				else
				{
					return "Command can only be answered by an admin.";
				}
			}
			return null;
		}

		private string ResponseQuit(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage.Message);
			if (answer == Answer.Yes)
			{
				if (info.IsAdmin)
					CommandQuit(info, "force");
				else
					return "Command can only be answered by an admin.";
			}
			return null;
		}

		private string ResponseHistoryDelete(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage.Message);
			if (answer == Answer.Yes)
			{
				if (info.IsAdmin)
				{
					var ale = info.Session.ResponseData as AudioLogEntry;
					if (ale == null)
					{
						Log.Write(Log.Level.Error, "No entry provided.");
						return "Internal error";
					}
					HistoryManager.RemoveEntry(ale);
				}
				else
				{
					return "Command can only be answered by an admin.";
				}
			}
			return null;
		}

		private string ResponseHistoryClean(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage.Message);
			if (answer == Answer.Yes)
			{
				if (info.IsAdmin)
				{
					string param = info.Session.ResponseData as string;
					if (string.IsNullOrEmpty(param))
					{
						HistoryManager.CleanHistoryFile();
						return "Cleanup done!";
					}
					else if (param == "removedefective")
					{
						HistoryManager.RemoveBrokenLinks(info.Session);
						return "Cleanup done!";
					}
					else
						return "Unknown parameter!";
				}
				else
					return "Command can only be answered by an admin.";
			}
			return null;
		}

		private string ResponseListDelete(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage.Message);
			if (answer == Answer.Yes)
			{
				var name = info.Session.ResponseData as string;
				var result = PlaylistManager.DeletePlaylist(name, info.Session.Client.DatabaseId, info.IsAdmin);
				if (!result) return result.Message;
				else return "Ok";
			}
			return null;
		}

		#endregion

		private void LoggedUpdateBotStatus(object sender, EventArgs e)
		{
			var result = UpdateBotStatus();
			if (!result)
				Log.Write(Log.Level.Warning, result.Message);
		}

		private R UpdateBotStatus(string overrideStr = null)
		{
			string setString;
			if (overrideStr != null)
			{
				setString = overrideStr;
			}
			else if (PlayManager.IsPlaying)
			{
				if (QuizMode)
					setString = "<Quiztime!>";
				else
					setString = PlayManager.CurrentPlayData.ResourceData.ResourceTitle;
			}
			else
			{
				setString = "<Sleeping>";
			}
			return QueryConnection.ChangeDescription(setString);
		}

		private Playlist AutoGetPlaylist(UserSession session)
		{
			var result = session.Get<PlaylistManager, Playlist>();
			if (result)
				return result.Value;

			var newPlist = new Playlist(session.Client.NickName, session.Client.DatabaseId);
			session.Set<PlaylistManager, Playlist>(newPlist);
			return newPlist;
		}

		public void Dispose()
		{
			if (!isDisposed) isDisposed = true;
			else return;
			Log.Write(Log.Level.Info, "Exiting...");

			WebManager?.Dispose(); // before: logStream,
			WebManager = null;

			PluginManager?.Dispose(); // before: SessionManager, logStream,
			PluginManager = null;

			AudioFramework.Dispose(); // before: logStream,
			AudioFramework = null;

			QueryConnection.Dispose(); // before: logStream,
			QueryConnection = null;

			HistoryManager.Dispose(); // before: logStream,
			HistoryManager = null;

			TickPool.Close(); // before:

			FactoryManager.Dispose(); // before:
			FactoryManager = null;

			logStream.Dispose();  // before:
			logStream = null;
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
	class MainBotData : ConfigData
	{
		[Info("Path to the logfile", "log_ts3audiobot")]
		public string LogFile { get; set; }
		[Info("Teamspeak group id authorized to execute admin commands")]
		public ulong AdminGroupId { get; set; }
		[Info("Teamspeak group id giving the Bot enough power to do his job", "0")]
		public ulong BotGroupId { get; set; }
	}
#pragma warning restore CS0649
}
