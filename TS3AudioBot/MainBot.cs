// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Drawing;
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
	using Rights;

	using TS3Client;
	using TS3Client.Messages;

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
		private string configFilePath;
		internal MainBotData mainBotData;

		private StreamWriter logStream;

		private TargetScript TargetScript { get; set; }
		internal PluginManager PluginManager { get; private set; }
		public CommandManager CommandManager { get; private set; }
		public PlaylistManager PlaylistManager { get; private set; }
		public TeamspeakControl QueryConnection { get; private set; }
		public SessionManager SessionManager { get; private set; }
		private HistoryManager historyManager = null;
		public HistoryManager HistoryManager => historyManager ?? throw new CommandException("History has not been enabled", CommandExceptionReason.NotSupported);
		public ResourceFactoryManager FactoryManager { get; private set; }
		public WebManager WebManager { get; private set; }
		public PlayManager PlayManager { get; private set; }
		public ITargetManager TargetManager { get; private set; }
		public IPlayerConnection PlayerConnection { get; private set; }
		public ConfigFile ConfigManager { get; private set; }
		public RightsManager RightsManager { get; private set; }

		public bool QuizMode { get; set; }

		public MainBot()
		{
			// setting defaults
			isDisposed = false;
			consoleOutput = true;
			writeLog = true;
			writeLogStack = false;
			configFilePath = "configTS3AudioBot.cfg";
		}

		private bool ReadParameter(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i])
				{
				case "-h":
				case "--help":
					Console.WriteLine(" --Quiet  -q         Deactivates all output to stdout.");
					Console.WriteLine(" --NoLog  -L         Deactivates writing to the logfile.");
					Console.WriteLine(" --Stack  -s         Adds the stacktrace to all log writes.");
					Console.WriteLine(" --Config -c <file>  Specifies the path to the config file.");
					Console.WriteLine(" --help   -h         Prints this help....");
					return false;

				case "-q":
				case "--Quiet":
					consoleOutput = false;
					break;

				case "-L":
				case "--NoLog":
					writeLog = false;
					break;

				case "-s":
				case "--Stack":
					writeLogStack = true;
					break;

				case "-c":
				case "--Config":
					if (i >= args.Length - 1)
					{
						Console.WriteLine("No config file specified after \"{0}\"", args[i]);
						return false;
					}
					configFilePath = args[++i];
					break;

				default:
					Console.WriteLine("Unrecognized parameter: {0}", args[i]);
					return false;
				}
			}
			return true;
		}

		private bool InitializeBot()
		{
			// Read Config File
			ConfigManager = ConfigFile.OpenOrCreate(configFilePath) ?? ConfigFile.CreateDummy();
			var afd = ConfigManager.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			var tfcd = ConfigManager.GetDataStruct<Ts3FullClientData>("QueryConnection", true);
			var hmd = ConfigManager.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			var pmd = ConfigManager.GetDataStruct<PluginManagerData>("PluginManager", true);
			var pld = ConfigManager.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			var yfd = ConfigManager.GetDataStruct<YoutubeFactoryData>("YoutubeFactory", true);
			var webd = ConfigManager.GetDataStruct<WebData>("WebData", true);
			var rmd = ConfigManager.GetDataStruct<RightsManagerData>("RightsManager", true);
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
			Log.Write(Log.Level.Info, "[=== Date/Time: {0} {1}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString());
			Log.Write(Log.Level.Info, "[=== Version: {0}", Util.GetAssemblyData().ToString());
			Log.Write(Log.Level.Info, "[==============================================]");

			Log.Write(Log.Level.Info, "[============ Initializing Commands ===========]");
			CommandManager = new CommandManager();
			CommandManager.RegisterMain(this);

			Log.Write(Log.Level.Info, "[============ Initializing Modules ============]");
			AudioValues.audioFrameworkData = afd;
			var teamspeakClient = new Ts3Full(tfcd);
			QueryConnection = teamspeakClient;
			PlayerConnection = teamspeakClient;
			PlaylistManager = new PlaylistManager(pld);
			SessionManager = new SessionManager();
			if (hmd.EnableHistory)
				historyManager = new HistoryManager(hmd);
			PluginManager = new PluginManager(this, pmd);
			PlayManager = new PlayManager(this);
			WebManager = new WebManager(this, webd);
			RightsManager = new RightsManager(this, rmd);
			TargetManager = teamspeakClient;
			TargetScript = new TargetScript(this);

			Log.Write(Log.Level.Info, "[=========== Initializing Factories ===========]");
			YoutubeDlHelper.DataObj = yfd;
			FactoryManager = new ResourceFactoryManager();
			var mediaFactory = new MediaFactory();
			FactoryManager.AddFactory(mediaFactory, CommandManager);
			FactoryManager.AddFactory(new YoutubeFactory(yfd), CommandManager);
			FactoryManager.AddFactory(new SoundcloudFactory(), CommandManager);
			FactoryManager.AddFactory(new TwitchFactory(), CommandManager);
			FactoryManager.DefaultFactorty = mediaFactory;

			Log.Write(Log.Level.Info, "[=========== Registering callbacks ============]");
			PlayerConnection.OnSongEnd += PlayManager.SongStoppedHook;
			PlayManager.BeforeResourceStarted += TargetScript.BeforeResourceStarted;
			// In own favor update the own status text to the current song title
			PlayManager.AfterResourceStarted += LoggedUpdateBotStatus;
			PlayManager.AfterResourceStopped += LoggedUpdateBotStatus;
			// Log our resource in the history
			if (hmd.EnableHistory)
				PlayManager.AfterResourceStarted += (s, e) => HistoryManager.LogAudioResource(new HistorySaveData(e.PlayResource.BaseData, e.Owner));
			// Update our thumbnail
			PlayManager.AfterResourceStarted += GenerateStatusImage;
			PlayManager.AfterResourceStopped += GenerateStatusImage;
			// Register callback for all messages happening
			QueryConnection.OnMessageReceived += TextCallback;
			// Register callback to remove open private sessions, when user disconnects
			QueryConnection.OnClientDisconnect += OnClientDisconnect;

			Log.Write(Log.Level.Info, "[================= Finalizing =================]");
			RightsManager.RegisterRights(CommandManager.AllRights);
			RightsManager.RegisterRights(rightHighVolume, rightDeleteAllPlaylists);
			if (!RightsManager.ReadFile())
				return false;
			WebManager.StartServerAsync();

			Log.Write(Log.Level.Info, "[==================== Done ====================]");
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

		private void TextCallback(object sender, TextMessage textMessage)
		{
			Log.Write(Log.Level.Debug, "MB Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			textMessage.Message = textMessage.Message.TrimStart(new[] { ' ' });
			if (!textMessage.Message.StartsWith("!", StringComparison.Ordinal))
				return;

			var refreshResult = QueryConnection.RefreshClientBuffer(true);
			if (!refreshResult.Ok)
				Log.Write(Log.Level.Warning, "Bot is not correctly set up. Some requests might fail or are slower. ({0})", refreshResult.Message);

			var clientResult = QueryConnection.GetClientById(textMessage.InvokerId);

			// get the current session
			UserSession session;
			var result = SessionManager.GetSession(textMessage.InvokerId);
			if (result.Ok)
			{
				session = result.Value;
			}
			else
			{
				if (!clientResult.Ok)
				{
					Log.Write(Log.Level.Error, clientResult.Message);
					return;
				}
				session = SessionManager.CreateSession(this, clientResult.Value);
			}

			using (session.GetLock())
			{
				var invoker = new InvokerData(textMessage.InvokerUid)
				{
					ClientId = textMessage.InvokerId,
					IsApi = false,
					Visibiliy = textMessage.Target,
					NickName = textMessage.InvokerName,
				};
				if (clientResult.Ok)
				{
					invoker.ChannelId = clientResult.Value.ChannelId;
					invoker.DatabaseId = clientResult.Value.DatabaseId;
				}
				var execInfo = new ExecutionInformation(this, invoker, textMessage.Message, session);

				// check if the user has an open request
				if (session.ResponseProcessor != null)
				{
					var msg = session.ResponseProcessor(execInfo);
					session.ClearResponse();
					if (!string.IsNullOrEmpty(msg))
						execInfo.Write(msg);
					return;
				}

				try
				{
					// parse (and execute) the command
					var res = CommandManager.CommandSystem.Execute(execInfo, textMessage.Message);
					// Write result to user
					if (res.ResultType == CommandResultType.String)
					{
						var sRes = (StringCommandResult)res;
						if (!string.IsNullOrEmpty(sRes.Content))
							execInfo.Write(sRes.Content);
					}
					else if (res.ResultType == CommandResultType.Json)
					{
						var sRes = (JsonCommandResult)res;
						execInfo.Write("\nJson str: \n" + sRes.JsonObject.AsStringResult);
						execInfo.Write("\nJson val: \n" + Util.Serializer.Serialize(sRes.JsonObject));
					}
				}
				catch (CommandException ex)
				{
					execInfo.Write("Error: " + ex.Message);
				}
				catch (Exception ex)
				{
					Log.Write(Log.Level.Error, "MB Unexpected command error: {0}", ex.UnrollException());
					execInfo.Write("An unexpected error occured: " + ex.Message);
				}
			}
		}

		private void OnClientDisconnect(object sender, ClientLeftView eventArgs)
		{
			TargetManager.WhisperClientUnsubscribe(eventArgs.ClientId);
			SessionManager.RemoveSession(eventArgs.ClientId);
		}

		#region COMMANDS

		const string rightHighVolume = "ts3ab.admin.volume";
		const string rightDeleteAllPlaylists = "ts3ab.admin.list";

		// [...] = Optional
		// <name> = Placeholder for a text
		// [text] = Option for fixed text
		// (a|b) = either or switch

		[Command("add", "Adds a new song to the queue.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public void CommandAdd(ExecutionInformation info, string parameter)
			=> PlayManager.Enqueue(info.InvokerData, parameter).UnwrapThrow();

		[Command("api token", "Generates an api token.")]
		public JsonObject CommandApiToken(ExecutionInformation info)
		{
			if (info.InvokerData.Visibiliy.HasValue && info.InvokerData.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("Please use this command in a private session.", CommandExceptionReason.CommandError);
			if (info.InvokerData.ClientUid == null)
				throw new CommandException("No Uid found to register token for.", CommandExceptionReason.CommandError);
			var token = SessionManager.GenerateToken(info.InvokerData.ClientUid).UnwrapThrow();
			return new JsonSingleValue<string>(token);
		}

		[Command("api nonce", "Generates an api nonce.")]
		public JsonObject CommandApiNonce(ExecutionInformation info)
		{
			if (info.InvokerData.Visibiliy.HasValue && info.InvokerData.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("Please use this command in a private session.", CommandExceptionReason.CommandError);
			if (info.InvokerData.ClientUid == null)
				throw new CommandException("No Uid found to register token for.", CommandExceptionReason.CommandError);
			var result = SessionManager.GetToken(info.InvokerData.ClientUid);
			if (!result.Ok)
				throw new CommandException("No active token found.", CommandExceptionReason.CommandError);

			var nonce = result.Value.CreateNonce();
			return new JsonSingleValue<string>(nonce.Value);
		}

		[Command("bot commander", "Gets the status of the channel commander mode.")]
		public JsonObject CommandBotCommander()
		{
			var value = QueryConnection.IsChannelCommander().UnwrapThrow();
			return new JsonSingleValue<bool>("Channel commander is " + (value ? "on" : "off"), value);
		}
		[Command("bot commander on", "Enables channel commander.")]
		public void CommandBotCommanderOn() => QueryConnection.SetChannelCommander(true);
		[Command("bot commander off", "Disables channel commander.")]
		public void CommandBotCommanderOff() => QueryConnection.SetChannelCommander(false);

		[Command("bot move", "Moves the bot to you or a specified channel.")]
		[RequiredParameters(0)]
		public void CommandBotMove(ExecutionInformation info, ulong? channel)
		{
			if (!channel.HasValue)
				channel = (CommandGetChannel(info) as JsonSingleValue<ulong>)?.Value;
			if (!channel.HasValue)
				throw new CommandException("No target channel found");
			QueryConnection.MoveTo(channel.Value).UnwrapThrow();
		}

		[Command("bot name", "Gives the bot a new name.")]
		public void CommandBotName(string name) => QueryConnection.ChangeName(name).UnwrapThrow();

		[Command("bot setup", "Sets all teamspeak rights for the bot to be fully functional.")]
		[RequiredParameters(0)]
		public void CommandBotSetup(string adminToken)
		{
			QueryConnection.SetupRights(adminToken, mainBotData).UnwrapThrow();
		}

		[Command("clear", "Removes all songs from the current playlist.")]
		public void CommandClear()
		{
			PlaylistManager.ClearFreelist();
		}

		[Command("eval", "Executes a given command or string")]
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

		[Command("getuser uid", "Gets the unique Id of a user.")]
		public JsonObject CommandGetUid(ExecutionInformation info)
			=> info.InvokerData.ClientUid != null
			? new JsonSingleValue<string>(info.InvokerData.ClientUid)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser name", "Gets the Nickname of a user.")]
		public JsonObject CommandGetName(ExecutionInformation info)
			=> info.InvokerData.NickName != null
			? new JsonSingleValue<string>(info.InvokerData.NickName)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser dbid", "Gets the DatabaseId of a user.")]
		public JsonObject CommandGetDbId(ExecutionInformation info)
			=> info.InvokerData.DatabaseId.HasValue
			? new JsonSingleValue<ulong>(info.InvokerData.DatabaseId.Value)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser channel", "Gets the ChannelId a user is currently in.")]
		public JsonObject CommandGetChannel(ExecutionInformation info)
			=> info.InvokerData.ChannelId.HasValue
			? new JsonSingleValue<ulong>(info.InvokerData.ChannelId.Value)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser all", "Gets the unique Id of a user.")]
		public JsonObject CommandGetUser(ExecutionInformation info)
		{
			var client = info.InvokerData;
			return new JsonSingleObject<InvokerData>($"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.ClientUid}", client);
		}

		[Command("getuser uid byid", "Gets the unique Id of a user.")]
		public JsonObject CommandGetUidById(ushort id) => new JsonSingleValue<string>(QueryConnection.GetClientById(id).UnwrapThrow().Uid);
		[Command("getuser name byid", "Gets the Nickname of a user.")]
		public JsonObject CommandGetNameById(ushort id) => new JsonSingleValue<string>(QueryConnection.GetClientById(id).UnwrapThrow().NickName);
		[Command("getuser dbid byid", "Gets the DatabaseId of a user.")]
		public JsonObject CommandGetDbIdById(ushort id) => new JsonSingleValue<ulong>(QueryConnection.GetClientById(id).UnwrapThrow().DatabaseId);
		[Command("getuser channel byid", "Gets the ChannelId a user is currently in.")]
		public JsonObject CommandGetChannelById(ushort id) => new JsonSingleValue<ulong>(QueryConnection.GetClientById(id).UnwrapThrow().ChannelId);
		[Command("getuser all byid", "Gets the unique Id of a user.")]
		public JsonObject CommandGetUserById(ushort id)
		{
			var client = QueryConnection.GetClientById(id).UnwrapThrow();
			return new JsonSingleObject<ClientData>($"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}", client);
		}
		[Command("getuser id byname", "Gets the Id of a user.")]
		public JsonObject CommandGetIdByName(string username) => new JsonSingleValue<ushort>(QueryConnection.GetClientByName(username).UnwrapThrow().ClientId);
		[Command("getuser all byname", "Gets the unique Id of a user.")]
		public JsonObject CommandGetUserByName(string username)
		{
			var client = QueryConnection.GetClientByName(username).UnwrapThrow();
			return new JsonSingleObject<ClientData>($"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}", client);
		}
		[Command("getuser name bydbid", "Gets the User name by dbid.")]
		public JsonObject CommandGetNameByDbId(ulong dbId) => new JsonSingleValue<string>(QueryConnection.GetDbClientByDbId(dbId).UnwrapThrow().NickName ?? string.Empty);
		[Command("getuser uid bydbid", "Gets the unique Id of a user.")]
		public JsonObject CommandGetUidByDbId(ulong dbId) => new JsonSingleValue<string>(QueryConnection.GetDbClientByDbId(dbId).UnwrapThrow().Uid);

		[Command("help", "Shows all commands or detailed help about a specific command.")]
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

				if (target is BotCommand targetB)
					return new JsonSingleValue<string>(targetB.GetHelp());

				if (target is CommandGroup targetCG)
				{
					var subList = targetCG.Commands.Select(g => g.Key).ToArray();
					return new JsonArray<string>("The command contains the following subfunctions: " + string.Join(", ", subList), subList);
				}

				if (target is OverloadedFunctionCommand targetOfc)
				{
					var strb = new StringBuilder();
					foreach (var botCom in targetOfc.Functions.OfType<BotCommand>())
						strb.Append(botCom.GetHelp());
					return new JsonSingleValue<string>(strb.ToString());
				}

				throw new CommandException("Seems like something went wrong. No help can be shown for this command path.", CommandExceptionReason.CommandError);
			}
		}

		[Command("history add", "<id> Adds the song with <id> to the queue")]
		public void CommandHistoryQueue(ExecutionInformation info, uint id)
			=> PlayManager.Enqueue(info.InvokerData, id).UnwrapThrow();

		[Command("history clean", "Cleans up the history file for better startup performance.")]
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

		[Command("history clean removedefective", "Cleans up the history file for better startup performance. " +
			"Also checks for all links in the history which cannot be opened anymore")]
		public string CommandHistoryCleanRemove(ExecutionInformation info)
		{
			if (info.ApiCall)
			{
				HistoryManager.RemoveBrokenLinks(info);
				return null;
			}
			info.Session.SetResponse(ResponseHistoryClean, "removedefective");
			return $"Do want to remove all defective links file now? " +
					"This might(will!) take a while and make the bot unresponsive in meanwhile. !(yes|no)";
		}

		[Command("history delete", "<id> Removes the entry with <id> from the history")]
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

		[Command("history from", "Gets the last <count> songs from the user with the given <user-dbid>")]
		[RequiredParameters(1)]
		public JsonObject CommandHistoryFrom(uint userDbId, int? amount)
		{
			var query = new SeachQuery { UserId = userDbId };
			if (amount.HasValue)
				query.MaxResults = amount.Value;

			var results = HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(HistoryManager.Format(results), results);
		}

		[Command("history id", "<id> Displays all saved informations about the song with <id>")]
		public JsonObject CommandHistoryId(uint id)
		{
			var result = HistoryManager.GetEntryById(id);
			if (!result)
				return new JsonEmpty("No entry found...");
			return new JsonSingleObject<AudioLogEntry>(HistoryManager.Format(result.Value), result.Value);
		}

		[Command("history id", "(last|next) Gets the highest|next song id")]
		public JsonObject CommandHistoryId(string special)
		{
			if (special == "last")
				return new JsonSingleValue<uint>($"{HistoryManager.HighestId} is the currently highest song id.", HistoryManager.HighestId);
			else if (special == "next")
				return new JsonSingleValue<uint>($"{HistoryManager.HighestId + 1} will be the next song id.", HistoryManager.HighestId + 1);
			else
				throw new CommandException("Unrecognized name descriptor", CommandExceptionReason.CommandError);
		}

		[Command("history last", "Plays the last song again")]
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
					PlayManager.Play(info.InvokerData, ale.AudioResource).UnwrapThrow();
					return null;
				}
				else return new JsonEmpty("There is no song in the history");
			}
		}

		[Command("history play", "<id> Playes the song with <id>")]
		public void CommandHistoryPlay(ExecutionInformation info, uint id)
			=> PlayManager.Play(info.InvokerData, id).UnwrapThrow();

		[Command("history rename", "<id> <name> Sets the name of the song with <id> to <name>")]
		public void CommandHistoryRename(uint id, string newName)
		{
			var ale = HistoryManager.GetEntryById(id).UnwrapThrow();

			if (string.IsNullOrWhiteSpace(newName))
				throw new CommandException("The new name must not be empty or only whitespaces", CommandExceptionReason.CommandError);

			HistoryManager.RenameEntry(ale, newName);
		}

		[Command("history till", "<date> Gets all songs played until <date>.")]
		public JsonObject CommandHistoryTill(DateTime time)
		{
			var query = new SeachQuery { LastInvokedAfter = time };
			var results = HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(HistoryManager.Format(results), results);
		}

		[Command("history till", "<name> Any of those desciptors: (hour|today|yesterday|week)")]
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

		[Command("history title", "Gets all songs which title contains <string>")]
		public JsonObject CommandHistoryTitle(string part)
		{
			var query = new SeachQuery { TitlePart = part };
			var results = HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(HistoryManager.Format(results), results);
		}

		[Command("if")]
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

			bool cmpResult;
			// Try to parse arguments into doubles
			if (double.TryParse(arg0, NumberStyles.Number, CultureInfo.InvariantCulture, out var d0)
				&& double.TryParse(arg1, NumberStyles.Number, CultureInfo.InvariantCulture, out var d1))
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

		[Command("json merge", "Allows you to combine multiple JsonResults into one")]
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

		[Command("kickme", "Guess what?")]
		[Usage("[far]", "Optional attribute for the extra punch strength")]
		[RequiredParameters(0)]
		public void CommandKickme(ExecutionInformation info, string parameter)
		{
			if (info.ApiCall)
				throw new CommandException("This command is not available as API", CommandExceptionReason.NotSupported);

			try
			{
				if (info.InvokerData.ClientId.HasValue)
				{
					if (string.IsNullOrEmpty(parameter) || parameter == "near")
						QueryConnection.KickClientFromChannel(info.InvokerData.ClientId.Value);
					else if (parameter == "far")
						QueryConnection.KickClientFromServer(info.InvokerData.ClientId.Value);
				}
			}
			catch (Ts3CommandException ex)
			{
				Log.Write(Log.Level.Info, "Could not kick: {0}", ex);
				throw new CommandException("I'm not strong enough, master!", ex, CommandExceptionReason.CommandError);
			}
		}

		[Command("link", "Gets a link to the origin of the current song.")]
		public JsonObject CommandLink(ExecutionInformation info)
		{
			if (PlayManager.CurrentPlayData == null)
				return new JsonEmpty("There is nothing on right now...");
			else if (QuizMode && PlayManager.CurrentPlayData.Invoker.ClientId != info.InvokerData.ClientId && !info.ApiCall)
				return new JsonEmpty("Sorry, you have to guess!");
			else
			{
				var link = FactoryManager.RestoreLink(PlayManager.CurrentPlayData.ResourceData);
				return new JsonSingleValue<string>(link);
			}
		}

		[Command("list add", "Adds a link to your private playlist.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public void CommandListAdd(ExecutionInformation info, string link)
		{
			var plist = AutoGetPlaylist(info);
			var playResource = FactoryManager.Load(link).UnwrapThrow();
			plist.AddItem(new PlaylistItem(playResource.BaseData, new MetaData() { ResourceOwnerDbId = info.InvokerData.DatabaseId }));
		}

		[Command("list add", "<id> Adds a link to your private playlist from the history by <id>.")]
		public void CommandListAdd(ExecutionInformation info, uint hid)
		{
			var plist = AutoGetPlaylist(info);

			if (!HistoryManager.GetEntryById(hid))
				throw new CommandException("History entry not found", CommandExceptionReason.CommandError);

			plist.AddItem(new PlaylistItem(hid, new MetaData() { ResourceOwnerDbId = info.InvokerData.DatabaseId }));
		}

		[Command("list clear", "Clears your private playlist.")]
		public void CommandListClear(ExecutionInformation info) => AutoGetPlaylist(info).Clear();

		[Command("list delete", "<name> Deletes the playlist with the name <name>. You can only delete playlists which you also have created. Admins can delete every playlist.")]
		public JsonObject CommandListDelete(ExecutionInformation info, string name)
		{
			if (info.ApiCall)
				PlaylistManager.DeletePlaylist(name, info.InvokerData.DatabaseId ?? 0, info.HasRights(rightDeleteAllPlaylists)).UnwrapThrow();

			var hresult = PlaylistManager.LoadPlaylist(name, true);
			if (!hresult)
			{
				info.Session.SetResponse(ResponseListDelete, name);
				return new JsonEmpty($"Do you really want to delete the playlist \"{name}\" (error:{hresult.Message})");
			}
			else
			{
				if (hresult.Value.CreatorDbId != info.InvokerData.DatabaseId
					&& !info.HasRights(rightDeleteAllPlaylists))
					throw new CommandException("You are not allowed to delete others playlists", CommandExceptionReason.MissingRights);

				info.Session.SetResponse(ResponseListDelete, name);
				return new JsonEmpty($"Do you really want to delete the playlist \"{name}\"");
			}
		}

		[Command("list get", "<link> Imports a playlist form an other plattform like youtube etc.")]
		public JsonObject CommandListGet(ExecutionInformation info, string link)
		{
			var playlist = info.Session.Bot.FactoryManager.LoadPlaylistFrom(link).UnwrapThrow();

			playlist.CreatorDbId = info.InvokerData.DatabaseId;
			info.Session.Set<PlaylistManager, Playlist>(playlist);
			return new JsonEmpty("Ok");
		}

		[Command("list item move", "<from> <to> Moves a item in a playlist <from> <to> position.")]
		public void CommandListMove(ExecutionInformation info, int from, int to)
		{
			var plist = AutoGetPlaylist(info);

			if (from < 0 || from >= plist.Count
				|| to < 0 || to >= plist.Count)
				throw new CommandException("Index must be within playlist length", CommandExceptionReason.CommandError);

			if (from == to)
				return;

			var plitem = plist.GetResource(from);
			plist.RemoveItemAt(from);
			plist.InsertItem(plitem, Math.Min(to, plist.Count));
		}

		[Command("list item delete", "<index> Removes the item at <index>.")]
		public string CommandListRemove(ExecutionInformation info, int index)
		{
			var plist = AutoGetPlaylist(info);

			if (index < 0 || index >= plist.Count)
				throw new CommandException("Index must be within playlist length", CommandExceptionReason.CommandError);

			var deletedItem = plist.GetResource(index);
			plist.RemoveItemAt(index);
			return "Removed: " + deletedItem.DisplayString;
		}

		// add list item rename

		[Command("list list", "Displays all available playlists from all users.")]
		[Usage("<pattern>", "Filters all lists cantaining the given pattern.")]
		[RequiredParameters(0)]
		public JsonObject CommandListList(ExecutionInformation info, string pattern)
		{
			var files = PlaylistManager.GetAvailablePlaylists(pattern).ToArray();
			if (files.Length <= 0)
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

		[Command("list load", "Opens a playlist to be editable for you. This replaces your current worklist with the opened playlist.")]
		public JsonObject CommandListLoad(ExecutionInformation info, string name)
		{
			Playlist loadList = AutoGetPlaylist(info);

			var playList = PlaylistManager.LoadPlaylist(name).UnwrapThrow();

			loadList.Clear();
			loadList.AddRange(playList.AsEnumerable());
			loadList.Name = playList.Name;
			return new JsonSingleObject<Playlist>($"Loaded: \"{name}\" with {loadList.Count} songs", loadList);
		}

		[Command("list merge", "Appends another playlist to yours.")]
		public void CommandListMerge(ExecutionInformation info, string name)
		{
			var plist = AutoGetPlaylist(info);

			var lresult = PlaylistManager.LoadPlaylist(name);
			if (!lresult)
				throw new CommandException("The other playlist could not be found", CommandExceptionReason.CommandError);

			plist.AddRange(lresult.Value.AsEnumerable());
		}

		[Command("list name", "Displays the name of the playlist you are currently working on.")]
		[Usage("<name>", "Changes the playlist name to <name>.")]
		public JsonObject CommandListName(ExecutionInformation info, string name)
		{
			var plist = AutoGetPlaylist(info);

			if (string.IsNullOrEmpty(name))
				return new JsonSingleValue<string>(plist.Name);

			PlaylistManager.IsNameValid(name).UnwrapThrow();

			plist.Name = name;
			return null;
		}

		[Command("list play", "Replaces the current freelist with your workinglist and plays from the beginning.")]
		[Usage("<index>", "Lets you specify the starting song index.")]
		[RequiredParameters(0)]
		public void CommandListPlay(ExecutionInformation info, int? index)
		{
			var plist = AutoGetPlaylist(info);

			if (!index.HasValue || (index.Value >= 0 && index.Value < plist.Count))
			{
				PlaylistManager.PlayFreelist(plist);
				PlaylistManager.Index = index ?? 0;
			}
			else
				throw new CommandException("Invalid starting index", CommandExceptionReason.CommandError);

			PlaylistItem item = PlaylistManager.Current();
			if (item != null)
				PlayManager.Play(info.InvokerData, item).UnwrapThrow();
			else
				throw new CommandException("Nothing to play...", CommandExceptionReason.CommandError);
		}

		[Command("list save", "Stores your current workinglist to disk.")]
		[Usage("<name>", "Changes the playlist name to <name> before saving.")]
		[RequiredParameters(0)]
		public JsonObject CommandListSave(ExecutionInformation info, string optNewName)
		{
			var plist = AutoGetPlaylist(info);
			if (!string.IsNullOrEmpty(optNewName))
			{
				PlaylistManager.IsNameValid(optNewName).UnwrapThrow();
				plist.Name = optNewName;
			}

			PlaylistManager.SavePlaylist(plist).UnwrapThrow();
			return new JsonEmpty("Ok");
		}

		[Command("list show", "Displays all songs currently in the playlists you are working on")]
		[Usage("<index>", "Lets you specify the staring index from which songs should be listed.")]
		[RequiredParameters(0)]
		public JsonObject CommandListShow(ExecutionInformation info, int? offset) => CommandListShow(info, null, offset);

		[Command("list show", "<name> Displays all songs currently in the playlists with the name <name>")]
		[Usage("name> <index>", "Lets you specify the starting index from which songs should be listed.")]
		[RequiredParameters(0)]
		public JsonObject CommandListShow(ExecutionInformation info, string name, int? offset)
		{
			Playlist plist;
			if (!string.IsNullOrEmpty(name))
				plist = PlaylistManager.LoadPlaylist(name).UnwrapThrow();
			else
				plist = AutoGetPlaylist(info);

			var strb = new StringBuilder();
			strb.Append($"Playlist: \"").Append(plist.Name).Append("\" with ").Append(plist.Count).AppendLine(" songs.");
			int from = Math.Max(offset ?? 0, 0);
			var items = plist.AsEnumerable().Skip(from).ToArray();
			foreach (var plitem in items.Take(10))
				strb.Append(from++).Append(": ").AppendLine(plitem.DisplayString);

			return new JsonArray<PlaylistItem>(strb.ToString(), items);
		}

		[Command("loop", "Gets whether or not to loop the entire playlist.")]
		public JsonObject CommandLoop() => new JsonSingleValue<bool>("Loop is " + (PlaylistManager.Loop ? "on" : "off"), PlaylistManager.Loop);
		[Command("loop on", "Enables looping the entire playlist.")]
		public void CommandLoopOn() => PlaylistManager.Loop = true;
		[Command("loop off", "Disables looping the entire playlist.")]
		public void CommandLoopOff() => PlaylistManager.Loop = false;

		[Command("next", "Plays the next song in the playlist.")]
		public void CommandNext(ExecutionInformation info)
		{
			PlayManager.Next(info.InvokerData).UnwrapThrow();
		}

		[Command("pm", "Requests a private session with the ServerBot so you can be intimate.")]
		public string CommandPM(ExecutionInformation info)
		{
			if (info.ApiCall)
				throw new CommandException("This command is not available as API", CommandExceptionReason.NotSupported);
			info.InvokerData.Visibiliy = TextMessageTargetMode.Private;
			return "Hi " + (info.InvokerData.NickName ?? "Anonymous");
		}

		[Command("parse command", "Displays the AST of the requested command.")]
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

		[Command("pause", "Well, pauses the song. Undo with !play.")]
		public void CommandPause() => PlayerConnection.Paused = true;

		[Command("play", "Automatically tries to decide whether the link is a special resource (like youtube) or a direct resource (like ./hello.mp3) and starts it.")]
		[Usage("<link>", "Youtube, Soundcloud, local path or file link")]
		[RequiredParameters(0)]
		public void CommandPlay(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				PlayerConnection.Paused = false;
			else
				PlayManager.Play(info.InvokerData, parameter).UnwrapThrow();
		}

		[Command("plugin list", "Lists all found plugins.")]
		public string CommandPluginList()
		{
			return PluginManager.GetPluginOverview(); // TODO Api callcable
		}

		[Command("plugin unload", "Unloads a plugin.")]
		public JsonObject CommandPluginUnload(string identifier)
		{
			string ret = PluginManager.UnloadPlugin(identifier).ToString();
			return new JsonSingleValue<string>(ret);
		}

		[Command("plugin load", "Unloads a plugin.")]
		public void CommandPluginLoad(string identifier)
			=> PluginManager.LoadPlugin(identifier).UnwrapThrow();

		[Command("previous", "Plays the previous song in the playlist.")]
		public void CommandPrevious(ExecutionInformation info)
			=> PlayManager.Previous(info.InvokerData).UnwrapThrow();

		[Command("print", "Lets you format multiple parameter to one.")]
		[RequiredParameters(0)]
		public JsonObject CommandPrint(params string[] parameter)
		{
			// << Desing changes expected >>
			var strb = new StringBuilder();
			foreach (var param in parameter)
				strb.Append(param);
			return new JsonSingleValue<string>(strb.ToString());
		}

		[Command("quit", "Closes the TS3AudioBot application.")]
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

		[Command("quiz", "Shows the quizmode status.")]
		public JsonObject CommandQuiz() => new JsonSingleValue<bool>("Quizmode is " + (QuizMode ? "on" : "off"), QuizMode);
		[Command("quiz on", "Enable to hide the songnames and let your friends guess the title.")]
		public void CommandQuizOn()
		{
			QuizMode = true;
			UpdateBotStatus().UnwrapThrow();
		}
		[Command("quiz off", "Disable to show the songnames again.")]
		public void CommandQuizOff(ExecutionInformation info)
		{
			if (!info.ApiCall && info.InvokerData.Visibiliy.HasValue && info.InvokerData.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("No cheatig! Everybody has to see it!", CommandExceptionReason.CommandError);
			QuizMode = false;
			UpdateBotStatus().UnwrapThrow();
		}

		[Command("random", "Gets whether or not to play playlists in random order.")]
		public JsonObject CommandRandom() => new JsonSingleValue<bool>("Random is " + (PlaylistManager.Random ? "on" : "off"), PlaylistManager.Random);
		[Command("random on", "Enables random playlist playback")]
		public void CommandRandomOn() => PlaylistManager.Random = true;
		[Command("random off", "Disables random playlist playback")]
		public void CommandRandomOff() => PlaylistManager.Random = false;
		[Command("random seed", "Gets the unique seed for a certain playback order")]
		public JsonObject CommandRandomSeed()
		{
			string seed = Util.FromSeed(PlaylistManager.Seed);
			string strseed = string.IsNullOrEmpty(seed) ? "<empty>" : seed;
			return new JsonSingleValue<string>(strseed);
		}
		[Command("random seed", "Sets the unique seed for a certain playback order")]
		public void CommandRandomSeed(string newSeed)
		{
			if (newSeed.Any(c => !char.IsLetter(c)))
				throw new CommandException("Only letters allowed", CommandExceptionReason.CommandError);
			PlaylistManager.Seed = Util.ToSeed(newSeed.ToLowerInvariant());
		}
		[Command("random seed", "Sets the unique seed for a certain playback order")]
		public void CommandRandomSeed(int newSeed) => PlaylistManager.Seed = newSeed;

		[Command("repeat", "Gets whether or not to loop a single song.")]
		public JsonObject CommandRepeat() => new JsonSingleValue<bool>("Repeat is " + (PlayerConnection.Repeated ? "on" : "off"), PlayerConnection.Repeated);
		[Command("repeat on", "Enables single song repeat.")]
		public void CommandRepeatOn() => PlayerConnection.Repeated = true;
		[Command("repeat off", "Disables single song repeat.")]
		public void CommandRepeatOff() => PlayerConnection.Repeated = false;

		[Command("rights can", "Returns the subset of allowed commands the caller (you) can execute.")]
		public JsonObject CommandRightsCan(ExecutionInformation info, params string[] rights)
		{
			var result = RightsManager.GetRightsSubset(info.InvokerData, rights);
			if (result.Length > 0)
				return new JsonArray<string>(string.Join(", ", result), result);
			else
				return new JsonEmpty("No");
		}

		[Command("rights reload", "Reloads the rights configuration from file.")]
		public JsonObject CommandRightsReload()
		{
			if (RightsManager.ReadFile())
				return new JsonEmpty("Ok");
			else
				// TODO: this can be done nicer by returning the errors and warnings from parsing
				return new JsonError("Error while parsing file, see log for more details", CommandExceptionReason.CommandError);
		}

		[Command("rng", "Gets a random number.")]
		[Usage("", "Gets a number between 0 and 2147483647")]
		[Usage("<max>", "Gets a number between 0 and <max>")]
		[Usage("<min> <max>", "Gets a number between <min> and <max>")]
		[RequiredParameters(0)]
		public JsonObject CommandRng(int? first, int? second)
		{
			int num;
			if (first.HasValue && second.HasValue)
				num = Util.Random.Next(Math.Min(first.Value, second.Value), Math.Max(first.Value, second.Value));
			else if (first.HasValue)
			{
				if (first.Value <= 0)
					throw new CommandException("Value must be 0 or positive", CommandExceptionReason.CommandError);
				num = Util.Random.Next(first.Value);
			}
			else
				num = Util.Random.Next();
			return new JsonSingleValue<int>(num);
		}

		[Command("seek", "Jumps to a timemark within the current song.")]
		[Usage("<sec>", "Time in seconds")]
		[Usage("<min:sec>", "Time in Minutes:Seconds")]
		public void CommandSeek(ExecutionInformation info, string parameter)
		{
			TimeSpan span;
			bool parsed = false;
			if (parameter.Contains(":"))
			{
				string[] splittime = parameter.Split(':');

				if (splittime.Length == 2
					&& int.TryParse(splittime[0], out int minutes)
					&& int.TryParse(splittime[1], out int seconds))
				{
					parsed = true;
					span = TimeSpan.FromSeconds(seconds) + TimeSpan.FromMinutes(minutes);
				}
				else span = TimeSpan.MinValue;
			}
			else
			{
				parsed = int.TryParse(parameter, out int seconds);
				span = TimeSpan.FromSeconds(seconds);
			}

			if (!parsed)
				throw new CommandException("The time was not in a correct format, see !help seek for more information.", CommandExceptionReason.CommandError);
			else if (span < TimeSpan.Zero || span > PlayerConnection.Length)
				throw new CommandException("The point of time is not within the songlenth.", CommandExceptionReason.CommandError);
			else
				PlayerConnection.Position = span;
		}

		[Command("settings", "Changes values from the settigns. Not all changes can be applied immediately.")]
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

		[Command("song", "Tells you the name of the current song.")]
		public JsonObject CommandSong(ExecutionInformation info)
		{
			if (PlayManager.CurrentPlayData == null)
				return new JsonEmpty("There is nothing on right now...");
			else if (QuizMode && PlayManager.CurrentPlayData.Invoker.ClientId != info.InvokerData.ClientId && !info.ApiCall)
				return new JsonEmpty("Sorry, you have to guess!");
			else
				return new JsonSingleValue<string>(
					$"[url={FactoryManager.RestoreLink(PlayManager.CurrentPlayData.ResourceData)}]{PlayManager.CurrentPlayData.ResourceData.ResourceTitle}[/url]",
					PlayManager.CurrentPlayData.ResourceData.ResourceTitle);
		}

		[Command("stop", "Stops the current song.")]
		public void CommandStop()
		{
			PlayManager.Stop();
		}

		[Command("subscribe", "Lets you hear the music independent from the channel you are in.")]
		public void CommandSubscribe(ExecutionInformation info)
		{
			if (info.InvokerData.ClientId.HasValue)
				TargetManager.WhisperClientSubscribe(info.InvokerData.ClientId.Value);
		}

		[Command("subscribe tempchannel", "Adds your current channel to the music playback.")]
		[RequiredParameters(0)]
		public void CommandSubscribeTempChannel(ExecutionInformation info, ulong? channel)
		{
			var subChan = channel ?? info.InvokerData.ChannelId ?? 0;
			if (subChan != 0)
				TargetManager.WhisperChannelSubscribe(subChan, true);
		}

		[Command("subscribe channel", "Adds your current channel to the music playback.")]
		[RequiredParameters(0)]
		public void CommandSubscribeChannel(ExecutionInformation info, ulong? channel)
		{
			var subChan = channel ?? info.InvokerData.ChannelId ?? 0;
			if (subChan != 0)
				TargetManager.WhisperChannelSubscribe(subChan, false);
		}

		[Command("take", "Take a substring from a string.")]
		[Usage("<count> <text>", "Take only <count> parts of the text")]
		[Usage("<count> <start> <text>", "Take <count> parts, starting with the part at <start>")]
		[Usage("<count> <start> <delimiter> <text>", "Specify another delimiter for the parts than spaces")]
		public ICommandResult CommandTake(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			var argList = arguments.ToList();

			if (argList.Count < 2)
				throw new CommandException("Expected at least 2 parameters", CommandExceptionReason.MissingParameter);

			int start = 0;
			string delimiter = null;

			// Get count
			var res = ((StringCommandResult)argList[0].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
			if (!int.TryParse(res, out int count) || count < 0)
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

		[Command("test", "Only for debugging purposes.")]
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

		[Command("unsubscribe", "Only lets you hear the music in active channels again.")]
		public void CommandUnsubscribe(ExecutionInformation info)
		{
			if (info.InvokerData.ClientId.HasValue)
				TargetManager.WhisperClientUnsubscribe(info.InvokerData.ClientId.Value);
		}

		[Command("unsubscribe channel", "Removes your current channel from the music playback.")]
		[RequiredParameters(0)]
		public void CommandUnsubscribeChannel(ExecutionInformation info, ulong? channel)
		{
			var subChan = channel ?? info.InvokerData.ChannelId ?? 0;
			if (subChan != 0)
				TargetManager.WhisperChannelUnsubscribe(subChan, false);
		}

		[Command("unsubscribe temporary", "Clears all temporary targets.")]
		[RequiredParameters(0)]
		public void CommandUnsubscribeTemporary()
		{
			TargetManager.ClearTemporary();
		}

		[Command("version", "Gets the current build version.")]
		public JsonObject CommandVersion(ExecutionInformation info)
		{
			var data = Util.GetAssemblyData();
			return new JsonSingleValue<Util.BuildData>(data.ToLongString(), data);
		}

		[Command("volume", "Sets the volume level of the music.")]
		[Usage("<level>", "A new volume level between 0 and 100.")]
		[Usage("+/-<level>", "Adds or subtracts a value form the current volume.")]
		[RequiredParameters(0)]
		public JsonObject CommandVolume(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				return new JsonSingleValue<int>("Current volume: " + PlayerConnection.Volume, PlayerConnection.Volume);

			bool relPos = parameter.StartsWith("+", StringComparison.Ordinal);
			bool relNeg = parameter.StartsWith("-", StringComparison.Ordinal);
			string numberString = (relPos || relNeg) ? parameter.Remove(0, 1) : parameter;

			if (!int.TryParse(numberString, out int volume))
				throw new CommandException("The new volume could not be parsed", CommandExceptionReason.CommandError);

			int newVolume;
			if (relPos) newVolume = PlayerConnection.Volume + volume;
			else if (relNeg) newVolume = PlayerConnection.Volume - volume;
			else newVolume = volume;

			if (newVolume < 0 || newVolume > AudioValues.MaxVolume)
				throw new CommandException("The volume level must be between 0 and " + AudioValues.MaxVolume, CommandExceptionReason.CommandError);

			if (newVolume <= AudioValues.MaxUserVolume || newVolume < PlayerConnection.Volume || info.ApiCall)
				PlayerConnection.Volume = newVolume;
			else if (newVolume <= AudioValues.MaxVolume)
			{
				info.Session.SetResponse(ResponseVolume, newVolume);
				return new JsonEmpty("Careful you are requesting a very high volume! Do you want to apply this? !(yes|no)");
			}
			return null;
		}

		[Command("whisper off", "Enables normal voice mode.")]
		public void CommandWhisperOff() => TargetManager.SendMode = TargetSendMode.Voice;

		[Command("whisper subscription", "Enables default whisper subsciption mode.")]
		public void CommandWhisperSubsription() => TargetManager.SendMode = TargetSendMode.Whisper;

		[Command("whisper all", "Set how to send music.")]
		public void CommandWhisperAll() => CommandWhisperGroup(GroupWhisperType.AllClients, GroupWhisperTarget.AllChannels);

		[Command("whisper group", "Set a specific teamspeak whisper group.")]
		[RequiredParameters(2)]
		public void CommandWhisperGroup(GroupWhisperType type, GroupWhisperTarget target, ulong? targetId = null)
		{
			if (type == GroupWhisperType.ServerGroup || type == GroupWhisperType.ChannelGroup)
			{
				if (!targetId.HasValue)
					throw new CommandException("This type required an additional target", CommandExceptionReason.CommandError);
				TargetManager.SetGroupWhisper(type, target, targetId.Value);
				TargetManager.SendMode = TargetSendMode.WhisperGroup;
			}
			else
			{
				if (targetId.HasValue)
					throw new CommandException("This type does not take an additional target", CommandExceptionReason.CommandError);
				TargetManager.SetGroupWhisper(type, target, 0);
				TargetManager.SendMode = TargetSendMode.WhisperGroup;
			}
		}

		[Command("xecute", "Evaluates all parameter.")]
		public void CommandXecute(ExecutionInformation info, IEnumerable<ICommand> arguments)
		{
			var retType = new CommandResultType[] { CommandResultType.Empty, CommandResultType.String, CommandResultType.Json };
			foreach (var arg in arguments)
				arg.Execute(info, Enumerable.Empty<ICommand>(), retType);
		}

		#endregion

		#region RESPONSES

		private string ResponseVolume(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				if (info.HasRights(rightHighVolume))
				{
					var respInt = info.Session.ResponseData as int?;
					if (!respInt.HasValue)
					{
						Log.Write(Log.Level.Error, "responseData is not an int.");
						return "Internal error";
					}
					PlayerConnection.Volume = respInt.Value;
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
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				if (info.HasRights("cmd.quit"))
					CommandQuit(info, "force");
				else
					return "Command can only be answered by an admin.";
			}
			return null;
		}

		private string ResponseHistoryDelete(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				if (info.HasRights("cmd.history.delete"))
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
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				if (info.HasRights("cmd.history.clean"))
				{
					string param = info.Session.ResponseData as string;
					if (string.IsNullOrEmpty(param))
					{
						HistoryManager.CleanHistoryFile();
						return "Cleanup done!";
					}
					else if (param == "removedefective")
					{
						HistoryManager.RemoveBrokenLinks(info);
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
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				var name = info.Session.ResponseData as string;
				var result = PlaylistManager.DeletePlaylist(name, info.InvokerData.DatabaseId ?? 0, info.HasRights(rightDeleteAllPlaylists));
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

		private void GenerateStatusImage(object sender, EventArgs e)
		{
			if (!mainBotData.GenerateStatusAvatar)
				return;

			if (e is PlayInfoEventArgs startEvent)
			{
				var thumresult = FactoryManager.GetThumbnail(startEvent.PlayResource);
				if (!thumresult.Ok)
					return;

				using (var bmp = thumresult.Value)
				{
					ImageUtil.BuildStringImage(
						"Now playing: " + startEvent.ResourceData.ResourceTitle,
						bmp,
						new RectangleF(0, 0, bmp.Width, bmp.Height));
					using (var mem = new MemoryStream())
					{
						bmp.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
						var result = QueryConnection.UploadAvatar(mem);
						if (!result.Ok)
							Log.Write(Log.Level.Warning, "Could not save avatar: {0}", result.Message);
					}
				}
			}
			else
			{
				using (var sleepPic = Util.GetEmbeddedFile("TS3AudioBot.Media.SleepingKitty.png"))
				{
					var result = QueryConnection.UploadAvatar(sleepPic);
					if (!result.Ok)
						Log.Write(Log.Level.Warning, "Could not save avatar: {0}", result.Message);
				}
			}
		}

		private static Playlist AutoGetPlaylist(ExecutionInformation info)
		{
			var result = info.Session.Get<PlaylistManager, Playlist>();
			if (result)
				return result.Value;

			var newPlist = new Playlist(info.InvokerData.NickName, info.InvokerData.DatabaseId);
			info.Session.Set<PlaylistManager, Playlist>(newPlist);
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

			PlayManager.Stop();

			PlayerConnection?.Dispose(); // before: logStream,
			PlayerConnection = null;

			QueryConnection?.Dispose(); // before: logStream,
			QueryConnection = null;

			historyManager?.Dispose(); // before: logStream,
			historyManager = null;

			TickPool.Close(); // before:

			FactoryManager?.Dispose(); // before:
			FactoryManager = null;

			logStream?.Dispose();  // before:
			logStream = null;
		}
	}

#pragma warning disable CS0649
	class MainBotData : ConfigData
	{
		[Info("Path to the logfile", "log_ts3audiobot")]
		public string LogFile { get; set; }
		[Info("Teamspeak group id giving the Bot enough power to do his job", "0")]
		public ulong BotGroupId { get; set; }
		[Info("Generate fancy status images as avatar", "true")]
		public bool GenerateStatusAvatar { get; set; }
	}
#pragma warning restore CS0649
}
