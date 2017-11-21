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
	using Helper;
	using History;
	using Plugins;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using TS3Client;
	using TS3Client.Messages;
	using Web.Api;

	public static class Commands
	{
		#region COMMANDS

		public const string RightHighVolume = "ts3ab.admin.volume";
		public const string RightDeleteAllPlaylists = "ts3ab.admin.list";

		// [...] = Optional
		// <name> = Placeholder for a text
		// [text] = Option for fixed text
		// (a|b) = either or switch

		// ReSharper disable UnusedMember.Global
		[Command("add", "Adds a new song to the queue.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public static void CommandAdd(ExecutionInformation info, string parameter)
			=> info.Bot.PlayManager.Enqueue(info.InvokerData, parameter).UnwrapThrow();

		[Command("api token", "Generates an api token.")]
		[Usage("[<link>]", "Optionally specifies a duration this key is valid in hours.")]
		[RequiredParameters(0)]
		public static JsonObject CommandApiToken(ExecutionInformation info, double? validHours)
		{
			if (info.InvokerData.Visibiliy.HasValue && info.InvokerData.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("Please use this command in a private session.", CommandExceptionReason.CommandError);
			if (info.InvokerData.ClientUid == null)
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
			var token = info.Bot.SessionManager.GenerateToken(info.InvokerData.ClientUid, validSpan).UnwrapThrow();
			return new JsonSingleValue<string>(token);
		}

		[Command("api nonce", "Generates an api nonce.")]
		public static JsonObject CommandApiNonce(ExecutionInformation info)
		{
			if (info.InvokerData.Visibiliy.HasValue && info.InvokerData.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("Please use this command in a private session.", CommandExceptionReason.CommandError);
			if (info.InvokerData.ClientUid == null)
				throw new CommandException("No Uid found to register token for.", CommandExceptionReason.CommandError);
			var result = info.Bot.SessionManager.GetToken(info.InvokerData.ClientUid);
			if (!result.Ok)
				throw new CommandException("No active token found.", CommandExceptionReason.CommandError);

			var nonce = result.Value.CreateNonce();
			return new JsonSingleValue<string>(nonce.Value);
		}

		[Command("bot commander", "Gets the status of the channel commander mode.")]
		public static JsonObject CommandBotCommander(ExecutionInformation info)
		{
			var value = info.Bot.QueryConnection.IsChannelCommander().UnwrapThrow();
			return new JsonSingleValue<bool>("Channel commander is " + (value ? "on" : "off"), value);
		}
		[Command("bot commander on", "Enables channel commander.")]
		public static void CommandBotCommanderOn(ExecutionInformation info) => info.Bot.QueryConnection.SetChannelCommander(true).UnwrapThrow();
		[Command("bot commander off", "Disables channel commander.")]
		public static void CommandBotCommanderOff(ExecutionInformation info) => info.Bot.QueryConnection.SetChannelCommander(false).UnwrapThrow();

		[Command("bot come", "Moves the bot to you or a specified channel.")]
		[RequiredParameters(0)]
		public static void CommandBotCome(ExecutionInformation info, string password = null) => CommandBotMove(info, null, password);

		[Command("bot move", "Moves the bot to you or a specified channel.")]
		[RequiredParameters(1)]
		public static void CommandBotMove(ExecutionInformation info, ulong? channel, string password = null)
		{
			if (!channel.HasValue)
				channel = (CommandGetChannel(info) as JsonSingleValue<ulong>)?.Value;
			if (!channel.HasValue)
				throw new CommandException("No target channel found");
			info.Bot.QueryConnection.MoveTo(channel.Value, password).UnwrapThrow();
		}

		[Command("bot name", "Gives the bot a new name.")]
		public static void CommandBotName(ExecutionInformation info, string name) => info.Bot.QueryConnection.ChangeName(name).UnwrapThrow();

		[Command("bot badges", "Set your bot a badge. The badges string starts with 'overwolf=0:badges='")]
		public static void CommandBotBadges(ExecutionInformation info, string badgesString) => info.Bot.QueryConnection.ChangeBadges(badgesString).UnwrapThrow();

		[Command("bot setup", "Sets all teamspeak rights for the bot to be fully functional.")]
		[RequiredParameters(0)]
		public static void CommandBotSetup(ExecutionInformation info, string adminToken)
		{
			var mbd = info.Core.ConfigManager.GetDataStruct<MainBotData>("MainBot", true);
			info.Bot.QueryConnection.SetupRights(adminToken, mbd).UnwrapThrow();
		}

		[Command("clear", "Removes all songs from the current playlist.")]
		public static void CommandClear(ExecutionInformation info)
		{
			info.Bot.PlaylistManager.ClearFreelist();
		}

		[Command("connect", "Start a new bot instance.")]
		public static void CommandFork(ExecutionInformation info)
		{
			if (!info.Core.Bots.CreateBot())
				throw new CommandException("Could not create new instance");
		}

		[Command("disconnect", "Start a new bot instance.")]
		public static void CommandDisconnect(ExecutionInformation info)
		{
			info.Core.Bots.StopBot(info.Bot);
		}

		[Command("eval", "Executes a given command or string")]
		[Usage("<command> <arguments...>", "Executes the given command on arguments")]
		[Usage("<strings...>", "Concat the strings and execute them with the command system")]
		public static ICommandResult CommandEval(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			// Evaluate the first argument on the rest of the arguments
			if (arguments.Count == 0)
				throw new CommandException("Need at least one argument to evaluate", CommandExceptionReason.MissingParameter);
			var leftArguments = arguments.TrySegment(1);
			var arg0 = arguments[0].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.Command, CommandResultType.String });
			if (arg0.ResultType == CommandResultType.Command)
				return ((CommandCommandResult)arg0).Command.Execute(info, leftArguments, returnTypes);

			// We got a string back so parse and evaluate it
			var args = ((StringCommandResult)arg0).Content;

			// Add the rest of the arguments
			args += string.Join(" ", arguments.Select(a =>
				((StringCommandResult)a.Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content));

			var cmd = info.Core.CommandManager.CommandSystem.AstToCommandResult(CommandParser.ParseCommandRequest(args));
			return cmd.Execute(info, leftArguments, returnTypes);
		}

		[Command("getuser id", "Gets your id.")]
		public static JsonObject CommandGetId(ExecutionInformation info)
			=> info.InvokerData.ClientId.HasValue
			? new JsonSingleValue<ushort>(info.InvokerData.ClientId.Value)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser uid", "Gets your unique id.")]
		public static JsonObject CommandGetUid(ExecutionInformation info)
			=> info.InvokerData.ClientUid != null
			? new JsonSingleValue<string>(info.InvokerData.ClientUid)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser name", "Gets your nickname.")]
		public static JsonObject CommandGetName(ExecutionInformation info)
			=> info.InvokerData.NickName != null
			? new JsonSingleValue<string>(info.InvokerData.NickName)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser dbid", "Gets your database id.")]
		public static JsonObject CommandGetDbId(ExecutionInformation info)
			=> info.InvokerData.DatabaseId.HasValue
			? new JsonSingleValue<ulong>(info.InvokerData.DatabaseId.Value)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser channel", "Gets your channel id you are currently in.")]
		public static JsonObject CommandGetChannel(ExecutionInformation info)
			=> info.InvokerData.ChannelId.HasValue
			? new JsonSingleValue<ulong>(info.InvokerData.ChannelId.Value)
			: (JsonObject)new JsonError("Not found.", CommandExceptionReason.CommandError);
		[Command("getuser all", "Gets all information about you.")]
		public static JsonObject CommandGetUser(ExecutionInformation info)
		{
			var client = info.InvokerData;
			return new JsonSingleObject<InvokerData>($"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.ClientUid}", client);
		}

		[Command("getuser uid byid", "Gets the unique id of a user, searching with his id.")]
		public static JsonObject CommandGetUidById(ExecutionInformation info, ushort id) => new JsonSingleValue<string>(info.Bot.QueryConnection.GetClientById(id).UnwrapThrow().Uid);
		[Command("getuser name byid", "Gets the nickname of a user, searching with his id.")]
		public static JsonObject CommandGetNameById(ExecutionInformation info, ushort id) => new JsonSingleValue<string>(info.Bot.QueryConnection.GetClientById(id).UnwrapThrow().NickName);
		[Command("getuser dbid byid", "Gets the database id of a user, searching with his id.")]
		public static JsonObject CommandGetDbIdById(ExecutionInformation info, ushort id) => new JsonSingleValue<ulong>(info.Bot.QueryConnection.GetClientById(id).UnwrapThrow().DatabaseId);
		[Command("getuser channel byid", "Gets the channel id a user is currently in, searching with his id.")]
		public static JsonObject CommandGetChannelById(ExecutionInformation info, ushort id) => new JsonSingleValue<ulong>(info.Bot.QueryConnection.GetClientById(id).UnwrapThrow().ChannelId);
		[Command("getuser all byid", "Gets all information about a user, searching with his id.")]
		public static JsonObject CommandGetUserById(ExecutionInformation info, ushort id)
		{
			var client = info.Bot.QueryConnection.GetClientById(id).UnwrapThrow();
			return new JsonSingleObject<ClientData>($"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}", client);
		}
		[Command("getuser id byname", "Gets the id of a user, searching with his name.")]
		public static JsonObject CommandGetIdByName(ExecutionInformation info, string username) => new JsonSingleValue<ushort>(info.Bot.QueryConnection.GetClientByName(username).UnwrapThrow().ClientId);
		[Command("getuser all byname", "Gets all information of a user, searching with his name.")]
		public static JsonObject CommandGetUserByName(ExecutionInformation info, string username)
		{
			var client = info.Bot.QueryConnection.GetClientByName(username).UnwrapThrow();
			return new JsonSingleObject<ClientData>($"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}", client);
		}
		[Command("getuser name bydbid", "Gets the user name by dbid, searching with his database id.")]
		public static JsonObject CommandGetNameByDbId(ExecutionInformation info, ulong dbId) => new JsonSingleValue<string>(info.Bot.QueryConnection.GetDbClientByDbId(dbId).UnwrapThrow().NickName ?? string.Empty);
		[Command("getuser uid bydbid", "Gets the unique id of a user, searching with his database id.")]
		public static JsonObject CommandGetUidByDbId(ExecutionInformation info, ulong dbId) => new JsonSingleValue<string>(info.Bot.QueryConnection.GetDbClientByDbId(dbId).UnwrapThrow().Uid);

		[Command("help", "Shows all commands or detailed help about a specific command.")]
		[Usage("[<command>]", "Any currently accepted command")]
		[RequiredParameters(0)]
		public static JsonObject CommandHelp(ExecutionInformation info, params string[] parameter)
		{
			if (parameter.Length == 0)
			{
				var strb = new StringBuilder();
				strb.Append("\n========= Welcome to the TS3AudioBot ========="
					+ "\nIf you need any help with a special command use !help <commandName>."
					+ "\nHere are all possible commands:\n");
				var botComList = info.Core.CommandManager.AllCommands.Select(c => c.InvokeName).GroupBy(n => n.Split(' ')[0]).Select(x => x.Key).ToArray();
				foreach (var botCom in botComList)
					strb.Append(botCom).Append(", ");
				strb.Length -= 2;
				return new JsonArray<string>(strb.ToString(), botComList);
			}

			CommandGroup group = info.Core.CommandManager.CommandSystem.RootCommand;
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
				return new JsonSingleValue<string>(targetB.GetHelp());
			case CommandGroup targetCg:
				var subList = targetCg.Commands.Select(g => g.Key).ToArray();
				return new JsonArray<string>("The command contains the following subfunctions: " + string.Join(", ", subList), subList);
			case OverloadedFunctionCommand targetOfc:
				var strb = new StringBuilder();
				foreach (var botCom in targetOfc.Functions.OfType<BotCommand>())
					strb.Append(botCom.GetHelp());
				return new JsonSingleValue<string>(strb.ToString());
			}

			throw new CommandException("Seems like something went wrong. No help can be shown for this command path.", CommandExceptionReason.CommandError);
		}

		[Command("history add", "<id> Adds the song with <id> to the queue")]
		public static void CommandHistoryQueue(ExecutionInformation info, uint id)
			=> info.Bot.PlayManager.Enqueue(info.InvokerData, id).UnwrapThrow();

		[Command("history clean", "Cleans up the history file for better startup performance.")]
		public static string CommandHistoryClean(ExecutionInformation info)
		{
			if (info.ApiCall)
			{
				info.Core.Database.CleanFile();
				return null;
			}
			info.Session.SetResponse(ResponseHistoryClean, null);
			return "Do want to clean the history file now? " +
					"This might take a while and make the bot unresponsive in meanwhile. !(yes|no)";
		}

		[Command("history clean removedefective", "Cleans up the history file for better startup performance. " +
			"Also checks for all links in the history which cannot be opened anymore")]
		public static string CommandHistoryCleanRemove(ExecutionInformation info)
		{
			if (info.ApiCall)
			{
				info.Bot.HistoryManager.RemoveBrokenLinks(info);
				return null;
			}
			info.Session.SetResponse(ResponseHistoryClean, "removedefective");
			return "Do want to remove all defective links file now? " +
					"This might(will!) take a while and make the bot unresponsive in meanwhile. !(yes|no)";
		}

		[Command("history delete", "<id> Removes the entry with <id> from the history")]
		public static string CommandHistoryDelete(ExecutionInformation info, uint id)
		{
			var ale = info.Bot.HistoryManager.GetEntryById(id).UnwrapThrow();

			if (info.ApiCall)
			{
				info.Bot.HistoryManager.RemoveEntry(ale);
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
		public static JsonObject CommandHistoryFrom(ExecutionInformation info, uint userDbId, int? amount)
		{
			var query = new SeachQuery { UserId = userDbId };
			if (amount.HasValue)
				query.MaxResults = amount.Value;

			var results = info.Bot.HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(info.Bot.HistoryManager.Format(results), results);
		}

		[Command("history id", "<id> Displays all saved informations about the song with <id>")]
		public static JsonObject CommandHistoryId(ExecutionInformation info, uint id)
		{
			var result = info.Bot.HistoryManager.GetEntryById(id);
			if (!result)
				return new JsonEmpty("No entry found...");
			return new JsonSingleObject<AudioLogEntry>(info.Bot.HistoryManager.Format(result.Value), result.Value);
		}

		[Command("history id", "(last|next) Gets the highest|next song id")]
		public static JsonObject CommandHistoryId(ExecutionInformation info, string special)
		{
			if (special == "last")
				return new JsonSingleValue<uint>($"{info.Bot.HistoryManager.HighestId} is the currently highest song id.", info.Bot.HistoryManager.HighestId);
			else if (special == "next")
				return new JsonSingleValue<uint>($"{info.Bot.HistoryManager.HighestId + 1} will be the next song id.", info.Bot.HistoryManager.HighestId + 1);
			else
				throw new CommandException("Unrecognized name descriptor", CommandExceptionReason.CommandError);
		}

		[Command("history last", "Plays the last song again")]
		[Usage("<count>", "Gets the last <count> played songs.")]
		[RequiredParameters(0)]
		public static JsonObject CommandHistoryLast(ExecutionInformation info, int? amount)
		{
			if (amount.HasValue)
			{
				var query = new SeachQuery { MaxResults = amount.Value };
				var results = info.Bot.HistoryManager.Search(query).ToArray();
				return new JsonArray<AudioLogEntry>(info.Bot.HistoryManager.Format(results), results);
			}
			else
			{
				var ale = info.Bot.HistoryManager.Search(new SeachQuery { MaxResults = 1 }).FirstOrDefault();
				if (ale != null)
				{
					info.Bot.PlayManager.Play(info.InvokerData, ale.AudioResource).UnwrapThrow();
					return null;
				}
				else return new JsonEmpty("There is no song in the history");
			}
		}

		[Command("history play", "<id> Playes the song with <id>")]
		public static void CommandHistoryPlay(ExecutionInformation info, uint id)
			=> info.Bot.PlayManager.Play(info.InvokerData, id).UnwrapThrow();

		[Command("history rename", "<id> <name> Sets the name of the song with <id> to <name>")]
		public static void CommandHistoryRename(ExecutionInformation info, uint id, string newName)
		{
			var ale = info.Bot.HistoryManager.GetEntryById(id).UnwrapThrow();

			if (string.IsNullOrWhiteSpace(newName))
				throw new CommandException("The new name must not be empty or only whitespaces", CommandExceptionReason.CommandError);

			info.Bot.HistoryManager.RenameEntry(ale, newName);
		}

		[Command("history till", "<date> Gets all songs played until <date>.")]
		public static JsonObject CommandHistoryTill(ExecutionInformation info, DateTime time)
		{
			var query = new SeachQuery { LastInvokedAfter = time };
			var results = info.Bot.HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(info.Bot.HistoryManager.Format(results), results);
		}

		[Command("history till", "<name> Any of those desciptors: (hour|today|yesterday|week)")]
		public static JsonObject CommandHistoryTill(ExecutionInformation info, string time)
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
			var results = info.Bot.HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(info.Bot.HistoryManager.Format(results), results);
		}

		[Command("history title", "Gets all songs which title contains <string>")]
		public static JsonObject CommandHistoryTitle(ExecutionInformation info, string part)
		{
			var query = new SeachQuery { TitlePart = part };
			var results = info.Bot.HistoryManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(info.Bot.HistoryManager.Format(results), results);
		}

		[Command("if")]
		[Usage("<argument0> <comparator> <argument1> <then>", "Compares the two arguments and returns or executes the then-argument")]
		[Usage("<argument0> <comparator> <argument1> <then> <else>", "Same as before and return the else-arguments if the condition is false")]
		public static ICommandResult CommandIf(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			if (arguments.Count < 4)
				throw new CommandException("Expected at least 4 arguments", CommandExceptionReason.MissingParameter);
			var arg0 = ((StringCommandResult)arguments[0].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
			var cmp = ((StringCommandResult)arguments[1].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
			var arg1 = ((StringCommandResult)arguments[2].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;

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
		public static JsonObject CommandJsonMerge(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			if (arguments.Count == 0)
				return new JsonEmpty(string.Empty);

			var jsonArr = arguments
				.Select(arg => arg.Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.Json }))
				.Where(arg => arg.ResultType == CommandResultType.Json)
				.OfType<JsonCommandResult>()
				.Select(arg => arg.JsonObject.GetSerializeObject())
				.ToArray();

			return new JsonArray<object>(string.Empty, jsonArr);
		}

		[Command("kickme", "Guess what?")]
		[Usage("[far]", "Optional attribute for the extra punch strength")]
		[RequiredParameters(0)]
		public static void CommandKickme(ExecutionInformation info, string parameter)
		{
			if (info.ApiCall)
				throw new CommandException("This command is not available as API", CommandExceptionReason.NotSupported);

			try
			{
				if (info.InvokerData.ClientId.HasValue)
				{
					if (string.IsNullOrEmpty(parameter) || parameter == "near")
						info.Bot.QueryConnection.KickClientFromChannel(info.InvokerData.ClientId.Value);
					else if (parameter == "far")
						info.Bot.QueryConnection.KickClientFromServer(info.InvokerData.ClientId.Value);
				}
			}
			catch (Ts3CommandException ex)
			{
				Log.Write(Log.Level.Info, "Could not kick: {0}", ex);
				throw new CommandException("I'm not strong enough, master!", ex, CommandExceptionReason.CommandError);
			}
		}

		[Command("link", "Gets a link to the origin of the current song.")]
		public static JsonObject CommandLink(ExecutionInformation info)
		{
			if (info.Bot.PlayManager.CurrentPlayData == null)
				return new JsonEmpty("There is nothing on right now...");
			else if (info.Bot.QuizMode && info.Bot.PlayManager.CurrentPlayData.Invoker.ClientId != info.InvokerData.ClientId && !info.ApiCall)
				return new JsonEmpty("Sorry, you have to guess!");
			else
			{
				var link = info.Core.FactoryManager.RestoreLink(info.Bot.PlayManager.CurrentPlayData.ResourceData);
				return new JsonSingleValue<string>(link);
			}
		}

		[Command("list add", "Adds a link to your private playlist.")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public static void CommandListAdd(ExecutionInformation info, string link)
		{
			var plist = AutoGetPlaylist(info);
			var playResource = info.Core.FactoryManager.Load(link).UnwrapThrow();
			plist.AddItem(new PlaylistItem(playResource.BaseData, new MetaData() { ResourceOwnerDbId = info.InvokerData.DatabaseId }));
		}

		[Command("list add", "<id> Adds a link to your private playlist from the history by <id>.")]
		public static void CommandListAdd(ExecutionInformation info, uint hid)
		{
			var plist = AutoGetPlaylist(info);

			if (!info.Bot.HistoryManager.GetEntryById(hid))
				throw new CommandException("History entry not found", CommandExceptionReason.CommandError);

			plist.AddItem(new PlaylistItem(hid, new MetaData() { ResourceOwnerDbId = info.InvokerData.DatabaseId }));
		}

		[Command("list clear", "Clears your private playlist.")]
		public static void CommandListClear(ExecutionInformation info) => AutoGetPlaylist(info).Clear();

		[Command("list delete", "<name> Deletes the playlist with the name <name>. You can only delete playlists which you also have created. Admins can delete every playlist.")]
		public static JsonObject CommandListDelete(ExecutionInformation info, string name)
		{
			if (info.ApiCall)
				info.Bot.PlaylistManager.DeletePlaylist(name, info.InvokerData.DatabaseId ?? 0, info.HasRights(RightDeleteAllPlaylists)).UnwrapThrow();

			var hresult = info.Bot.PlaylistManager.LoadPlaylist(name, true);
			if (!hresult)
			{
				info.Session.SetResponse(ResponseListDelete, name);
				return new JsonEmpty($"Do you really want to delete the playlist \"{name}\" (error:{hresult.Message})");
			}
			else
			{
				if (hresult.Value.CreatorDbId != info.InvokerData.DatabaseId
					&& !info.HasRights(RightDeleteAllPlaylists))
					throw new CommandException("You are not allowed to delete others playlists", CommandExceptionReason.MissingRights);

				info.Session.SetResponse(ResponseListDelete, name);
				return new JsonEmpty($"Do you really want to delete the playlist \"{name}\"");
			}
		}

		[Command("list get", "<link> Imports a playlist form an other plattform like youtube etc.")]
		public static JsonObject CommandListGet(ExecutionInformation info, string link)
		{
			var playlist = info.Core.FactoryManager.LoadPlaylistFrom(link).UnwrapThrow();

			playlist.CreatorDbId = info.InvokerData.DatabaseId;
			info.Session.Set<PlaylistManager, Playlist>(playlist);
			return new JsonEmpty("Ok");
		}

		[Command("list item move", "<from> <to> Moves a item in a playlist <from> <to> position.")]
		public static void CommandListMove(ExecutionInformation info, int from, int to)
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
		public static string CommandListRemove(ExecutionInformation info, int index)
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
		public static JsonObject CommandListList(ExecutionInformation info, string pattern)
		{
			var files = info.Bot.PlaylistManager.GetAvailablePlaylists(pattern).ToArray();
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
		public static JsonObject CommandListLoad(ExecutionInformation info, string name)
		{
			Playlist loadList = AutoGetPlaylist(info);

			var playList = info.Bot.PlaylistManager.LoadPlaylist(name).UnwrapThrow();

			loadList.Clear();
			loadList.AddRange(playList.AsEnumerable());
			loadList.Name = playList.Name;
			return new JsonSingleObject<Playlist>($"Loaded: \"{name}\" with {loadList.Count} songs", loadList);
		}

		[Command("list merge", "Appends another playlist to yours.")]
		public static void CommandListMerge(ExecutionInformation info, string name)
		{
			var plist = AutoGetPlaylist(info);

			var lresult = info.Bot.PlaylistManager.LoadPlaylist(name);
			if (!lresult)
				throw new CommandException("The other playlist could not be found", CommandExceptionReason.CommandError);

			plist.AddRange(lresult.Value.AsEnumerable());
		}

		[Command("list name", "Displays the name of the playlist you are currently working on.")]
		[Usage("<name>", "Changes the playlist name to <name>.")]
		public static JsonObject CommandListName(ExecutionInformation info, string name)
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
		public static void CommandListPlay(ExecutionInformation info, int? index)
		{
			var plist = AutoGetPlaylist(info);

			if (!index.HasValue || (index.Value >= 0 && index.Value < plist.Count))
			{
				info.Bot.PlaylistManager.PlayFreelist(plist);
				info.Bot.PlaylistManager.Index = index ?? 0;
			}
			else
				throw new CommandException("Invalid starting index", CommandExceptionReason.CommandError);

			PlaylistItem item = info.Bot.PlaylistManager.Current();
			if (item != null)
				info.Bot.PlayManager.Play(info.InvokerData, item).UnwrapThrow();
			else
				throw new CommandException("Nothing to play...", CommandExceptionReason.CommandError);
		}

		[Command("list queue", "Appends your playlist to the freelist.")]
		public static void CommandListQueue(ExecutionInformation info)
		{
			var plist = AutoGetPlaylist(info);
			info.Bot.PlayManager.Enqueue(plist.AsEnumerable());
		}

		[Command("list save", "Stores your current workinglist to disk.")]
		[Usage("<name>", "Changes the playlist name to <name> before saving.")]
		[RequiredParameters(0)]
		public static JsonObject CommandListSave(ExecutionInformation info, string optNewName)
		{
			var plist = AutoGetPlaylist(info);
			if (!string.IsNullOrEmpty(optNewName))
			{
				PlaylistManager.IsNameValid(optNewName).UnwrapThrow();
				plist.Name = optNewName;
			}

			info.Bot.PlaylistManager.SavePlaylist(plist).UnwrapThrow();
			return new JsonEmpty("Ok");
		}

		[Command("list show", "Displays all songs currently in the playlists you are working on")]
		[Usage("<index>", "Lets you specify the staring index from which songs should be listed.")]
		[RequiredParameters(0)]
		public static JsonObject CommandListShow(ExecutionInformation info, int? offset) => CommandListShow(info, null, offset);

		[Command("list show", "<name> Displays all songs currently in the playlists with the name <name>")]
		[Usage("<name> <index>", "Lets you specify the starting index from which songs should be listed.")]
		[RequiredParameters(0)]
		public static JsonObject CommandListShow(ExecutionInformation info, string name, int? offset)
		{
			Playlist plist;
			if (!string.IsNullOrEmpty(name))
				plist = info.Bot.PlaylistManager.LoadPlaylist(name).UnwrapThrow();
			else
				plist = AutoGetPlaylist(info);

			var strb = new StringBuilder();
			strb.Append("Playlist: \"").Append(plist.Name).Append("\" with ").Append(plist.Count).AppendLine(" songs.");
			int from = Math.Max(offset ?? 0, 0);
			var items = plist.AsEnumerable().Skip(from).ToArray();
			foreach (var plitem in items.Take(10))
				strb.Append(from++).Append(": ").AppendLine(plitem.DisplayString);

			return new JsonArray<PlaylistItem>(strb.ToString(), items);
		}

		[Command("loop", "Gets whether or not to loop the entire playlist.")]
		public static JsonObject CommandLoop(ExecutionInformation info) => new JsonSingleValue<bool>("Loop is " + (info.Bot.PlaylistManager.Loop ? "on" : "off"), info.Bot.PlaylistManager.Loop);
		[Command("loop on", "Enables looping the entire playlist.")]
		public static void CommandLoopOn(ExecutionInformation info) => info.Bot.PlaylistManager.Loop = true;
		[Command("loop off", "Disables looping the entire playlist.")]
		public static void CommandLoopOff(ExecutionInformation info) => info.Bot.PlaylistManager.Loop = false;

		[Command("next", "Plays the next song in the playlist.")]
		public static void CommandNext(ExecutionInformation info)
		{
			info.Bot.PlayManager.Next(info.InvokerData).UnwrapThrow();
		}

		[Command("pm", "Requests a private session with the ServerBot so you can be intimate.")]
		public static string CommandPm(ExecutionInformation info)
		{
			if (info.ApiCall)
				throw new CommandException("This command is not available as API", CommandExceptionReason.NotSupported);
			info.InvokerData.Visibiliy = TextMessageTargetMode.Private;
			return "Hi " + (info.InvokerData.NickName ?? "Anonymous");
		}

		[Command("parse command", "Displays the AST of the requested command.")]
		[Usage("<command>", "The comand to be parsed")]
		public static JsonObject CommandParse(string parameter)
		{
			if (!parameter.TrimStart().StartsWith("!", StringComparison.Ordinal))
				throw new CommandException("This is not a command", CommandExceptionReason.CommandError);
			try
			{
				var node = CommandParser.ParseCommandRequest(parameter);
				var strb = new StringBuilder();
				strb.AppendLine();
				node.Write(strb, 0);
				return new JsonSingleObject<AstNode>(strb.ToString(), node);
			}
			catch (Exception ex)
			{
				throw new CommandException("GJ - You crashed it!!!", ex, CommandExceptionReason.CommandError);
			}
		}

		[Command("pause", "Well, pauses the song. Undo with !play.")]
		public static void CommandPause(ExecutionInformation info) => info.Bot.PlayerConnection.Paused = true;

		[Command("play", "Automatically tries to decide whether the link is a special resource (like youtube) or a direct resource (like ./hello.mp3) and starts it.")]
		[Usage("<link>", "Youtube, Soundcloud, local path or file link")]
		[RequiredParameters(0)]
		public static void CommandPlay(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				info.Bot.PlayerConnection.Paused = false;
			else
				info.Bot.PlayManager.Play(info.InvokerData, parameter).UnwrapThrow();
		}

		[Command("plugin list", "Lists all found plugins.")]
		public static JsonArray<PluginStatusInfo> CommandPluginList(ExecutionInformation info)
		{
			var overview = info.Core.PluginManager.GetPluginOverview();
			return new JsonArray<PluginStatusInfo>(PluginManager.FormatOverview(overview), overview);
		}

		[Command("plugin unload", "Unloads a plugin.")]
		public static void CommandPluginUnload(ExecutionInformation info, string identifier)
		{
			var result = info.Core.PluginManager.StopPlugin(identifier);
			if (result != PluginResponse.Ok)
				throw new CommandException("Plugin error: " + result, CommandExceptionReason.CommandError);
		}

		[Command("plugin load", "Unloads a plugin.")]
		public static void CommandPluginLoad(ExecutionInformation info, string identifier)
		{
			var result = info.Core.PluginManager.StartPlugin(identifier);
			if (result != PluginResponse.Ok)
				throw new CommandException("Plugin error: " + result, CommandExceptionReason.CommandError);
		}

		[Command("previous", "Plays the previous song in the playlist.")]
		public static void CommandPrevious(ExecutionInformation info)
			=> info.Bot.PlayManager.Previous(info.InvokerData).UnwrapThrow();

		[Command("print", "Lets you format multiple parameter to one.")]
		[RequiredParameters(0)]
		public static JsonObject CommandPrint(params string[] parameter)
		{
			// << Desing changes expected >>
			var strb = new StringBuilder();
			foreach (var param in parameter)
				strb.Append(param);
			return new JsonSingleValue<string>(strb.ToString());
		}

		[Command("quit", "Closes the TS3AudioBot application.")]
		[RequiredParameters(0)]
		public static string CommandQuit(ExecutionInformation info, string param)
		{
			if (info.ApiCall)
			{
				info.Bot.Dispose();
				return null;
			}

			if (param == "force")
			{
				// TODO necessary?: info.Bot.QueryConnection.OnMessageReceived -= TextCallback;
				info.Bot.Dispose();
				return null;
			}
			else
			{
				info.Session.SetResponse(ResponseQuit, null);
				return "Do you really want to quit? !(yes|no)";
			}
		}

		[Command("quiz", "Shows the quizmode status.")]
		public static JsonObject CommandQuiz(ExecutionInformation info) => new JsonSingleValue<bool>("Quizmode is " + (info.Bot.QuizMode ? "on" : "off"), info.Bot.QuizMode);
		[Command("quiz on", "Enable to hide the songnames and let your friends guess the title.")]
		public static void CommandQuizOn(ExecutionInformation info)
		{
			info.Bot.QuizMode = true;
			info.Bot.UpdateBotStatus().UnwrapThrow();
		}
		[Command("quiz off", "Disable to show the songnames again.")]
		public static void CommandQuizOff(ExecutionInformation info)
		{
			if (!info.ApiCall && info.InvokerData.Visibiliy.HasValue && info.InvokerData.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException("No cheatig! Everybody has to see it!", CommandExceptionReason.CommandError);
			info.Bot.QuizMode = false;
			info.Bot.UpdateBotStatus().UnwrapThrow();
		}

		[Command("random", "Gets whether or not to play playlists in random order.")]
		public static JsonObject CommandRandom(ExecutionInformation info) => new JsonSingleValue<bool>("Random is " + (info.Bot.PlaylistManager.Random ? "on" : "off"), info.Bot.PlaylistManager.Random);
		[Command("random on", "Enables random playlist playback")]
		public static void CommandRandomOn(ExecutionInformation info) => info.Bot.PlaylistManager.Random = true;
		[Command("random off", "Disables random playlist playback")]
		public static void CommandRandomOff(ExecutionInformation info) => info.Bot.PlaylistManager.Random = false;
		[Command("random seed", "Gets the unique seed for a certain playback order")]
		public static JsonObject CommandRandomSeed(ExecutionInformation info)
		{
			string seed = Util.FromSeed(info.Bot.PlaylistManager.Seed);
			string strseed = string.IsNullOrEmpty(seed) ? "<empty>" : seed;
			return new JsonSingleValue<string>(strseed);
		}
		[Command("random seed", "Sets the unique seed for a certain playback order")]
		public static void CommandRandomSeed(ExecutionInformation info, string newSeed)
		{
			if (newSeed.Any(c => !char.IsLetter(c)))
				throw new CommandException("Only letters allowed", CommandExceptionReason.CommandError);
			info.Bot.PlaylistManager.Seed = Util.ToSeed(newSeed.ToLowerInvariant());
		}
		[Command("random seed", "Sets the unique seed for a certain playback order")]
		public static void CommandRandomSeed(ExecutionInformation info, int newSeed) => info.Bot.PlaylistManager.Seed = newSeed;

		[Command("repeat", "Gets whether or not to loop a single song.")]
		public static JsonObject CommandRepeat(ExecutionInformation info) => new JsonSingleValue<bool>("Repeat is " + (info.Bot.PlayerConnection.Repeated ? "on" : "off"), info.Bot.PlayerConnection.Repeated);
		[Command("repeat on", "Enables single song repeat.")]
		public static void CommandRepeatOn(ExecutionInformation info) => info.Bot.PlayerConnection.Repeated = true;
		[Command("repeat off", "Disables single song repeat.")]
		public static void CommandRepeatOff(ExecutionInformation info) => info.Bot.PlayerConnection.Repeated = false;

		[Command("rights can", "Returns the subset of allowed commands the caller (you) can execute.")]
		public static JsonObject CommandRightsCan(ExecutionInformation info, params string[] rights)
		{
			var result = info.Core.RightsManager.GetRightsSubset(info.InvokerData, rights);
			if (result.Length > 0)
				return new JsonArray<string>(string.Join(", ", result), result);
			else
				return new JsonEmpty("No");
		}

		[Command("rights reload", "Reloads the rights configuration from file.")]
		public static JsonObject CommandRightsReload(ExecutionInformation info)
		{
			if (info.Core.RightsManager.ReadFile())
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
		public static JsonObject CommandRng(int? first, int? second)
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
		public static void CommandSeek(ExecutionInformation info, string parameter)
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
			else if (span < TimeSpan.Zero || span > info.Bot.PlayerConnection.Length)
				throw new CommandException("The point of time is not within the songlenth.", CommandExceptionReason.CommandError);
			else
				info.Bot.PlayerConnection.Position = span;
		}

		[Command("settings", "Changes values from the settigns. Not all changes can be applied immediately.")]
		[Usage("<key>", "Get the value of a setting")]
		[Usage("<key> <value>", "Set the value of a setting")]
		[RequiredParameters(0)]
		public static JsonObject CommandSettings(ExecutionInformation info, string key, string value)
		{
			var configMap = info.Core.ConfigManager.GetConfigMap();
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
					var result = info.Core.ConfigManager.SetSetting(filteredArr[0].Key, value);
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
		public static JsonObject CommandSong(ExecutionInformation info)
		{
			if (info.Bot.PlayManager.CurrentPlayData == null)
				return new JsonEmpty("There is nothing on right now...");
			else if (info.Bot.QuizMode && info.Bot.PlayManager.CurrentPlayData.Invoker.ClientId != info.InvokerData.ClientId && !info.ApiCall)
				return new JsonEmpty("Sorry, you have to guess!");
			else
				return new JsonSingleValue<string>(
					$"[url={info.Core.FactoryManager.RestoreLink(info.Bot.PlayManager.CurrentPlayData.ResourceData)}]{info.Bot.PlayManager.CurrentPlayData.ResourceData.ResourceTitle}[/url]",
					info.Bot.PlayManager.CurrentPlayData.ResourceData.ResourceTitle);
		}

		[Command("stop", "Stops the current song.")]
		public static void CommandStop(ExecutionInformation info)
		{
			info.Bot.PlayManager.Stop();
		}

		[Command("subscribe", "Lets you hear the music independent from the channel you are in.")]
		public static void CommandSubscribe(ExecutionInformation info)
		{
			if (info.InvokerData.ClientId.HasValue)
				info.Bot.TargetManager.WhisperClientSubscribe(info.InvokerData.ClientId.Value);
		}

		[Command("subscribe tempchannel", "Adds your current channel to the music playback.")]
		[RequiredParameters(0)]
		public static void CommandSubscribeTempChannel(ExecutionInformation info, ulong? channel)
		{
			var subChan = channel ?? info.InvokerData.ChannelId ?? 0;
			if (subChan != 0)
				info.Bot.TargetManager.WhisperChannelSubscribe(subChan, true);
		}

		[Command("subscribe channel", "Adds your current channel to the music playback.")]
		[RequiredParameters(0)]
		public static void CommandSubscribeChannel(ExecutionInformation info, ulong? channel)
		{
			var subChan = channel ?? info.InvokerData.ChannelId ?? 0;
			if (subChan != 0)
				info.Bot.TargetManager.WhisperChannelSubscribe(subChan, false);
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
			var res = ((StringCommandResult)arguments[0].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
			if (!int.TryParse(res, out int count) || count < 0)
				throw new CommandException("Count must be an integer >= 0", CommandExceptionReason.CommandError);

			if (arguments.Count > 2)
			{
				// Get start
				res = ((StringCommandResult)arguments[1].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
				if (!int.TryParse(res, out start) || start < 0)
					throw new CommandException("Start must be an integer >= 0", CommandExceptionReason.CommandError);
			}

			if (arguments.Count > 3)
				// Get delimiter
				delimiter = ((StringCommandResult)arguments[2].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;

			string text = ((StringCommandResult)arguments[Math.Min(arguments.Count - 1, 3)]
				.Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;

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
					return new JsonCommandResult(new JsonArray<string>(string.Join(delimiter ?? " ", splittedarr), splittedarr));
			}

			throw new CommandException("Can't find a fitting return type for take", CommandExceptionReason.NoReturnMatch);
		}

#if DEBUG
		[Command("test", "Only for debugging purposes.")]
		public static JsonObject CommandTest(ExecutionInformation info, string privet)
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
			public static int Num { get; set; } = 42;
			public static string Awesome { get; set; } = "Nehmen Sie AWESOME!!!";

			public JsonTest(string msgval) : base(msgval)
			{

			}
		}
#endif

		[Command("unsubscribe", "Only lets you hear the music in active channels again.")]
		public static void CommandUnsubscribe(ExecutionInformation info)
		{
			if (info.InvokerData.ClientId.HasValue)
				info.Bot.TargetManager.WhisperClientUnsubscribe(info.InvokerData.ClientId.Value);
		}

		[Command("unsubscribe channel", "Removes your current channel from the music playback.")]
		[RequiredParameters(0)]
		public static void CommandUnsubscribeChannel(ExecutionInformation info, ulong? channel)
		{
			var subChan = channel ?? info.InvokerData.ChannelId ?? 0;
			if (subChan != 0)
				info.Bot.TargetManager.WhisperChannelUnsubscribe(subChan, false);
		}

		[Command("unsubscribe temporary", "Clears all temporary targets.")]
		[RequiredParameters(0)]
		public static void CommandUnsubscribeTemporary(ExecutionInformation info)
		{
			info.Bot.TargetManager.ClearTemporary();
		}

		[Command("version", "Gets the current build version.")]
		public static JsonObject CommandVersion()
		{
			var data = Util.GetAssemblyData();
			return new JsonSingleValue<Util.BuildData>(data.ToLongString(), data);
		}

		[Command("volume", "Sets the volume level of the music.")]
		[Usage("<level>", "A new volume level between 0 and 100.")]
		[Usage("+/-<level>", "Adds or subtracts a value form the current volume.")]
		[RequiredParameters(0)]
		public static JsonObject CommandVolume(ExecutionInformation info, string parameter)
		{
			if (string.IsNullOrEmpty(parameter))
				return new JsonSingleValue<int>("Current volume: " + info.Bot.PlayerConnection.Volume, info.Bot.PlayerConnection.Volume);

			bool relPos = parameter.StartsWith("+", StringComparison.Ordinal);
			bool relNeg = parameter.StartsWith("-", StringComparison.Ordinal);
			string numberString = (relPos || relNeg) ? parameter.Remove(0, 1) : parameter;

			if (!int.TryParse(numberString, out int volume))
				throw new CommandException("The new volume could not be parsed", CommandExceptionReason.CommandError);

			int newVolume;
			if (relPos) newVolume = info.Bot.PlayerConnection.Volume + volume;
			else if (relNeg) newVolume = info.Bot.PlayerConnection.Volume - volume;
			else newVolume = volume;

			if (newVolume < 0 || newVolume > AudioValues.MaxVolume)
				throw new CommandException("The volume level must be between 0 and " + AudioValues.MaxVolume, CommandExceptionReason.CommandError);

			if (newVolume <= AudioValues.MaxUserVolume || newVolume < info.Bot.PlayerConnection.Volume || info.ApiCall)
				info.Bot.PlayerConnection.Volume = newVolume;
			else if (newVolume <= AudioValues.MaxVolume)
			{
				info.Session.SetResponse(ResponseVolume, newVolume);
				return new JsonEmpty("Careful you are requesting a very high volume! Do you want to apply this? !(yes|no)");
			}
			return null;
		}

		[Command("whisper off", "Enables normal voice mode.")]
		public static void CommandWhisperOff(ExecutionInformation info) => info.Bot.TargetManager.SendMode = TargetSendMode.Voice;

		[Command("whisper subscription", "Enables default whisper subsciption mode.")]
		public static void CommandWhisperSubsription(ExecutionInformation info) => info.Bot.TargetManager.SendMode = TargetSendMode.Whisper;

		[Command("whisper all", "Set how to send music.")]
		public static void CommandWhisperAll(ExecutionInformation info) => CommandWhisperGroup(info, GroupWhisperType.AllClients, GroupWhisperTarget.AllChannels);

		[Command("whisper group", "Set a specific teamspeak whisper group.")]
		[RequiredParameters(2)]
		public static void CommandWhisperGroup(ExecutionInformation info, GroupWhisperType type, GroupWhisperTarget target, ulong? targetId = null)
		{
			if (type == GroupWhisperType.ServerGroup || type == GroupWhisperType.ChannelGroup)
			{
				if (!targetId.HasValue)
					throw new CommandException("This type required an additional target", CommandExceptionReason.CommandError);
				info.Bot.TargetManager.SetGroupWhisper(type, target, targetId.Value);
				info.Bot.TargetManager.SendMode = TargetSendMode.WhisperGroup;
			}
			else
			{
				if (targetId.HasValue)
					throw new CommandException("This type does not take an additional target", CommandExceptionReason.CommandError);
				info.Bot.TargetManager.SetGroupWhisper(type, target, 0);
				info.Bot.TargetManager.SendMode = TargetSendMode.WhisperGroup;
			}
		}

		[Command("xecute", "Evaluates all parameter.")]
		public static void CommandXecute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			var retType = new[] { CommandResultType.Empty, CommandResultType.String, CommandResultType.Json };
			foreach (var arg in arguments)
				arg.Execute(info, StaticList.Empty<ICommand>(), retType);
		}
		// ReSharper enable UnusedMember.Global

		#endregion

		#region RESPONSES

		private static string ResponseVolume(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				if (info.HasRights(RightHighVolume))
				{
					if (info.Session.ResponseData is int respInt)
					{
						info.Bot.PlayerConnection.Volume = respInt;
					}
					else
					{
						Log.Write(Log.Level.Error, "responseData is not an int.");
						return "Internal error";
					}
				}
				else
				{
					return "Command can only be answered by an admin.";
				}
			}
			return null;
		}

		private static string ResponseQuit(ExecutionInformation info)
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

		private static string ResponseHistoryDelete(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				if (info.HasRights("cmd.history.delete"))
				{
					if (info.Session.ResponseData is AudioLogEntry ale)
					{
						info.Bot.HistoryManager.RemoveEntry(ale);
					}
					else
					{
						Log.Write(Log.Level.Error, "No entry provided.");
						return "Internal error";
					}
				}
				else
				{
					return "Command can only be answered by an admin.";
				}
			}
			return null;
		}

		private static string ResponseHistoryClean(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				if (info.HasRights("cmd.history.clean"))
				{
					string param = info.Session.ResponseData as string;
					if (string.IsNullOrEmpty(param))
					{
						info.Core.Database.CleanFile();
						return "Cleanup done!";
					}
					else if (param == "removedefective")
					{
						info.Bot.HistoryManager.RemoveBrokenLinks(info);
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

		private static string ResponseListDelete(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage);
			if (answer == Answer.Yes)
			{
				var name = info.Session.ResponseData as string;
				var result = info.Bot.PlaylistManager.DeletePlaylist(name, info.InvokerData.DatabaseId ?? 0, info.HasRights(RightDeleteAllPlaylists));
				if (!result) return result.Message;
				else return "Ok";
			}
			return null;
		}

		#endregion

		private static Playlist AutoGetPlaylist(ExecutionInformation info)
		{
			var result = info.Session.Get<PlaylistManager, Playlist>();
			if (result)
				return result.Value;

			var newPlist = new Playlist(info.InvokerData.NickName, info.InvokerData.DatabaseId);
			info.Session.Set<PlaylistManager, Playlist>(newPlist);
			return newPlist;
		}
	}
}
