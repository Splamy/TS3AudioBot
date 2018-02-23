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
	using CommandSystem;
	using CommandSystem.Ast;
	using CommandSystem.CommandResults;
	using CommandSystem.Commands;
	using Dependency;
	using Helper;
	using Helper.Environment;
	using History;
	using Plugins;
	using ResourceFactories;
	using Rights;
	using Sessions;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using TS3Client;
	using TS3Client.Audio;
	using TS3Client.Messages;
	using Web.Api;

	public static class MainCommands
	{
		public const string RightHighVolume = "ts3ab.admin.volume";
		public const string RightDeleteAllPlaylists = "ts3ab.admin.list";

		// [...] = Optional
		// <name> = Placeholder for a text
		// [text] = Option for fixed text
		// (a|b) = either or switch

		// ReSharper disable UnusedMember.Global
		[Command("add", "Adds a new song to the queue.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public static void CommandAdd(PlayManager playManager, InvokerData invoker, string parameter)
			=> playManager.Enqueue(invoker, parameter).UnwrapThrow();

		[Command("api token", "Generates an api token.")]
		[Usage("[<duration>]", "Optionally specifies a duration this key is valid in hours.")]
		public static string CommandApiToken(TokenManager tokenManager, InvokerData invoker, double? validHours = null)
		{
			if (invoker.Visibiliy.HasValue && invoker.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("Please use this command in a private session.", CommandExceptionReason.CommandError);
			if (invoker.ClientUid == null)
				throw new CommandException("No Uid found to register token for.", CommandExceptionReason.CommandError);
			TimeSpan? validSpan = null;
			try
			{
				if (validHours.HasValue)
					validSpan = TimeSpan.FromHours(validHours.Value);
			}
			catch (OverflowException oex)
			{
				throw new CommandException("Invalid token-valid duration.", oex, CommandExceptionReason.CommandError);
			}
			var token = tokenManager.GenerateToken(invoker.ClientUid, validSpan).UnwrapThrow();
			return token;
		}

		[Command("api nonce", "Generates an api nonce.")]
		public static string CommandApiNonce(TokenManager tokenManager, InvokerData invoker)
		{
			if (invoker.Visibiliy.HasValue && invoker.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("Please use this command in a private session.", CommandExceptionReason.CommandError);
			if (invoker.ClientUid == null)
				throw new CommandException("No Uid found to register token for.", CommandExceptionReason.CommandError);
			var result = tokenManager.GetToken(invoker.ClientUid);
			if (!result.Ok)
				throw new CommandException("No active token found.", CommandExceptionReason.CommandError);

			var nonce = result.Value.CreateNonce();
			return nonce.Value;
		}

		[Command("bot commander", "Gets the status of the channel commander mode.")]
		public static JsonValue<bool> CommandBotCommander(TeamspeakControl queryConnection)
		{
			var value = queryConnection.IsChannelCommander().UnwrapThrow();
			return new JsonValue<bool>(value, "Channel commander is " + (value ? "on" : "off"));
		}
		[Command("bot commander on", "Enables channel commander.")]
		public static void CommandBotCommanderOn(TeamspeakControl queryConnection) => queryConnection.SetChannelCommander(true).UnwrapThrow();
		[Command("bot commander off", "Disables channel commander.")]
		public static void CommandBotCommanderOff(TeamspeakControl queryConnection) => queryConnection.SetChannelCommander(false).UnwrapThrow();

		[Command("bot come", "Moves the bot to your channel.")]
		public static void CommandBotCome(TeamspeakControl queryConnection, InvokerData invoker, string password = null)
		{
			var channel = invoker?.ChannelId;
			if (!channel.HasValue)
				throw new CommandException("No target channel found", CommandExceptionReason.CommandError);
			CommandBotMove(queryConnection, channel.Value, password);
		}

		[Command("bot info", "Gets various information about the bot.")]
		public static BotInfo CommandBotInfo(Bot bot) => bot.GetInfo();

		[Command("bot info id", "Gets the id of the current bot.")]
		public static int CommandBotId(Bot bot) => bot.Id;

		[Command("bot list", "Gets the id of the current bot.")]
		public static JsonArray<BotInfo> CommandBotId(BotManager bots)
		{
			var botlist = bots.GetBotInfolist();
			return new JsonArray<BotInfo>(botlist, string.Join("\n", botlist.Select(x => x.ToString())));
		}

		[Command("bot move", "Moves the bot to you or a specified channel.")]
		public static void CommandBotMove(TeamspeakControl queryConnection, ulong channel, string password = null) => queryConnection.MoveTo(channel, password).UnwrapThrow();

		[Command("bot name", "Gives the bot a new name.")]
		public static void CommandBotName(TeamspeakControl queryConnection, string name) => queryConnection.ChangeName(name).UnwrapThrow();

		[Command("bot badges", "Set your bot a badge. The badges string starts with 'overwolf=0:badges='")]
		public static void CommandBotBadges(TeamspeakControl queryConnection, string badgesString) => queryConnection.ChangeBadges(badgesString).UnwrapThrow();

		[Command("bot setup", "Sets all teamspeak rights for the bot to be fully functional.")]
		public static void CommandBotSetup(ConfigFile configManager, TeamspeakControl queryConnection, string adminToken = null)
		{
			var mbd = configManager.GetDataStruct<MainBotData>("MainBot", true);
			queryConnection.SetupRights(adminToken, mbd).UnwrapThrow();
		}

		[Command("bot use", "Switches the conetext to the requested bot.")]
		public static ICommandResult CommandBotUse(ExecutionInformation info, IReadOnlyList<CommandResultType> returnTypes, BotManager bots, int botId, ICommand cmd)
		{
			using (var botLock = bots.GetBotLock(botId))
			{
				if (!botLock.IsValid)
					throw new CommandException("This bot does not exists", CommandExceptionReason.CommandError);

				var childInfo = new ExecutionInformation(botLock.Bot.Injector.CloneRealm<BotInjector>());
				if (info.TryGet<CallerInfo>(out var caller))
					childInfo.AddDynamicObject(caller);
				if (info.TryGet<InvokerData>(out var invoker))
					childInfo.AddDynamicObject(invoker);
				if (info.TryGet<UserSession>(out var session))
					childInfo.AddDynamicObject(session);

				return cmd.Execute(childInfo, StaticList.Empty<ICommand>(), returnTypes);
			}
		}

		[Command("clear", "Removes all songs from the current playlist.")]
		public static void CommandClear(PlaylistManager playlistManager) => playlistManager.ClearFreelist();

		[Command("connect", "Start a new bot instance.")]
		public static BotInfo CommandConnect(BotManager bots)
		{
			var botInfo = bots.CreateBot();
			if (botInfo == null)
				throw new CommandException("Could not create new instance", CommandExceptionReason.CommandError);
			return botInfo; // TODO check value/object
		}

		[Command("disconnect", "Stop this bot instance.")]
		public static void CommandDisconnect(BotManager bots, Bot bot) => bots.StopBot(bot);

		[Command("eval", "Executes a given command or string")]
		[Usage("<command> <arguments...>", "Executes the given command on arguments")]
		[Usage("<strings...>", "Concat the strings and execute them with the command system")]
		public static ICommandResult CommandEval(ExecutionInformation info, CommandManager commandManager, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			// Evaluate the first argument on the rest of the arguments
			if (arguments.Count == 0)
				throw new CommandException("Need at least one argument to evaluate", CommandExceptionReason.MissingParameter);
			var leftArguments = arguments.TrySegment(1);
			var arg0 = arguments[0].Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnCommandOrString);
			if (arg0.ResultType == CommandResultType.Command)
				return ((CommandCommandResult)arg0).Command.Execute(info, leftArguments, returnTypes);

			// We got a string back so parse and evaluate it
			var args = ((StringCommandResult)arg0).Content;

			// Add the rest of the arguments
			args += string.Join(" ", arguments.Select(a =>
				((StringCommandResult)a.Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnString)).Content));

			var cmd = commandManager.CommandSystem.AstToCommandResult(CommandParser.ParseCommandRequest(args));
			return cmd.Execute(info, leftArguments, returnTypes);
		}

		[Command("getmy id", "Gets your id.")]
		public static ushort CommandGetId(InvokerData invoker)
			=> invoker.ClientId ?? throw new CommandException("Not found.", CommandExceptionReason.CommandError);
		[Command("getmy uid", "Gets your unique id.")]
		public static string CommandGetUid(InvokerData invoker)
			=> invoker.ClientUid ?? throw new CommandException("Not found.", CommandExceptionReason.CommandError);
		[Command("getmy name", "Gets your nickname.")]
		public static string CommandGetName(InvokerData invoker)
			=> invoker.NickName ?? throw new CommandException("Not found.", CommandExceptionReason.CommandError);
		[Command("getmy dbid", "Gets your database id.")]
		public static ulong CommandGetDbId(InvokerData invoker)
			=> invoker.DatabaseId ?? throw new CommandException("Not found.", CommandExceptionReason.CommandError);
		[Command("getmy channel", "Gets your channel id you are currently in.")]
		public static ulong CommandGetChannel(InvokerData invoker)
			=> invoker.ChannelId ?? throw new CommandException("Not found.", CommandExceptionReason.CommandError);
		[Command("getmy all", "Gets all information about you.")]
		public static JsonValue<InvokerData> CommandGetUser(InvokerData invoker)
			=> new JsonValue<InvokerData>(invoker, $"Client: Id:{invoker.ClientId} DbId:{invoker.DatabaseId} ChanId:{invoker.ChannelId} Uid:{invoker.ClientUid}");

		[Command("getuser uid byid", "Gets the unique id of a user, searching with his id.")]
		public static string CommandGetUidById(TeamspeakControl queryConnection, ushort id) => queryConnection.GetClientById(id).UnwrapThrow().Uid;
		[Command("getuser name byid", "Gets the nickname of a user, searching with his id.")]
		public static string CommandGetNameById(TeamspeakControl queryConnection, ushort id) => queryConnection.GetClientById(id).UnwrapThrow().NickName;
		[Command("getuser dbid byid", "Gets the database id of a user, searching with his id.")]
		public static ulong CommandGetDbIdById(TeamspeakControl queryConnection, ushort id) => queryConnection.GetClientById(id).UnwrapThrow().DatabaseId;
		[Command("getuser channel byid", "Gets the channel id a user is currently in, searching with his id.")]
		public static ulong CommandGetChannelById(TeamspeakControl queryConnection, ushort id) => queryConnection.GetClientById(id).UnwrapThrow().ChannelId;
		[Command("getuser all byid", "Gets all information about a user, searching with his id.")]
		public static JsonValue<ClientData> CommandGetUserById(TeamspeakControl queryConnection, ushort id)
		{
			var client = queryConnection.GetClientById(id).UnwrapThrow();
			return new JsonValue<ClientData>(client, $"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}");
		}
		[Command("getuser id byname", "Gets the id of a user, searching with his name.")]
		public static ushort CommandGetIdByName(TeamspeakControl queryConnection, string username) => queryConnection.GetClientByName(username).UnwrapThrow().ClientId;
		[Command("getuser all byname", "Gets all information of a user, searching with his name.")]
		public static JsonValue<ClientData> CommandGetUserByName(TeamspeakControl queryConnection, string username)
		{
			var client = queryConnection.GetClientByName(username).UnwrapThrow();
			return new JsonValue<ClientData>(client, $"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}");
		}
		[Command("getuser name bydbid", "Gets the user name by dbid, searching with his database id.")]
		public static string CommandGetNameByDbId(TeamspeakControl queryConnection, ulong dbId) => queryConnection.GetDbClientByDbId(dbId).UnwrapThrow().NickName;
		[Command("getuser uid bydbid", "Gets the unique id of a user, searching with his database id.")]
		public static string CommandGetUidByDbId(TeamspeakControl queryConnection, ulong dbId) => queryConnection.GetDbClientByDbId(dbId).UnwrapThrow().Uid;

		[Command("help", "Shows all commands or detailed help about a specific command.")]
		[Usage("[<command>]", "Any currently accepted command")]
		public static JsonObject CommandHelp(CommandManager commandManager, params string[] parameter)
		{
			if (parameter.Length == 0)
			{
				var strb = new StringBuilder();
				strb.Append("\n========= Welcome to the TS3AudioBot ========="
					+ "\nIf you need any help with a special command use !help <commandName>."
					+ "\nHere are all possible commands:\n");
				var botComList = commandManager.AllCommands.Select(c => c.InvokeName).OrderBy(x => x).GroupBy(n => n.Split(' ')[0]).Select(x => x.Key).ToArray();
				foreach (var botCom in botComList)
					strb.Append(botCom).Append(", ");
				strb.Length -= 2;
				return new JsonArray<string>(botComList, strb.ToString());
			}

			CommandGroup group = commandManager.CommandSystem.RootCommand;
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

			switch (target)
			{
			case BotCommand targetB:
				return new JsonValue<string>(targetB.GetHelp());
			case CommandGroup targetCg:
				var subList = targetCg.Commands.Select(g => g.Key).ToArray();
				return new JsonArray<string>(subList, "The command contains the following subfunctions: " + string.Join(", ", subList));
			case OverloadedFunctionCommand targetOfc:
				var strb = new StringBuilder();
				foreach (var botCom in targetOfc.Functions.OfType<BotCommand>())
					strb.Append(botCom.GetHelp());
				return new JsonValue<string>(strb.ToString());
			}

			throw new CommandException("Seems like something went wrong. No help can be shown for this command path.", CommandExceptionReason.CommandError);
		}

		[Command("history add", "<id> Adds the song with <id> to the queue")]
		public static void CommandHistoryQueue(PlayManager playManager, InvokerData invoker, uint id)
			=> playManager.Enqueue(invoker, id).UnwrapThrow();

		[Command("history clean", "Cleans up the history file for better startup performance.")]
		public static JsonEmpty CommandHistoryClean(DbStore database, CallerInfo caller, UserSession session = null)
		{
			if (caller.ApiCall)
			{
				database.CleanFile();
				return new JsonEmpty(string.Empty);
			}

			string ResponseHistoryClean(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					database.CleanFile();
					return "Cleanup done!";
				}
				return null;
			}
			session.SetResponse(ResponseHistoryClean);
			return new JsonEmpty("Do want to clean the history file now? " +
				"This might take a while and make the bot unresponsive in meanwhile. !(yes|no)");
		}

		[Command("history clean removedefective", "Cleans up the history file for better startup performance. " +
			"Also checks for all links in the history which cannot be opened anymore")]
		public static JsonEmpty CommandHistoryCleanRemove(HistoryManager historyManager, CallerInfo caller, UserSession session = null)
		{
			if (caller.ApiCall)
			{
				historyManager.RemoveBrokenLinks();
				return new JsonEmpty(string.Empty);
			}

			string ResponseHistoryCleanRemove(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					historyManager.RemoveBrokenLinks();
					return "Cleanup done!";
				}
				return null;
			}
			session.SetResponse(ResponseHistoryCleanRemove);
			return new JsonEmpty("Do want to remove all defective links file now? " +
				"This might(will!) take a while and make the bot unresponsive in meanwhile. !(yes|no)");
		}

		[Command("history delete", "<id> Removes the entry with <id> from the history")]
		public static JsonEmpty CommandHistoryDelete(ExecutionInformation info, HistoryManager historyManager, CallerInfo caller, uint id, UserSession session = null)
		{
			var ale = historyManager.GetEntryById(id).UnwrapThrow();

			if (caller.ApiCall)
			{
				historyManager.RemoveEntry(ale);
				return new JsonEmpty(string.Empty);
			}

			string ResponseHistoryDelete(string message)
			{
				Answer answer = TextUtil.GetAnswer(message);
				if (answer == Answer.Yes)
				{
					historyManager.RemoveEntry(ale);
				}
				return null;
			}

			session.SetResponse(ResponseHistoryDelete);
			string name = ale.AudioResource.ResourceTitle;
			if (name.Length > 100)
				name = name.Substring(100) + "...";
			return new JsonEmpty($"Do you really want to delete the entry \"{name}\"\nwith the id {id}? !(yes|no)");
		}

		[Command("history from", "Gets the last <count> songs from the user with the given <user-dbid>")]
		public static JsonArray<AudioLogEntry> CommandHistoryFrom(HistoryManager historyManager, uint userDbId, int? amount = null)
		{
			var query = new SeachQuery { UserId = userDbId };
			if (amount.HasValue)
				query.MaxResults = amount.Value;

			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format(results));
		}

		[Command("history id", "<id> Displays all saved informations about the song with <id>")]
		public static JsonValue<AudioLogEntry> CommandHistoryId(HistoryManager historyManager, uint id)
		{
			var result = historyManager.GetEntryById(id).UnwrapThrow();
			return new JsonValue<AudioLogEntry>(result, historyManager.Format(result));
		}

		[Command("history id", "(last|next) Gets the highest|next song id")]
		public static JsonValue<uint> CommandHistoryId(HistoryManager historyManager, string special)
		{
			if (special == "last")
				return new JsonValue<uint>(historyManager.HighestId, $"{historyManager.HighestId} is the currently highest song id.");
			else if (special == "next")
				return new JsonValue<uint>(historyManager.HighestId + 1, $"{historyManager.HighestId + 1} will be the next song id.");
			else
				throw new CommandException("Unrecognized name descriptor", CommandExceptionReason.CommandError);
		}

		[Command("history last", "<count> Gets the last <count> played songs.")]
		public static JsonArray<AudioLogEntry> CommandHistoryLast(HistoryManager historyManager, int amount)
		{
			var query = new SeachQuery { MaxResults = amount };
			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format(results));
		}

		[Command("history last", "Plays the last song again")]
		public static void CommandHistoryLast(PlayManager playManager, HistoryManager historyManager, InvokerData invoker)
		{
			var ale = historyManager.Search(new SeachQuery { MaxResults = 1 }).FirstOrDefault();
			if (ale == null)
				throw new CommandException("There is no song in the history", CommandExceptionReason.CommandError);
			playManager.Play(invoker, ale.AudioResource).UnwrapThrow();
		}

		[Command("history play", "<id> Playes the song with <id>")]
		public static void CommandHistoryPlay(PlayManager playManager, InvokerData invoker, uint id)
			=> playManager.Play(invoker, id).UnwrapThrow();

		[Command("history rename", "<id> <name> Sets the name of the song with <id> to <name>")]
		public static void CommandHistoryRename(HistoryManager historyManager, uint id, string newName)
		{
			var ale = historyManager.GetEntryById(id).UnwrapThrow();

			if (string.IsNullOrWhiteSpace(newName))
				throw new CommandException("The new name must not be empty or only whitespaces", CommandExceptionReason.CommandError);

			historyManager.RenameEntry(ale, newName);
		}

		[Command("history till", "<date> Gets all songs played until <date>.")]
		public static JsonArray<AudioLogEntry> CommandHistoryTill(HistoryManager historyManager, DateTime time)
		{
			var query = new SeachQuery { LastInvokedAfter = time };
			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format(results));
		}

		[Command("history till", "<name> Any of those desciptors: (hour|today|yesterday|week)")]
		public static JsonArray<AudioLogEntry> CommandHistoryTill(HistoryManager historyManager, string time)
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
			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format(results));
		}

		[Command("history title", "Gets all songs which title contains <string>")]
		public static JsonArray<AudioLogEntry> CommandHistoryTitle(HistoryManager historyManager, string part)
		{
			var query = new SeachQuery { TitlePart = part };
			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format(results));
		}

		[Command("if")]
		[Usage("<argument0> <comparator> <argument1> <then>", "Compares the two arguments and returns or executes the then-argument")]
		[Usage("<argument0> <comparator> <argument1> <then> <else>", "Same as before and return the else-arguments if the condition is false")]
		public static ICommandResult CommandIf(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			if (arguments.Count < 4)
				throw new CommandException("Expected at least 4 arguments", CommandExceptionReason.MissingParameter);
			var arg0 = ((StringCommandResult)arguments[0].Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
			var cmp = ((StringCommandResult)arguments[1].Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
			var arg1 = ((StringCommandResult)arguments[2].Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;

			Func<double, double, bool> comparer;
			switch (cmp)
			{
			case "<": comparer = (a, b) => a < b; break;
			case ">": comparer = (a, b) => a > b; break;
			case "<=": comparer = (a, b) => a <= b; break;
			case ">=": comparer = (a, b) => a >= b; break;
			case "==": comparer = (a, b) => Math.Abs(a - b) < 1e-6; break;
			case "!=": comparer = (a, b) => Math.Abs(a - b) > 1e-6; break;
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
				return arguments[3].Execute(info, StaticList.Empty<ICommand>(), returnTypes);
			// Else branch
			if (arguments.Count > 4)
				return arguments[4].Execute(info, StaticList.Empty<ICommand>(), returnTypes);

			// Try to return nothing
			if (returnTypes.Contains(CommandResultType.Empty))
				return new EmptyCommandResult();
			throw new CommandException("If found nothing to return", CommandExceptionReason.MissingParameter);
		}

		[Command("json merge", "Allows you to combine multiple JsonResults into one")]
		public static JsonArray<object> CommandJsonMerge(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			if (arguments.Count == 0)
				return new JsonArray<object>(new object[0], string.Empty);

			var jsonArr = arguments
				.Select(arg => arg.Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnJson))
				.Where(arg => arg.ResultType == CommandResultType.Json)
				.OfType<JsonCommandResult>()
				.Select(arg => arg.JsonObject.GetSerializeObject())
				.ToArray();

			return new JsonArray<object>(jsonArr, string.Empty);
		}

		[Command("kickme", "Guess what?")]
		[Usage("[far]", "Optional attribute for the extra punch strength")]
		public static void CommandKickme(TeamspeakControl queryConnection, InvokerData invoker, CallerInfo caller, string parameter = null)
		{
			if (caller.ApiCall)
				throw new CommandException("This command is not available as API", CommandExceptionReason.NotSupported);

			if (invoker.ClientId.HasValue)
			{
				var result = R.OkR;
				if (string.IsNullOrEmpty(parameter) || parameter == "near")
					result = queryConnection.KickClientFromChannel(invoker.ClientId.Value);
				else if (parameter == "far")
					result = queryConnection.KickClientFromServer(invoker.ClientId.Value);
				if (!result.Ok)
					throw new CommandException("I'm not strong enough, master!", CommandExceptionReason.CommandError);
			}
		}

		[Command("link", "Gets a link to the origin of the current song.")]
		public static string CommandLink(ResourceFactoryManager factoryManager, PlayManager playManager, Bot bot, CallerInfo caller, InvokerData invoker = null)
		{
			if (playManager.CurrentPlayData == null)
				throw new CommandException("There is nothing on right now...", CommandExceptionReason.CommandError);
			if (bot.QuizMode && !caller.ApiCall && (invoker == null || playManager.CurrentPlayData.Invoker.ClientId != invoker.ClientId))
				throw new CommandException("Sorry, you have to guess!", CommandExceptionReason.CommandError);

			return factoryManager.RestoreLink(playManager.CurrentPlayData.ResourceData);
		}

		[Command("list add", "Adds a link to your private playlist.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public static void CommandListAdd(ResourceFactoryManager factoryManager, UserSession session, InvokerData invoker, string link)
		{
			var plist = AutoGetPlaylist(session, invoker);
			var playResource = factoryManager.Load(link).UnwrapThrow();
			plist.AddItem(new PlaylistItem(playResource.BaseData, new MetaData { ResourceOwnerDbId = invoker.DatabaseId }));
		}

		[Command("list add", "<id> Adds a link to your private playlist from the history by <id>.")]
		public static void CommandListAdd(HistoryManager historyManager, UserSession session, InvokerData invoker, uint hid)
		{
			var plist = AutoGetPlaylist(session, invoker);

			if (!historyManager.GetEntryById(hid))
				throw new CommandException("History entry not found", CommandExceptionReason.CommandError);

			plist.AddItem(new PlaylistItem(hid, new MetaData { ResourceOwnerDbId = invoker.DatabaseId }));
		}

		[Command("list clear", "Clears your private playlist.")]
		public static void CommandListClear(UserSession session, InvokerData invoker) => AutoGetPlaylist(session, invoker).Clear();

		[Command("list delete", "<name> Deletes the playlist with the name <name>. You can only delete playlists which you also have created. Admins can delete every playlist.")]
		public static JsonEmpty CommandListDelete(ExecutionInformation info, PlaylistManager playlistManager, CallerInfo caller, InvokerData invoker, string name, UserSession session = null)
		{
			if (caller.ApiCall)
				playlistManager.DeletePlaylist(name, invoker.DatabaseId ?? 0, info.HasRights(RightDeleteAllPlaylists)).UnwrapThrow();

			bool? canDeleteAllPlaylists = null;

			string ResponseListDelete(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					playlistManager.DeletePlaylist(name, invoker.DatabaseId ?? 0, canDeleteAllPlaylists ?? info.HasRights(RightDeleteAllPlaylists)).UnwrapThrow();
				}
				return null;
			}

			var hresult = playlistManager.LoadPlaylist(name, true);
			if (!hresult)
			{
				session.SetResponse(ResponseListDelete);
				// TODO check if return == string => ask, return == empty => just delete
				return new JsonEmpty($"Do you really want to delete the playlist \"{name}\" (error:{hresult.Error})");
			}
			else
			{
				canDeleteAllPlaylists = info.HasRights(RightDeleteAllPlaylists);
				if (hresult.Value.CreatorDbId != invoker.DatabaseId && !canDeleteAllPlaylists.Value)
					throw new CommandException("You are not allowed to delete others playlists", CommandExceptionReason.MissingRights);

				session.SetResponse(ResponseListDelete);
				return new JsonEmpty($"Do you really want to delete the playlist \"{name}\"");
			}
		}

		[Command("list get", "<link> Imports a playlist form an other plattform like youtube etc.")]
		public static JsonEmpty CommandListGet(ResourceFactoryManager factoryManager, UserSession session, InvokerData invoker, string link)
		{
			var playlist = factoryManager.LoadPlaylistFrom(link).UnwrapThrow();

			playlist.CreatorDbId = invoker.DatabaseId;
			session.Set<PlaylistManager, Playlist>(playlist);
			return new JsonEmpty("Ok");
		}

		[Command("list item move", "<from> <to> Moves a item in a playlist <from> <to> position.")]
		public static void CommandListMove(UserSession session, InvokerData invoker, int from, int to)
		{
			var plist = AutoGetPlaylist(session, invoker);

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
		public static string CommandListRemove(UserSession session, InvokerData invoker, int index)
		{
			var plist = AutoGetPlaylist(session, invoker);

			if (index < 0 || index >= plist.Count)
				throw new CommandException("Index must be within playlist length", CommandExceptionReason.CommandError);

			var deletedItem = plist.GetResource(index);
			plist.RemoveItemAt(index);
			return "Removed: " + deletedItem.DisplayString;
		}

		// add list item rename

		[Command("list list", "Displays all available playlists from all users.")]
		[Usage("<pattern>", "Filters all lists cantaining the given pattern.")]
		public static JsonArray<string> CommandListList(PlaylistManager playlistManager, string pattern = null)
		{
			var files = playlistManager.GetAvailablePlaylists(pattern).ToArray();
			if (files.Length <= 0)
				return new JsonArray<string>(files, "No playlists found");

			var strb = new StringBuilder();
			int tokenLen = 0;
			foreach (var file in files)
			{
				int newTokenLen = tokenLen + TS3Client.Commands.Ts3String.TokenLength(file) + 3;
				if (newTokenLen < TS3Client.Commands.Ts3Const.MaxSizeTextMessage)
				{
					strb.Append(file).Append(", ");
					tokenLen = newTokenLen;
				}
				else
					break;
			}

			if (strb.Length > 2)
				strb.Length -= 2;
			return new JsonArray<string>(files, strb.ToString());
		}

		[Command("list load", "Opens a playlist to be editable for you. This replaces your current worklist with the opened playlist.")]
		public static JsonValue<Playlist> CommandListLoad(PlaylistManager playlistManager, UserSession session, InvokerData invoker, string name)
		{
			var loadList = AutoGetPlaylist(session, invoker);

			var playList = playlistManager.LoadPlaylist(name).UnwrapThrow();

			loadList.Clear();
			loadList.AddRange(playList.AsEnumerable());
			loadList.Name = playList.Name;
			return new JsonValue<Playlist>(loadList, $"Loaded: \"{name}\" with {loadList.Count} songs");
		}

		[Command("list merge", "Appends another playlist to yours.")]
		public static void CommandListMerge(PlaylistManager playlistManager, UserSession session, InvokerData invoker, string name)
		{
			var plist = AutoGetPlaylist(session, invoker);

			var lresult = playlistManager.LoadPlaylist(name);
			if (!lresult)
				throw new CommandException("The other playlist could not be found", CommandExceptionReason.CommandError);

			plist.AddRange(lresult.Value.AsEnumerable());
		}

		[Command("list name", "Displays the name of the playlist you are currently working on.")]
		[Usage("<name>", "Changes the playlist name to <name>.")]
		public static string CommandListName(UserSession session, InvokerData invoker, string name)
		{
			var plist = AutoGetPlaylist(session, invoker);

			if (string.IsNullOrEmpty(name))
				return plist.Name;

			PlaylistManager.IsNameValid(name).UnwrapThrow();

			plist.Name = name;
			return null;
		}

		[Command("list play", "Replaces the current freelist with your workinglist and plays from the beginning.")]
		[Usage("<index>", "Lets you specify the starting song index.")]
		public static void CommandListPlay(PlaylistManager playlistManager, PlayManager playManager, UserSession session, InvokerData invoker, int? index = null)
		{
			var plist = AutoGetPlaylist(session, invoker);

			if (!index.HasValue || (index.Value >= 0 && index.Value < plist.Count))
			{
				playlistManager.PlayFreelist(plist);
				playlistManager.Index = index ?? 0;
			}
			else
				throw new CommandException("Invalid starting index", CommandExceptionReason.CommandError);

			var item = playlistManager.Current();
			if (item != null)
				playManager.Play(invoker, item).UnwrapThrow();
			else
				throw new CommandException("Nothing to play...", CommandExceptionReason.CommandError);
		}

		[Command("list queue", "Appends your playlist to the freelist.")]
		public static void CommandListQueue(PlayManager playManager, UserSession session, InvokerData invoker)
		{
			var plist = AutoGetPlaylist(session, invoker);
			playManager.Enqueue(plist.AsEnumerable()).UnwrapThrow();
		}

		[Command("list save", "Stores your current workinglist to disk.")]
		[Usage("<name>", "Changes the playlist name to <name> before saving.")]
		public static void CommandListSave(PlaylistManager playlistManager, UserSession session, InvokerData invoker, string optNewName = null)
		{
			var plist = AutoGetPlaylist(session, invoker);
			if (!string.IsNullOrEmpty(optNewName))
			{
				PlaylistManager.IsNameValid(optNewName).UnwrapThrow();
				plist.Name = optNewName;
			}

			playlistManager.SavePlaylist(plist).UnwrapThrow();
		}

		[Command("list show", "Displays all songs currently in the playlists you are working on")]
		[Usage("<index>", "Lets you specify the staring index from which songs should be listed.")]
		public static JsonArray<PlaylistItem> CommandListShow(PlaylistManager playlistManager, UserSession session, InvokerData invoker, int? offset = null) => CommandListShow(playlistManager, session, invoker, null, offset);

		[Command("list show", "<name> Displays all songs currently in the playlists with the name <name>")]
		[Usage("<name> <index>", "Lets you specify the starting index from which songs should be listed.")]
		public static JsonArray<PlaylistItem> CommandListShow(PlaylistManager playlistManager, UserSession session, InvokerData invoker, string name = null, int? offset = null)
		{
			Playlist plist;
			if (!string.IsNullOrEmpty(name))
				plist = playlistManager.LoadPlaylist(name).UnwrapThrow();
			else
				plist = AutoGetPlaylist(session, invoker);

			var strb = new StringBuilder();
			strb.Append("Playlist: \"").Append(plist.Name).Append("\" with ").Append(plist.Count).AppendLine(" songs.");
			int from = Math.Max(offset ?? 0, 0);
			var items = plist.AsEnumerable().Skip(from).ToArray();
			foreach (var plitem in items.Take(10))
				strb.Append(from++).Append(": ").AppendLine(plitem.DisplayString);

			return new JsonArray<PlaylistItem>(items, strb.ToString());
		}

		[Command("loop", "Gets whether or not to loop the entire playlist.")]
		public static JsonValue<bool> CommandLoop(PlaylistManager playlistManager) => new JsonValue<bool>(playlistManager.Loop, "Loop is " + (playlistManager.Loop ? "on" : "off"));
		[Command("loop on", "Enables looping the entire playlist.")]
		public static void CommandLoopOn(PlaylistManager playlistManager) => playlistManager.Loop = true;
		[Command("loop off", "Disables looping the entire playlist.")]
		public static void CommandLoopOff(PlaylistManager playlistManager) => playlistManager.Loop = false;

		[Command("next", "Plays the next song in the playlist.")]
		public static void CommandNext(PlayManager playManager, InvokerData invoker)
		{
			playManager.Next(invoker).UnwrapThrow();
		}

		[Command("pm", "Requests a private session with the ServerBot so you can be intimate.")]
		public static string CommandPm(CallerInfo caller, InvokerData invoker)
		{
			if (caller.ApiCall)
				throw new CommandException("This command is not available as API", CommandExceptionReason.NotSupported);
			invoker.Visibiliy = TextMessageTargetMode.Private;
			return "Hi " + (invoker.NickName ?? "Anonymous");
		}

		[Command("parse command", "Displays the AST of the requested command.")]
		[Usage("<command>", "The command to be parsed")]
		public static JsonValue<AstNode> CommandParse(string parameter)
		{
			if (!parameter.TrimStart().StartsWith("!", StringComparison.Ordinal))
				throw new CommandException("This is not a command", CommandExceptionReason.CommandError);
			try
			{
				var node = CommandParser.ParseCommandRequest(parameter);
				var strb = new StringBuilder();
				strb.AppendLine();
				node.Write(strb, 0);
				return new JsonValue<AstNode>(node, strb.ToString());
			}
			catch (Exception ex)
			{
				throw new CommandException("GJ - You crashed it!!!", ex, CommandExceptionReason.CommandError);
			}
		}

		[Command("pause", "Well, pauses the song. Undo with !play.")]
		public static void CommandPause(Bot bot) => bot.PlayerConnection.Paused = true;

		[Command("play", "Automatically tries to decide whether the link is a special resource (like youtube) or a direct resource (like ./hello.mp3) and starts it.")]
		[Usage("<link>", "Youtube, Soundcloud, local path or file link")]
		public static void CommandPlay(IPlayerConnection playerConnection, PlayManager playManager, InvokerData invoker, string parameter = null)
		{
			if (string.IsNullOrEmpty(parameter))
				playerConnection.Paused = false;
			else
				playManager.Play(invoker, parameter).UnwrapThrow();
		}

		[Command("plugin list", "Lists all found plugins.")]
		public static JsonArray<PluginStatusInfo> CommandPluginList(PluginManager pluginManager, Bot bot = null)
		{
			var overview = pluginManager.GetPluginOverview(bot);
			return new JsonArray<PluginStatusInfo>(overview, PluginManager.FormatOverview(overview));
		}

		[Command("plugin unload", "Unloads a plugin.")]
		public static void CommandPluginUnload(PluginManager pluginManager, string identifier, Bot bot = null)
		{
			var result = pluginManager.StopPlugin(identifier, bot);
			if (result != PluginResponse.Ok)
				throw new CommandException("Plugin error: " + result, CommandExceptionReason.CommandError);
		}

		[Command("plugin load", "Unloads a plugin.")]
		public static void CommandPluginLoad(PluginManager pluginManager, string identifier, Bot bot = null)
		{
			var result = pluginManager.StartPlugin(identifier, bot);
			if (result != PluginResponse.Ok)
				throw new CommandException("Plugin error: " + result, CommandExceptionReason.CommandError);
		}

		[Command("previous", "Plays the previous song in the playlist.")]
		public static void CommandPrevious(PlayManager playManager, InvokerData invoker)
			=> playManager.Previous(invoker).UnwrapThrow();

		[Command("print", "Lets you format multiple parameter to one.")]
		public static string CommandPrint(params string[] parameter)
		{
			// XXX << Design changes expected >>
			var strb = new StringBuilder();
			foreach (var param in parameter)
				strb.Append(param);
			return strb.ToString();
		}

		[Command("quit", "Closes the TS3AudioBot application.")]
		public static JsonEmpty CommandQuit(Core core, CallerInfo caller, UserSession session = null, string param = null)
		{
			if (caller.ApiCall)
			{
				core.Dispose();
				return new JsonEmpty(string.Empty);
			}

			if (param == "force")
			{
				core.Dispose();
				return new JsonEmpty(string.Empty);
			}

			string ResponseQuit(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					CommandQuit(core, caller, session, "force");
				}
				return null;
			}
			
			session.SetResponse(ResponseQuit);
			return new JsonEmpty("Do you really want to quit? !(yes|no)");
		}

		[Command("quiz", "Shows the quizmode status.")]
		public static JsonValue<bool> CommandQuiz(Bot bot) => new JsonValue<bool>(bot.QuizMode, "Quizmode is " + (bot.QuizMode ? "on" : "off"));
		[Command("quiz on", "Enable to hide the songnames and let your friends guess the title.")]
		public static void CommandQuizOn(Bot bot)
		{
			bot.QuizMode = true;
			bot.UpdateBotStatus().UnwrapThrow();
		}
		[Command("quiz off", "Disable to show the songnames again.")]
		public static void CommandQuizOff(Bot bot, CallerInfo caller, InvokerData invoker)
		{
			if (!caller.ApiCall && invoker.Visibiliy.HasValue && invoker.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("No cheating! Everybody has to see it!", CommandExceptionReason.CommandError);
			bot.QuizMode = false;
			bot.UpdateBotStatus().UnwrapThrow();
		}

		[Command("random", "Gets whether or not to play playlists in random order.")]
		public static JsonValue<bool> CommandRandom(PlaylistManager playlistManager) => new JsonValue<bool>(playlistManager.Random, "Random is " + (playlistManager.Random ? "on" : "off"));
		[Command("random on", "Enables random playlist playback")]
		public static void CommandRandomOn(PlaylistManager playlistManager) => playlistManager.Random = true;
		[Command("random off", "Disables random playlist playback")]
		public static void CommandRandomOff(PlaylistManager playlistManager) => playlistManager.Random = false;
		[Command("random seed", "Gets the unique seed for a certain playback order")]
		public static string CommandRandomSeed(PlaylistManager playlistManager)
		{
			string seed = Util.FromSeed(playlistManager.Seed);
			return string.IsNullOrEmpty(seed) ? "<empty>" : seed;
		}
		[Command("random seed", "Sets the unique seed for a certain playback order")]
		public static void CommandRandomSeed(PlaylistManager playlistManager, string newSeed)
		{
			if (newSeed.Any(c => !char.IsLetter(c)))
				throw new CommandException("Only letters allowed", CommandExceptionReason.CommandError);
			playlistManager.Seed = Util.ToSeed(newSeed.ToLowerInvariant());
		}
		[Command("random seed", "Sets the unique seed for a certain playback order")]
		public static void CommandRandomSeed(PlaylistManager playlistManager, int newSeed) => playlistManager.Seed = newSeed;

		[Command("repeat", "Gets whether or not to loop a single song.")]
		public static JsonValue<bool> CommandRepeat(IPlayerConnection playerConnection) => new JsonValue<bool>(playerConnection.Repeated, "Repeat is " + (playerConnection.Repeated ? "on" : "off"));
		[Command("repeat on", "Enables single song repeat.")]
		public static void CommandRepeatOn(IPlayerConnection playerConnection) => playerConnection.Repeated = true;
		[Command("repeat off", "Disables single song repeat.")]
		public static void CommandRepeatOff(IPlayerConnection playerConnection) => playerConnection.Repeated = false;

		[Command("rights can", "Returns the subset of allowed commands the caller (you) can execute.")]
		public static JsonArray<string> CommandRightsCan(RightsManager rightsManager, TeamspeakControl ts, CallerInfo caller, InvokerData invoker = null, params string[] rights)
		{
			var result = rightsManager.GetRightsSubset(caller, invoker, ts, rights);
			return new JsonArray<string>(result, result.Length > 0 ? string.Join(", ", result) : "No");
		}

		[Command("rights reload", "Reloads the rights configuration from file.")]
		public static JsonEmpty CommandRightsReload(RightsManager rightsManager)
		{
			if (rightsManager.ReadFile())
				return new JsonEmpty("Ok");

			// TODO: this can be done nicer by returning the errors and warnings from parsing
			throw new CommandException("Error while parsing file, see log for more details", CommandExceptionReason.CommandError);
		}

		[Command("rng", "Gets a random number.")]
		[Usage("", "Gets a number between 0 and 2147483647")]
		[Usage("<max>", "Gets a number between 0 and <max>")]
		[Usage("<min> <max>", "Gets a number between <min> and <max>")]
		public static int CommandRng(int? first = null, int? second = null)
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
			return num;
		}

		[Command("seek", "Jumps to a timemark within the current song.")]
		[Usage("<sec>", "Time in seconds")]
		[Usage("<min:sec>", "Time in Minutes:Seconds")]
		public static void CommandSeek(IPlayerConnection playerConnection, string parameter)
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
			else if (span < TimeSpan.Zero || span > playerConnection.Length)
				throw new CommandException("The point of time is not within the song length.", CommandExceptionReason.CommandError);
			else
				playerConnection.Position = span;
		}

		[Command("settings", "Changes values from the settigns. Not all changes can be applied immediately.")]
		[Usage("<key>", "Get the value of a setting")]
		[Usage("<key> <value>", "Set the value of a setting")]
		public static JsonValue<KeyValuePair<string, string>> CommandSettings(ConfigFile configManager, string key = null, string value = null)
		{
			var configMap = configManager.GetConfigMap();
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
					return new JsonValue<KeyValuePair<string, string>>(filteredArr[0], filteredArr[0].Key + " = " + filteredArr[0].Value);
				else
				{
					configManager.SetSetting(filteredArr[0].Key, value).UnwrapThrow();
					return null;
				}
			}
			else
			{
				throw new CommandException("Found more than one matching key: \n  " + string.Join("\n  ", filteredArr.Take(3).Select(kvp => kvp.Key)),
					CommandExceptionReason.CommandError);
			}
		}

		[Command("song", "Tells you the name of the current song.")]
		public static JsonValue<string> CommandSong(PlayManager playManager, ResourceFactoryManager factoryManager, Bot bot, CallerInfo caller, InvokerData invoker = null)
		{
			if (playManager.CurrentPlayData == null)
				throw new CommandException("There is nothing on right now...", CommandExceptionReason.CommandError);
			if (bot.QuizMode && !caller.ApiCall && (invoker == null || playManager.CurrentPlayData.Invoker.ClientId != invoker.ClientId))
				throw new CommandException("Sorry, you have to guess!", CommandExceptionReason.CommandError);

			return new JsonValue<string>(
				playManager.CurrentPlayData.ResourceData.ResourceTitle,
				$"[url={factoryManager.RestoreLink(playManager.CurrentPlayData.ResourceData)}]{playManager.CurrentPlayData.ResourceData.ResourceTitle}[/url]");
		}

		[Command("stop", "Stops the current song.")]
		public static void CommandStop(PlayManager playManager) => playManager.Stop();

		[Command("subscribe", "Lets you hear the music independent from the channel you are in.")]
		public static void CommandSubscribe(ITargetManager targetManager, InvokerData invoker)
		{
			if (invoker.ClientId.HasValue)
				targetManager.WhisperClientSubscribe(invoker.ClientId.Value);
		}

		[Command("subscribe tempchannel", "Adds your current channel to the music playback.")]
		public static void CommandSubscribeTempChannel(ITargetManager targetManager, InvokerData invoker, ulong? channel = null)
		{
			var subChan = channel ?? invoker.ChannelId ?? 0;
			if (subChan != 0)
				targetManager.WhisperChannelSubscribe(subChan, true);
		}

		[Command("subscribe channel", "Adds your current channel to the music playback.")]
		public static void CommandSubscribeChannel(ITargetManager targetManager, InvokerData invoker, ulong? channel = null)
		{
			var subChan = channel ?? invoker.ChannelId ?? 0;
			if (subChan != 0)
				targetManager.WhisperChannelSubscribe(subChan, false);
		}

		[Command("take", "Take a substring from a string.")]
		[Usage("<count> <text>", "Take only <count> parts of the text")]
		[Usage("<count> <start> <text>", "Take <count> parts, starting with the part at <start>")]
		[Usage("<count> <start> <delimiter> <text>", "Specify another delimiter for the parts than spaces")]
		public static ICommandResult CommandTake(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			if (arguments.Count < 2)
				throw new CommandException("Expected at least 2 parameters", CommandExceptionReason.MissingParameter);

			int start = 0;
			string delimiter = null;

			// Get count
			var res = ((StringCommandResult)arguments[0].Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
			if (!int.TryParse(res, out int count) || count < 0)
				throw new CommandException("Count must be an integer >= 0", CommandExceptionReason.CommandError);

			if (arguments.Count > 2)
			{
				// Get start
				res = ((StringCommandResult)arguments[1].Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
				if (!int.TryParse(res, out start) || start < 0)
					throw new CommandException("Start must be an integer >= 0", CommandExceptionReason.CommandError);
			}

			if (arguments.Count > 3)
				// Get delimiter
				delimiter = ((StringCommandResult)arguments[2].Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;

			string text = ((StringCommandResult)arguments[Math.Min(arguments.Count - 1, 3)]
				.Execute(info, StaticList.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;

			var splitted = delimiter == null
				? text.Split()
				: text.Split(new[] { delimiter }, StringSplitOptions.None);
			if (splitted.Length < start + count)
				throw new CommandException("Not enough arguments to take", CommandExceptionReason.CommandError);
			var splittedarr = splitted.Skip(start).Take(count).ToArray();

			foreach (var returnType in returnTypes)
			{
				if (returnType == CommandResultType.String)
					return new StringCommandResult(string.Join(delimiter ?? " ", splittedarr));
				if (returnType == CommandResultType.Json)
					return new JsonCommandResult(new JsonArray<string>(splittedarr, string.Join(delimiter ?? " ", splittedarr)));
			}

			throw new CommandException("Can't find a fitting return type for take", CommandExceptionReason.NoReturnMatch);
		}

		[Command("unsubscribe", "Only lets you hear the music in active channels again.")]
		public static void CommandUnsubscribe(ITargetManager targetManager, InvokerData invoker)
		{
			if (invoker.ClientId.HasValue)
				targetManager.WhisperClientUnsubscribe(invoker.ClientId.Value);
		}

		[Command("unsubscribe channel", "Removes your current channel from the music playback.")]
		public static void CommandUnsubscribeChannel(ITargetManager targetManager, InvokerData invoker, ulong? channel = null)
		{
			var subChan = channel ?? invoker.ChannelId ?? 0;
			if (subChan != 0)
				targetManager.WhisperChannelUnsubscribe(subChan, false);
		}

		[Command("unsubscribe temporary", "Clears all temporary targets.")]
		public static void CommandUnsubscribeTemporary(ITargetManager targetManager) => targetManager.ClearTemporary();

		[Command("version", "Gets the current build version.")]
		public static JsonValue<BuildData> CommandVersion()
		{
			var data = SystemData.AssemblyData;
			return new JsonValue<BuildData>(data, data.ToLongString());
		}

		[Command("volume", "Sets the volume level of the music.")]
		[Usage("<level>", "A new volume level between 0 and 100.")]
		[Usage("+/-<level>", "Adds or subtracts a value from the current volume.")]
		public static JsonValue<float> CommandVolume(ExecutionInformation info, IPlayerConnection playerConnection, CallerInfo caller, UserSession session = null, string parameter = null)
		{
			if (string.IsNullOrEmpty(parameter))
				return new JsonValue<float>(playerConnection.Volume, "Current volume: " + playerConnection.Volume);

			bool relPos = parameter.StartsWith("+", StringComparison.Ordinal);
			bool relNeg = parameter.StartsWith("-", StringComparison.Ordinal);
			string numberString = (relPos || relNeg) ? parameter.Remove(0, 1) : parameter;

			if (!float.TryParse(numberString, NumberStyles.Float, CultureInfo.InvariantCulture, out var volume))
				throw new CommandException("The new volume could not be parsed", CommandExceptionReason.CommandError);

			float newVolume;
			if (relPos) newVolume = playerConnection.Volume + volume;
			else if (relNeg) newVolume = playerConnection.Volume - volume;
			else newVolume = volume;

			if (newVolume < 0 || newVolume > AudioValues.MaxVolume)
				throw new CommandException("The volume level must be between 0 and " + AudioValues.MaxVolume, CommandExceptionReason.CommandError);

			if (newVolume <= AudioValues.MaxUserVolume || newVolume < playerConnection.Volume || caller.ApiCall)
				playerConnection.Volume = newVolume;
			else if (newVolume <= AudioValues.MaxVolume)
			{
				string ResponseVolume(string message)
				{
					if (TextUtil.GetAnswer(message) == Answer.Yes)
					{
						if (info.HasRights(RightHighVolume))
							playerConnection.Volume = newVolume;
						else
							return "You are not allowed to set higher volumes.";
					}
					return null;
				}
				
				session.SetResponse(ResponseVolume);
				throw new CommandException("Careful you are requesting a very high volume! Do you want to apply this? !(yes|no)", CommandExceptionReason.CommandError);
			}
			return null;
		}

		[Command("whisper off", "Enables normal voice mode.")]
		public static void CommandWhisperOff(ITargetManager targetManager) => targetManager.SendMode = TargetSendMode.Voice;

		[Command("whisper subscription", "Enables default whisper subscription mode.")]
		public static void CommandWhisperSubsription(ITargetManager targetManager) => targetManager.SendMode = TargetSendMode.Whisper;

		[Command("whisper all", "Set how to send music.")]
		public static void CommandWhisperAll(ITargetManager targetManager) => CommandWhisperGroup(targetManager, GroupWhisperType.AllClients, GroupWhisperTarget.AllChannels);

		[Command("whisper group", "Set a specific teamspeak whisper group.")]
		public static void CommandWhisperGroup(ITargetManager targetManager, GroupWhisperType type, GroupWhisperTarget target, ulong? targetId = null)
		{
			if (type == GroupWhisperType.ServerGroup || type == GroupWhisperType.ChannelGroup)
			{
				if (!targetId.HasValue)
					throw new CommandException("This type requires an additional target", CommandExceptionReason.CommandError);
				targetManager.SetGroupWhisper(type, target, targetId.Value);
				targetManager.SendMode = TargetSendMode.WhisperGroup;
			}
			else
			{
				if (targetId.HasValue)
					throw new CommandException("This type does not take an additional target", CommandExceptionReason.CommandError);
				targetManager.SetGroupWhisper(type, target, 0);
				targetManager.SendMode = TargetSendMode.WhisperGroup;
			}
		}

		private static readonly CommandResultType[] ReturnPreferNothingAllowAny = { CommandResultType.Empty, CommandResultType.String, CommandResultType.Json, CommandResultType.Command };

		[Command("xecute", "Evaluates all parameter.")]
		public static void CommandXecute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			foreach (var arg in arguments)
				arg.Execute(info, StaticList.Empty<ICommand>(), ReturnPreferNothingAllowAny);
		}
		// ReSharper enable UnusedMember.Global

		private static Playlist AutoGetPlaylist(UserSession session, InvokerData invoker)
		{
			if (session == null)
				throw new CommandException("Missing session context", CommandExceptionReason.CommandError);
			var result = session.Get<PlaylistManager, Playlist>();
			if (result)
				return result.Value;

			var newPlist = new Playlist(invoker.NickName, invoker.DatabaseId);
			session.Set<PlaylistManager, Playlist>(newPlist);
			return newPlist;
		}

		public static bool HasRights(this ExecutionInformation info, params string[] rights)
		{
			if (!info.TryGet<CallerInfo>(out var caller)) caller = null;
			if (caller?.SkipRightsChecks ?? false)
				return true;
			if (!info.TryGet<RightsManager>(out var rightsManager))
				return false;
			if (!info.TryGet<InvokerData>(out var invoker)) invoker = null;
			if (!info.TryGet<TeamspeakControl>(out var ts)) ts = null;
			return rightsManager.HasAllRights(caller, invoker, ts, rights);
		}

		public static R Write(this ExecutionInformation info, string message)
		{
			if (!info.TryGet<TeamspeakControl>(out var queryConnection))
				return "No teamspeak connection in context";

			if (!info.TryGet<InvokerData>(out var invoker))
				return "No invoker in context";

			if (!invoker.Visibiliy.HasValue || !invoker.ClientId.HasValue)
				return "Invoker casted Ghost Walk";

			switch (invoker.Visibiliy.Value)
			{
			case TextMessageTargetMode.Private:
				return queryConnection.SendMessage(message, invoker.ClientId.Value);
			case TextMessageTargetMode.Channel:
				return queryConnection.SendChannelMessage(message);
			case TextMessageTargetMode.Server:
				return queryConnection.SendServerMessage(message);
			default:
				throw Util.UnhandledDefault(invoker.Visibiliy.Value);
			}
		}
	}
}
