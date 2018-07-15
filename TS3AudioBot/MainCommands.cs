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
	using Config;
	using Dependency;
	using Helper;
	using Helper.Environment;
	using History;
	using Localization;
	using Playlists;
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

		private const string YesNoOption = " !(yes|no)";

		// [...] = Optional
		// <name> = Placeholder for a text
		// [text] = Option for fixed text
		// (a|b) = either or switch

		// ReSharper disable UnusedMember.Global
		[Command("add")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public static void CommandAdd(PlayManager playManager, InvokerData invoker, string parameter)
			=> playManager.Enqueue(invoker, parameter).UnwrapThrow();

		[Command("api token")]
		[Usage("[<duration>]", "Optionally specifies a duration this key is valid in hours.")]
		public static string CommandApiToken(TokenManager tokenManager, InvokerData invoker, double? validHours = null)
		{
			if (invoker.Visibiliy.HasValue && invoker.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException(strings.error_use_private, CommandExceptionReason.CommandError);
			if (invoker.ClientUid == null)
				throw new CommandException(strings.error_no_uid_found, CommandExceptionReason.CommandError);

			TimeSpan? validSpan = null;
			try
			{
				if (validHours.HasValue)
					validSpan = TimeSpan.FromHours(validHours.Value);
			}
			catch (OverflowException oex)
			{
				throw new CommandException(strings.error_invalid_token_duration, oex, CommandExceptionReason.CommandError);
			}
			return tokenManager.GenerateToken(invoker.ClientUid, validSpan);
		}

		[Command("api nonce")]
		public static string CommandApiNonce(TokenManager tokenManager, InvokerData invoker)
		{
			if (invoker.Visibiliy.HasValue && invoker.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException(strings.error_use_private, CommandExceptionReason.CommandError);
			if (invoker.ClientUid == null)
				throw new CommandException(strings.error_no_uid_found, CommandExceptionReason.CommandError);
			var result = tokenManager.GetToken(invoker.ClientUid).UnwrapThrow();

			var nonce = result.CreateNonce();
			return nonce.Value;
		}

		[Command("bot avatar")]
		public static void CommandBotAvatar(Ts3Client ts3Client, string url)
		{
			url = TextUtil.ExtractUrlFromBb(url);
			Uri uri;
			try { uri = new Uri(url); }
			catch (Exception ex) { throw new CommandException(strings.error_media_invalid_uri, ex, CommandExceptionReason.CommandError); }

			WebWrapper.GetResponse(uri, x =>
			{
				var stream = x.GetResponseStream();
				if (stream == null)
					throw new CommandException(strings.error_net_empty_response, CommandExceptionReason.CommandError);
				using (var image = ImageUtil.ResizeImage(stream))
				{
					if (image == null)
						throw new CommandException(strings.error_media_internal_invalid, CommandExceptionReason.CommandError);
					ts3Client.UploadAvatar(image).UnwrapThrow();
				}
			});
		}

		[Command("bot commander")]
		public static JsonValue<bool> CommandBotCommander(Ts3Client ts3Client)
		{
			var value = ts3Client.IsChannelCommander().UnwrapThrow();
			return new JsonValue<bool>(value, string.Format(strings.info_status_channelcommander, value ? strings.info_on : strings.info_off));
		}
		[Command("bot commander on")]
		public static void CommandBotCommanderOn(Ts3Client ts3Client) => ts3Client.SetChannelCommander(true).UnwrapThrow();
		[Command("bot commander off")]
		public static void CommandBotCommanderOff(Ts3Client ts3Client) => ts3Client.SetChannelCommander(false).UnwrapThrow();

		[Command("bot come")]
		public static void CommandBotCome(Ts3Client ts3Client, InvokerData invoker, string password = null)
		{
			var channel = invoker?.ChannelId;
			if (!channel.HasValue)
				throw new CommandException(strings.error_no_target_channel, CommandExceptionReason.CommandError);
			CommandBotMove(ts3Client, channel.Value, password);
		}

		[Command("bot connect to")]
		public static BotInfo CommandBotConnectTo(BotManager bots, string name)
		{
			var botInfo = bots.RunBotTemplate(name);
			if (!botInfo.Ok)
				throw new CommandException(strings.error_could_not_create_bot + $" ({botInfo.Error})", CommandExceptionReason.CommandError);
			return botInfo.Value; // TODO check value/object
		}

		[Command("bot connect new")]
		public static BotInfo CommandBotConnectNew(BotManager bots, string address, string password = null)
		{
			var botConf = bots.CreateNewBot();
			botConf.Connect.Address.Value = address;
			if (!string.IsNullOrEmpty(password))
				botConf.Connect.ServerPassword.Password.Value = password;
			var botInfo = bots.RunBot(botConf);
			if (!botInfo.Ok)
				throw new CommandException(strings.error_could_not_create_bot + $" ({botInfo.Error})", CommandExceptionReason.CommandError);
			return botInfo.Value; // TODO check value/object
		}

		[Command("bot info")]
		public static BotInfo CommandBotInfo(Bot bot) => bot.GetInfo();

		[Command("bot info client", "_undocumented")]
		public static JsonValue<ClientInfo> CommandBotInfoClient(Ts3Client ts3Client)
			=> new JsonValue<ClientInfo>(ts3Client.GetSelf().UnwrapThrow(), string.Empty);

		[Command("bot list")]
		public static JsonArray<BotInfo> CommandBotId(BotManager bots)
		{
			var botlist = bots.GetBotInfolist();
			return new JsonArray<BotInfo>(botlist, bl => string.Join("\n", bl.Select(x => x.ToString())));
		}

		[Command("bot move")]
		public static void CommandBotMove(Ts3Client ts3Client, ulong channel, string password = null) => ts3Client.MoveTo(channel, password).UnwrapThrow();

		[Command("bot name")]
		public static void CommandBotName(Ts3Client ts3Client, string name) => ts3Client.ChangeName(name).UnwrapThrow();

		[Command("bot badges")]
		public static void CommandBotBadges(Ts3Client ts3Client, string badgesString) => ts3Client.ChangeBadges(badgesString).UnwrapThrow();

		[Command("bot save")]
		public static void CommandBotSetup(Bot bot, ConfBot botConfig, string name)
		{
			botConfig.SaveNew(name).UnwrapThrow();
			bot.Name = name;
		}

		[Command("bot setup")]
		public static void CommandBotSetup(Ts3Client ts3Client, string adminToken = null)
		{
			if (!ts3Client.SetupRights(adminToken))
				throw new CommandException(strings.cmd_bot_setup_error, CommandExceptionReason.CommandError);
		}

		[Command("bot use")]
		public static ICommandResult CommandBotUse(ExecutionInformation info, IReadOnlyList<CommandResultType> returnTypes, BotManager bots, int botId, ICommand cmd)
		{
			using (var botLock = bots.GetBotLock(botId))
			{
				if (botLock == null)
					throw new CommandException(strings.error_bot_does_not_exist, CommandExceptionReason.CommandError);

				var childInfo = new ExecutionInformation(botLock.Bot.Injector.CloneRealm<BotInjector>());
				if (info.TryGet<CallerInfo>(out var caller))
					childInfo.AddDynamicObject(caller);
				if (info.TryGet<InvokerData>(out var invoker))
					childInfo.AddDynamicObject(invoker);
				if (info.TryGet<UserSession>(out var session))
					childInfo.AddDynamicObject(session);

				return cmd.Execute(childInfo, Array.Empty<ICommand>(), returnTypes);
			}
		}

		[Command("clear")]
		public static void CommandClear(PlaylistManager playlistManager) => playlistManager.ClearFreelist();

		[Command("disconnect")]
		public static void CommandDisconnect(BotManager bots, Bot bot) => bots.StopBot(bot);

		[Command("eval")]
		[Usage("<command> <arguments...>", "Executes the given command on arguments")]
		[Usage("<strings...>", "Concat the strings and execute them with the command system")]
		public static ICommandResult CommandEval(ExecutionInformation info, CommandManager commandManager, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			// Evaluate the first argument on the rest of the arguments
			if (arguments.Count == 0)
				throw new CommandException(strings.error_cmd_at_least_one_argument, CommandExceptionReason.MissingParameter);
			var leftArguments = arguments.TrySegment(1);
			var arg0 = arguments[0].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnCommandOrString);
			if (arg0.ResultType == CommandResultType.Command)
				return ((CommandCommandResult)arg0).Command.Execute(info, leftArguments, returnTypes);

			// We got a string back so parse and evaluate it
			var args = ((StringCommandResult)arg0).Content;

			// Add the rest of the arguments
			args += string.Join(" ", arguments.Select(a =>
				((StringCommandResult)a.Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content));

			var cmd = commandManager.CommandSystem.AstToCommandResult(CommandParser.ParseCommandRequest(args));
			return cmd.Execute(info, leftArguments, returnTypes);
		}

		[Command("getmy id")]
		public static ushort CommandGetId(InvokerData invoker)
			=> invoker.ClientId ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy uid")]
		public static string CommandGetUid(InvokerData invoker)
			=> invoker.ClientUid ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy name")]
		public static string CommandGetName(InvokerData invoker)
			=> invoker.NickName ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy dbid")]
		public static ulong CommandGetDbId(InvokerData invoker)
			=> invoker.DatabaseId ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy channel")]
		public static ulong CommandGetChannel(InvokerData invoker)
			=> invoker.ChannelId ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy all")]
		public static JsonValue<InvokerData> CommandGetUser(InvokerData invoker)
			=> new JsonValue<InvokerData>(invoker, $"Client: Id:{invoker.ClientId} DbId:{invoker.DatabaseId} ChanId:{invoker.ChannelId} Uid:{invoker.ClientUid}"); // LOC: TODO

		[Command("getuser uid byid")]
		public static string CommandGetUidById(Ts3Client ts3Client, ushort id) => ts3Client.GetFallbackedClientById(id).UnwrapThrow().Uid;
		[Command("getuser name byid")]
		public static string CommandGetNameById(Ts3Client ts3Client, ushort id) => ts3Client.GetFallbackedClientById(id).UnwrapThrow().Name;
		[Command("getuser dbid byid")]
		public static ulong CommandGetDbIdById(Ts3Client ts3Client, ushort id) => ts3Client.GetFallbackedClientById(id).UnwrapThrow().DatabaseId;
		[Command("getuser channel byid")]
		public static ulong CommandGetChannelById(Ts3Client ts3Client, ushort id) => ts3Client.GetFallbackedClientById(id).UnwrapThrow().ChannelId;
		[Command("getuser all byid")]
		public static JsonValue<ClientData> CommandGetUserById(Ts3Client ts3Client, ushort id)
		{
			var client = ts3Client.GetFallbackedClientById(id).UnwrapThrow();
			return new JsonValue<ClientData>(client, $"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}");
		}
		[Command("getuser id byname")]
		public static ushort CommandGetIdByName(Ts3Client ts3Client, string username) => ts3Client.GetClientByName(username).UnwrapThrow().ClientId;
		[Command("getuser all byname")]
		public static JsonValue<ClientData> CommandGetUserByName(Ts3Client ts3Client, string username)
		{
			var client = ts3Client.GetClientByName(username).UnwrapThrow();
			return new JsonValue<ClientData>(client, $"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}");
		}
		[Command("getuser name bydbid")]
		public static string CommandGetNameByDbId(Ts3Client ts3Client, ulong dbId) => ts3Client.GetDbClientByDbId(dbId).UnwrapThrow().Name;
		[Command("getuser uid bydbid")]
		public static string CommandGetUidByDbId(Ts3Client ts3Client, ulong dbId) => ts3Client.GetDbClientByDbId(dbId).UnwrapThrow().Uid;

		[Command("help")]
		[Usage("[<command>]", "Any currently accepted command")]
		public static JsonObject CommandHelp(CommandManager commandManager, CallerInfo caller, Algorithm.Filter filter = null, params string[] parameter)
		{
			if (parameter.Length == 0 && !caller.ApiCall)
			{
				var botComList = commandManager.AllCommands.Select(c => c.InvokeName).OrderBy(x => x).GroupBy(n => n.Split(' ')[0]).Select(x => x.Key).ToArray();
				return new JsonArray<string>(botComList, bcl =>
				{
					var strb = new StringBuilder();
					strb.AppendLine();
					strb.AppendLine(strings.cmd_help_header);
					foreach (var botCom in bcl)
						strb.Append(botCom).Append(", ");
					strb.Length -= 2;
					return strb.ToString();
				});
			}

			CommandGroup group = commandManager.CommandSystem.RootCommand;
			ICommand target = group;
			for (int i = 0; i < parameter.Length; i++)
			{
				filter = filter ?? Algorithm.Filter.DefaultFilter;
				var possibilities = filter.Current.Filter(group.Commands, parameter[i]).ToList();
				if (possibilities.Count <= 0)
					throw new CommandException(strings.cmd_help_error_no_matching_command, CommandExceptionReason.CommandError);
				if (possibilities.Count > 1)
					throw new CommandException(string.Format(strings.cmd_help_error_ambiguous_command, string.Join(", ", possibilities.Select(kvp => kvp.Key))), CommandExceptionReason.CommandError);

				target = possibilities[0].Value;
				if (i < parameter.Length - 1)
				{
					group = target as CommandGroup;
					if (group == null)
						throw new CommandException(string.Format(strings.cmd_help_error_no_further_subfunctions, string.Join(" ", parameter, 0, i)), CommandExceptionReason.CommandError);
				}
			}

			switch (target)
			{
			case BotCommand targetB:
				return new JsonValue<BotCommand>(targetB, cmd => cmd?.GetHelp(), cmd => cmd?.AsJsonObj); // check if '?' is necessary
			case CommandGroup targetCg:
				var subList = targetCg.Commands.Select(g => g.Key).ToArray();
				return new JsonArray<string>(subList, string.Format(strings.cmd_help_info_contains_subfunctions, string.Join(", ", subList)));
			case OverloadedFunctionCommand targetOfc:
				var strb = new StringBuilder();
				foreach (var botCom in targetOfc.Functions.OfType<BotCommand>())
					strb.Append(botCom.GetHelp());
				return new JsonValue<string>(strb.ToString());
			default:
				throw new CommandException(strings.cmd_help_error_unknown_error, CommandExceptionReason.CommandError);
			}
		}

		[Command("history add")]
		public static void CommandHistoryQueue(HistoryManager historyManager, PlayManager playManager, InvokerData invoker, uint hid)
		{
			var ale = historyManager.GetEntryById(hid).UnwrapThrow();
			playManager.Enqueue(invoker, ale.AudioResource).UnwrapThrow();
		}

		[Command("history clean")]
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
					return strings.info_cleanup_done;
				}
				return null;
			}
			session.SetResponse(ResponseHistoryClean);
			return new JsonEmpty($"{strings.cmd_history_clean_confirm_clean} {strings.info_bot_might_be_unresponsive} {YesNoOption}");
		}

		[Command("history clean removedefective")]
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
					return strings.info_cleanup_done;
				}
				return null;
			}
			session.SetResponse(ResponseHistoryCleanRemove);
			return new JsonEmpty($"{strings.cmd_history_clean_removedefective_confirm_clean} {strings.info_bot_might_be_unresponsive} {YesNoOption}");
		}

		[Command("history clean upgrade", "_undocumented")]
		public static void CommandHistoryCleanUpgrade(HistoryManager historyManager, Ts3Client ts3Client)
		{
			historyManager.UpdadeDbIdToUid(ts3Client);
		}

		[Command("history delete")]
		public static JsonEmpty CommandHistoryDelete(HistoryManager historyManager, CallerInfo caller, uint id, UserSession session = null)
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
			return new JsonEmpty(string.Format(strings.cmd_history_delete_confirm + YesNoOption, name, id));
		}

		[Command("history from")]
		public static JsonArray<AudioLogEntry> CommandHistoryFrom(HistoryManager historyManager, string userUid, int? amount = null)
		{
			var query = new SeachQuery { UserUid = userUid };
			if (amount.HasValue)
				query.MaxResults = amount.Value;

			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format);
		}

		[Command("history id", "cmd_history_id_uint_help")]
		public static JsonValue<AudioLogEntry> CommandHistoryId(HistoryManager historyManager, uint id)
		{
			var result = historyManager.GetEntryById(id).UnwrapThrow();
			return new JsonValue<AudioLogEntry>(result, historyManager.Format(result));
		}

		[Command("history id", "cmd_history_id_string_help")]
		public static JsonValue<uint> CommandHistoryId(HistoryManager historyManager, string special)
		{
			if (special == "last")
				return new JsonValue<uint>(historyManager.HighestId, string.Format(strings.cmd_history_id_last, historyManager.HighestId));
			else if (special == "next")
				return new JsonValue<uint>(historyManager.HighestId + 1, string.Format(strings.cmd_history_id_next, historyManager.HighestId + 1));
			else
				throw new CommandException("Unrecognized name descriptor", CommandExceptionReason.CommandError);
		}

		[Command("history last", "cmd_history_last_int_help")]
		public static JsonArray<AudioLogEntry> CommandHistoryLast(HistoryManager historyManager, int amount)
		{
			var query = new SeachQuery { MaxResults = amount };
			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format);
		}

		[Command("history last", "cmd_history_last_help")]
		public static void CommandHistoryLast(HistoryManager historyManager, PlayManager playManager, InvokerData invoker)
		{
			var ale = historyManager.Search(new SeachQuery { MaxResults = 1 }).FirstOrDefault();
			if (ale == null)
				throw new CommandException(strings.cmd_history_last_is_empty, CommandExceptionReason.CommandError);
			playManager.Play(invoker, ale.AudioResource).UnwrapThrow();
		}

		[Command("history play")]
		public static void CommandHistoryPlay(HistoryManager historyManager, PlayManager playManager, InvokerData invoker, uint hid)
		{
			var ale = historyManager.GetEntryById(hid).UnwrapThrow();
			playManager.Play(invoker, ale.AudioResource).UnwrapThrow();
		}

		[Command("history rename")]
		public static void CommandHistoryRename(HistoryManager historyManager, uint id, string newName)
		{
			var ale = historyManager.GetEntryById(id).UnwrapThrow();

			if (string.IsNullOrWhiteSpace(newName))
				throw new CommandException(strings.cmd_history_rename_invalid_name, CommandExceptionReason.CommandError);

			historyManager.RenameEntry(ale, newName);
		}

		[Command("history till", "cmd_history_till_DateTime_help")]
		public static JsonArray<AudioLogEntry> CommandHistoryTill(HistoryManager historyManager, DateTime time)
		{
			var query = new SeachQuery { LastInvokedAfter = time };
			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format);
		}

		[Command("history till", "cmd_history_till_string_help")]
		public static JsonArray<AudioLogEntry> CommandHistoryTill(HistoryManager historyManager, string time)
		{
			DateTime tillTime;
			switch (time.ToLower(CultureInfo.InvariantCulture))
			{
			case "hour": tillTime = DateTime.Now.AddHours(-1); break;
			case "today": tillTime = DateTime.Today; break;
			case "yesterday": tillTime = DateTime.Today.AddDays(-1); break;
			case "week": tillTime = DateTime.Today.AddDays(-7); break;
			default: throw new CommandException(strings.error_unrecognized_descriptor, CommandExceptionReason.CommandError);
			}
			var query = new SeachQuery { LastInvokedAfter = tillTime };
			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format);
		}

		[Command("history title")]
		public static JsonArray<AudioLogEntry> CommandHistoryTitle(HistoryManager historyManager, string part)
		{
			var query = new SeachQuery { TitlePart = part };
			var results = historyManager.Search(query).ToArray();
			return new JsonArray<AudioLogEntry>(results, historyManager.Format);
		}

		[Command("if")]
		[Usage("<argument0> <comparator> <argument1> <then>", "Compares the two arguments and returns or executes the then-argument")]
		[Usage("<argument0> <comparator> <argument1> <then> <else>", "Same as before and return the else-arguments if the condition is false")]
		public static ICommandResult CommandIf(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			if (arguments.Count < 4)
				throw new CommandException(strings.error_cmd_at_least_four_argument, CommandExceptionReason.MissingParameter);
			var arg0 = ((StringCommandResult)arguments[0].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
			var cmp = ((StringCommandResult)arguments[1].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
			var arg1 = ((StringCommandResult)arguments[2].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;

			Func<double, double, bool> comparer;
			switch (cmp)
			{
			case "<": comparer = (a, b) => a < b; break;
			case ">": comparer = (a, b) => a > b; break;
			case "<=": comparer = (a, b) => a <= b; break;
			case ">=": comparer = (a, b) => a >= b; break;
			case "==": comparer = (a, b) => Math.Abs(a - b) < 1e-6; break;
			case "!=": comparer = (a, b) => Math.Abs(a - b) > 1e-6; break;
			default: throw new CommandException(strings.cmd_if_unknown_operator, CommandExceptionReason.CommandError);
			}

			bool cmpResult;
			// Try to parse arguments into doubles
			if (double.TryParse(arg0, NumberStyles.Number, CultureInfo.InvariantCulture, out var d0)
				&& double.TryParse(arg1, NumberStyles.Number, CultureInfo.InvariantCulture, out var d1))
			{
				cmpResult = comparer(d0, d1);
			}
			else
			{
				cmpResult = comparer(string.CompareOrdinal(arg0, arg1), 0);
			}

			// If branch
			if (cmpResult)
				return arguments[3].Execute(info, Array.Empty<ICommand>(), returnTypes);
			// Else branch
			if (arguments.Count > 4)
				return arguments[4].Execute(info, Array.Empty<ICommand>(), returnTypes);

			// Try to return nothing
			if (returnTypes.Contains(CommandResultType.Empty))
				return EmptyCommandResult.Instance;
			throw new CommandException(strings.error_nothing_to_return, CommandExceptionReason.NoReturnMatch);
		}

		[Command("json merge")]
		public static JsonArray<object> CommandJsonMerge(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			if (arguments.Count == 0)
				return new JsonArray<object>(Array.Empty<object>(), string.Empty);

			var jsonArr = arguments
				.Select(arg =>
				{
					ICommandResult res;
					try { res = arg.Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnJson); }
					catch (CommandException) { return null; }
					if (res.ResultType == CommandResultType.Json)
						return ((JsonCommandResult)res).JsonObject.GetSerializeObject();
					else
						throw new CommandException(strings.error_nothing_to_return, CommandExceptionReason.NoReturnMatch);
				})
				.ToArray();

			return new JsonArray<object>(jsonArr, string.Empty);
		}

		[Command("kickme")]
		[Usage("[far]", "Optional attribute for the extra punch strength")]
		public static void CommandKickme(Ts3Client ts3Client, InvokerData invoker, CallerInfo caller, string parameter = null)
		{
			if (caller.ApiCall)
				throw new CommandException(strings.error_not_available_from_api, CommandExceptionReason.NotSupported);

			if (invoker.ClientId.HasValue)
			{
				E<LocalStr> result = R.Ok;
				if (string.IsNullOrEmpty(parameter) || parameter == "near")
					result = ts3Client.KickClientFromChannel(invoker.ClientId.Value);
				else if (parameter == "far")
					result = ts3Client.KickClientFromServer(invoker.ClientId.Value);
				if (!result.Ok)
					throw new CommandException(strings.cmd_kickme_missing_permission, CommandExceptionReason.CommandError);
			}
		}

		[Command("link")]
		public static string CommandLink(ResourceFactoryManager factoryManager, PlayManager playManager, Bot bot, CallerInfo caller, InvokerData invoker = null)
		{
			if (playManager.CurrentPlayData == null)
				throw new CommandException(strings.info_currently_not_playing, CommandExceptionReason.CommandError);
			if (bot.QuizMode && !caller.ApiCall && (invoker == null || playManager.CurrentPlayData.Invoker.ClientId != invoker.ClientId))
				throw new CommandException(strings.info_quizmode_is_active, CommandExceptionReason.CommandError);

			return factoryManager.RestoreLink(playManager.CurrentPlayData.ResourceData);
		}

		[Command("list add")]
		[Usage("<link>", "Any link that is also recognized by !play")]
		public static void CommandListAdd(ResourceFactoryManager factoryManager, UserSession session, InvokerData invoker, string link)
		{
			var plist = AutoGetPlaylist(session, invoker);
			var playResource = factoryManager.Load(link).UnwrapThrow();
			plist.AddItem(new PlaylistItem(playResource.BaseData, new MetaData { ResourceOwnerUid = invoker.ClientUid }));
		}

		[Command("list add")]
		public static void CommandListAdd(HistoryManager historyManager, UserSession session, InvokerData invoker, uint hid)
		{
			var plist = AutoGetPlaylist(session, invoker);
			var ale = historyManager.GetEntryById(hid).UnwrapThrow();
			plist.AddItem(new PlaylistItem(ale.AudioResource, new MetaData { ResourceOwnerUid = invoker.ClientUid }));
		}

		[Command("list clear")]
		public static void CommandListClear(UserSession session, InvokerData invoker) => AutoGetPlaylist(session, invoker).Clear();

		[Command("list delete")]
		public static JsonEmpty CommandListDelete(ExecutionInformation info, PlaylistManager playlistManager, CallerInfo caller, InvokerData invoker, string name, UserSession session = null)
		{
			if (caller.ApiCall)
				playlistManager.DeletePlaylist(name, invoker.ClientUid, info.HasRights(RightDeleteAllPlaylists)).UnwrapThrow();

			bool? canDeleteAllPlaylists = null;

			string ResponseListDelete(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					playlistManager.DeletePlaylist(name, invoker.ClientUid, canDeleteAllPlaylists ?? info.HasRights(RightDeleteAllPlaylists)).UnwrapThrow();
				}
				return null;
			}

			var hresult = playlistManager.LoadPlaylist(name, true);
			if (!hresult)
			{
				session.SetResponse(ResponseListDelete);
				// TODO check if return == string => ask, return == empty => just delete
				return new JsonEmpty(string.Format(strings.cmd_list_delete_confirm, name));
			}
			else
			{
				canDeleteAllPlaylists = info.HasRights(RightDeleteAllPlaylists);
				if (hresult.Value.OwnerUid != invoker.ClientUid && !canDeleteAllPlaylists.Value)
					throw new CommandException(strings.cmd_list_delete_cannot_delete_others_playlist, CommandExceptionReason.MissingRights);

				session.SetResponse(ResponseListDelete);
				return new JsonEmpty(string.Format(strings.cmd_list_delete_confirm, name));
			}
		}

		[Command("list get")]
		public static JsonEmpty CommandListGet(ResourceFactoryManager factoryManager, UserSession session, InvokerData invoker, string link)
		{
			var playlist = factoryManager.LoadPlaylistFrom(link).UnwrapThrow();

			playlist.OwnerUid = invoker.ClientUid;
			session.Set<PlaylistManager, Playlist>(playlist);
			return new JsonEmpty(strings.info_ok);
		}

		[Command("list item move")]
		public static void CommandListMove(UserSession session, InvokerData invoker, int from, int to)
		{
			var plist = AutoGetPlaylist(session, invoker);

			if (from < 0 || from >= plist.Count
				|| to < 0 || to >= plist.Count)
			{
				throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);
			}

			if (from == to)
				return;

			var plitem = plist.GetResource(from);
			plist.RemoveItemAt(from);
			plist.InsertItem(plitem, Math.Min(to, plist.Count));
		}

		[Command("list item delete")]
		public static string CommandListRemove(UserSession session, InvokerData invoker, int index)
		{
			var plist = AutoGetPlaylist(session, invoker);

			if (index < 0 || index >= plist.Count)
				throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);

			var deletedItem = plist.GetResource(index);
			plist.RemoveItemAt(index);
			return string.Format(strings.info_removed, deletedItem.DisplayString);
		}

		// add list item rename

		[Command("list list")]
		[Usage("<pattern>", "Filters all lists cantaining the given pattern.")]
		public static JsonArray<string> CommandListList(PlaylistManager playlistManager, string pattern = null)
		{
			var files = playlistManager.GetAvailablePlaylists(pattern).ToArray();
			if (files.Length <= 0)
				return new JsonArray<string>(files, strings.error_playlist_not_found);

			return new JsonArray<string>(files, fi =>
			{
				var strb = new StringBuilder();
				int tokenLen = 0;
				foreach (var file in fi)
				{
					int newTokenLen = tokenLen + TS3Client.Commands.Ts3String.TokenLength(file) + 3;
					if (newTokenLen >= TS3Client.Commands.Ts3Const.MaxSizeTextMessage)
						break;

					strb.Append(file).Append(", ");
					tokenLen = newTokenLen;
				}

				if (strb.Length > 2)
					strb.Length -= 2;
				return strb.ToString();
			});
		}

		[Command("list load")]
		public static JsonValue<Playlist> CommandListLoad(PlaylistManager playlistManager, UserSession session, InvokerData invoker, string name)
		{
			var loadList = AutoGetPlaylist(session, invoker);

			var playList = playlistManager.LoadPlaylist(name).UnwrapThrow();

			loadList.Clear();
			loadList.AddRange(playList.AsEnumerable());
			loadList.Name = playList.Name;
			return new JsonValue<Playlist>(loadList, string.Format(strings.cmd_list_load_response, name, loadList.Count));
		}

		[Command("list merge")]
		public static void CommandListMerge(PlaylistManager playlistManager, UserSession session, InvokerData invoker, string name)
		{
			var plist = AutoGetPlaylist(session, invoker);

			var lresult = playlistManager.LoadPlaylist(name);
			if (!lresult)
				throw new CommandException(strings.error_playlist_not_found, CommandExceptionReason.CommandError);

			plist.AddRange(lresult.Value.AsEnumerable());
		}

		[Command("list name")]
		[Usage("<name>", "Changes the playlist name to <name>.")]
		public static string CommandListName(UserSession session, InvokerData invoker, string name)
		{
			var plist = AutoGetPlaylist(session, invoker);

			if (string.IsNullOrEmpty(name))
				return plist.Name;

			Util.IsSafeFileName(name).UnwrapThrow();

			plist.Name = name;
			return null;
		}

		[Command("list play")]
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
			{
				throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);
			}

			var item = playlistManager.Current();
			if (item != null)
				playManager.Play(invoker, item).UnwrapThrow();
			else
				throw new CommandException(strings.error_playlist_is_empty, CommandExceptionReason.CommandError);
		}

		[Command("list queue")]
		public static void CommandListQueue(PlayManager playManager, UserSession session, InvokerData invoker)
		{
			var plist = AutoGetPlaylist(session, invoker);
			playManager.Enqueue(plist.AsEnumerable()).UnwrapThrow();
		}

		[Command("list save")]
		[Usage("<name>", "Changes the playlist name to <name> before saving.")]
		public static void CommandListSave(PlaylistManager playlistManager, UserSession session, InvokerData invoker, string optNewName = null)
		{
			var plist = AutoGetPlaylist(session, invoker);
			if (!string.IsNullOrEmpty(optNewName))
			{
				Util.IsSafeFileName(optNewName).UnwrapThrow();
				plist.Name = optNewName;
			}

			playlistManager.SavePlaylist(plist).UnwrapThrow();
		}

		[Command("list show")]
		[Usage("<index>", "Lets you specify the staring index from which songs should be listed.")]
		public static JsonArray<PlaylistItem> CommandListShow(PlaylistManager playlistManager, UserSession session, InvokerData invoker, int? offset = null) => CommandListShow(playlistManager, session, invoker, null, offset);

		[Command("list show")]
		[Usage("<name> <index>", "Lets you specify the starting index from which songs should be listed.")]
		public static JsonArray<PlaylistItem> CommandListShow(PlaylistManager playlistManager, UserSession session, InvokerData invoker, string name = null, int? offset = null)
		{
			Playlist plist;
			if (!string.IsNullOrEmpty(name))
				plist = playlistManager.LoadPlaylist(name).UnwrapThrow();
			else
				plist = AutoGetPlaylist(session, invoker);

			int from = Math.Max(offset ?? 0, 0);
			var items = plist.AsEnumerable().Skip(from).ToArray();

			return new JsonArray<PlaylistItem>(items, it =>
			{
				var strb = new StringBuilder();
				strb.AppendFormat(strings.cmd_list_show_header, plist.Name, plist.Count).AppendLine();
				foreach (var plitem in it.Take(10))
					strb.Append(from++).Append(": ").AppendLine(plitem.DisplayString);
				return strb.ToString();
			});
		}

		[Command("loop")]
		public static JsonValue<bool> CommandLoop(PlaylistManager playlistManager) => new JsonValue<bool>(playlistManager.Loop, string.Format(strings.info_status_loop, playlistManager.Loop ? strings.info_on : strings.info_off));
		[Command("loop on")]
		public static void CommandLoopOn(PlaylistManager playlistManager) => playlistManager.Loop = true;
		[Command("loop off")]
		public static void CommandLoopOff(PlaylistManager playlistManager) => playlistManager.Loop = false;

		[Command("next")]
		public static void CommandNext(PlayManager playManager, InvokerData invoker)
		{
			playManager.Next(invoker).UnwrapThrow();
		}

		[Command("pm")]
		public static string CommandPm(CallerInfo caller, InvokerData invoker)
		{
			if (caller.ApiCall)
				throw new CommandException(strings.error_not_available_from_api, CommandExceptionReason.NotSupported);
			invoker.Visibiliy = TextMessageTargetMode.Private;
			return string.Format(strings.cmd_pm_hi, invoker.NickName ?? "Anonymous");
		}

		[Command("parse command")]
		public static JsonValue<AstNode> CommandParse(string parameter)
		{
			if (!parameter.TrimStart().StartsWith("!", StringComparison.Ordinal))
				throw new CommandException("This is not a command", CommandExceptionReason.CommandError);

			var node = CommandParser.ParseCommandRequest(parameter);
			var strb = new StringBuilder();
			strb.AppendLine();
			node.Write(strb, 0);
			return new JsonValue<AstNode>(node, strb.ToString());
		}

		[Command("pause")]
		public static void CommandPause(Bot bot) => bot.PlayerConnection.Paused = true;

		[Command("play")]
		[Usage("<link>", "Youtube, Soundcloud, local path or file link")]
		public static void CommandPlay(IPlayerConnection playerConnection, PlayManager playManager, InvokerData invoker, string parameter = null)
		{
			if (string.IsNullOrEmpty(parameter))
				playerConnection.Paused = false;
			else
				playManager.Play(invoker, parameter).UnwrapThrow();
		}

		[Command("plugin list")]
		public static JsonArray<PluginStatusInfo> CommandPluginList(PluginManager pluginManager, Bot bot = null)
		{
			var overview = pluginManager.GetPluginOverview(bot);
			return new JsonArray<PluginStatusInfo>(overview, PluginManager.FormatOverview);
		}

		[Command("plugin unload")]
		public static void CommandPluginUnload(PluginManager pluginManager, string identifier, Bot bot = null)
		{
			var result = pluginManager.StopPlugin(identifier, bot);
			if (result != PluginResponse.Ok)
				throw new CommandException(string.Format(strings.error_plugin_error, result /*TODO*/), CommandExceptionReason.CommandError);
		}

		[Command("plugin load")]
		public static void CommandPluginLoad(PluginManager pluginManager, string identifier, Bot bot = null)
		{
			var result = pluginManager.StartPlugin(identifier, bot);
			if (result != PluginResponse.Ok)
				throw new CommandException(string.Format(strings.error_plugin_error, result /*TODO*/), CommandExceptionReason.CommandError);
		}

		[Command("previous")]
		public static void CommandPrevious(PlayManager playManager, InvokerData invoker)
			=> playManager.Previous(invoker).UnwrapThrow();

		[Command("print")]
		public static string CommandPrint(params string[] parameter)
		{
			// XXX << Design changes expected >>
			var strb = new StringBuilder();
			foreach (var param in parameter)
				strb.Append(param);
			return strb.ToString();
		}

		[Command("quit")]
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
			return new JsonEmpty(strings.cmd_quit_confirm + YesNoOption);
		}

		[Command("quiz")]
		public static JsonValue<bool> CommandQuiz(Bot bot) => new JsonValue<bool>(bot.QuizMode, string.Format(strings.info_status_quizmode, bot.QuizMode ? strings.info_on : strings.info_off));
		[Command("quiz on")]
		public static void CommandQuizOn(Bot bot)
		{
			bot.QuizMode = true;
			bot.UpdateBotStatus().UnwrapThrow();
		}
		[Command("quiz off")]
		public static void CommandQuizOff(Bot bot, CallerInfo caller, InvokerData invoker)
		{
			if (!caller.ApiCall && invoker.Visibiliy.HasValue && invoker.Visibiliy == TextMessageTargetMode.Private)
				throw new CommandException(strings.cmd_quiz_off_no_cheating, CommandExceptionReason.CommandError);
			bot.QuizMode = false;
			bot.UpdateBotStatus().UnwrapThrow();
		}

		[Command("random")]
		public static JsonValue<bool> CommandRandom(PlaylistManager playlistManager) => new JsonValue<bool>(playlistManager.Random, string.Format(strings.info_status_random, playlistManager.Random ? strings.info_on : strings.info_off));
		[Command("random on")]
		public static void CommandRandomOn(PlaylistManager playlistManager) => playlistManager.Random = true;
		[Command("random off")]
		public static void CommandRandomOff(PlaylistManager playlistManager) => playlistManager.Random = false;
		[Command("random seed", "cmd_random_seed_help")]
		public static string CommandRandomSeed(PlaylistManager playlistManager)
		{
			string seed = Util.FromSeed(playlistManager.Seed);
			return string.IsNullOrEmpty(seed) ? strings.info_empty : seed;
		}
		[Command("random seed", "cmd_random_seed_string_help")]
		public static void CommandRandomSeed(PlaylistManager playlistManager, string newSeed)
		{
			if (newSeed.Any(c => !char.IsLetter(c)))
				throw new CommandException(strings.cmd_random_seed_only_letters_allowed, CommandExceptionReason.CommandError);
			playlistManager.Seed = Util.ToSeed(newSeed.ToLowerInvariant());
		}
		[Command("random seed", "cmd_random_seed_int_help")]
		public static void CommandRandomSeed(PlaylistManager playlistManager, int newSeed) => playlistManager.Seed = newSeed;

		[Command("repeat")]
		public static JsonValue<bool> CommandRepeat(IPlayerConnection playerConnection) => new JsonValue<bool>(playerConnection.Repeated, string.Format(strings.info_status_repeat, playerConnection.Repeated ? strings.info_on : strings.info_off));
		[Command("repeat on")]
		public static void CommandRepeatOn(IPlayerConnection playerConnection) => playerConnection.Repeated = true;
		[Command("repeat off")]
		public static void CommandRepeatOff(IPlayerConnection playerConnection) => playerConnection.Repeated = false;

		[Command("rights can")]
		public static JsonArray<string> CommandRightsCan(RightsManager rightsManager, Ts3Client ts3Client, CallerInfo caller, Bot bot = null, InvokerData invoker = null, params string[] rights)
		{
			var result = rightsManager.GetRightsSubset(caller, invoker, ts3Client, bot, rights);
			return new JsonArray<string>(result, result.Length > 0 ? string.Join(", ", result) : strings.info_empty);
		}

		[Command("rights reload")]
		public static JsonEmpty CommandRightsReload(RightsManager rightsManager)
		{
			if (rightsManager.ReadFile())
				return new JsonEmpty(strings.info_ok);

			// TODO: this can be done nicer by returning the errors and warnings from parsing
			throw new CommandException(strings.cmd_rights_reload_error_parsing_file, CommandExceptionReason.CommandError);
		}

		[Command("rng")]
		[Usage("", "Gets a number between 0 and 2147483647")]
		[Usage("<max>", "Gets a number between 0 and <max>")]
		[Usage("<min> <max>", "Gets a number between <min> and <max>")]
		public static int CommandRng(int? first = null, int? second = null)
		{
			if (first.HasValue && second.HasValue)
			{
				return Util.Random.Next(Math.Min(first.Value, second.Value), Math.Max(first.Value, second.Value));
			}
			else if (first.HasValue)
			{
				if (first.Value <= 0)
					throw new CommandException(strings.cmd_rng_value_must_be_positive, CommandExceptionReason.CommandError);
				return Util.Random.Next(first.Value);
			}
			else
			{
				return Util.Random.Next();
			}
		}

		[Command("seek")]
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
				else
				{
					span = TimeSpan.MinValue;
				}
			}
			else
			{
				parsed = int.TryParse(parameter, out int seconds);
				span = TimeSpan.FromSeconds(seconds);
			}

			if (!parsed)
				throw new CommandException(strings.cmd_seek_invalid_format, CommandExceptionReason.CommandError);
			else if (span < TimeSpan.Zero || span > playerConnection.Length)
				throw new CommandException(strings.cmd_seek_out_of_range, CommandExceptionReason.CommandError);
			else
				playerConnection.Position = span;
		}

		[Command("settings")]
		public static void CommandSettings()
		{
			throw new CommandException(string.Format(strings.cmd_settings_empty_usage, "'rights.path', 'web.api.enabled', 'tools.*'"),
				CommandExceptionReason.MissingParameter);
		}

		[Command("settings get")]
		public static JsonValue<ConfigPart> CommandSettingsGet(ConfBot config, string path)
			=> SettingsGet(config, path);

		[Command("settings set")]
		public static void CommandSettingsSet(ConfBot config, string path, string value)
		{
			SettingsSet(config, path, value);
			if (!config.SaveWhenExists())
			{
				throw new CommandException("Value was set but could not be saved to file. All changes are temporary and will be lost when the bot restarts.",
					CommandExceptionReason.CommandError);
			}
		}

		[Command("settings bot get", "cmd_settings_get_help")]
		public static JsonValue<ConfigPart> CommandSettingsBotGet(BotManager bots, ConfRoot config, string bot, string path)
		{
			using (var botlock = bots.GetBotLock(bot))
			{
				var confBot = GetConf(botlock.Bot, config, bot);
				return CommandSettingsGet(confBot, path);
			}
		}

		[Command("settings bot set", "cmd_settings_set_help")]
		public static void CommandSettingsBotSet(BotManager bots, ConfRoot config, string bot, string path, string value)
		{
			using (var botlock = bots.GetBotLock(bot))
			{
				var confBot = GetConf(botlock.Bot, config, bot);
				CommandSettingsSet(confBot, path, value);
			}
		}

		private static ConfBot GetConf(Bot bot, ConfRoot config, string name)
		{
			if (bot != null)
			{
				var mod = bot.Injector.GetModule<ConfBot>();
				if (mod != null)
					return mod;
				else
					throw new CommandException(strings.error_call_unexpected_error, CommandExceptionReason.CommandError);
			}
			else
			{
				var getTemplateResult = config.GetBotTemplate(name);
				if (!getTemplateResult.Ok)
					throw new CommandException(strings.error_bot_does_not_exist, getTemplateResult.Error, CommandExceptionReason.CommandError);
				return getTemplateResult.Value;
			}
		}

		[Command("settings global get")]
		public static JsonValue<ConfigPart> CommandSettingsGlobalGet(ConfRoot config, string path)
			=> SettingsGet(config, path);

		[Command("settings global set")]
		public static void CommandSettingsGlobalSet(ConfRoot config, string path, string value)
		{
			SettingsSet(config, path, value);
			if (!config.Save())
			{
				throw new CommandException("Value was set but could not be saved to file. All changes are temporary and will be lost when the bot restarts.",
					CommandExceptionReason.CommandError);
			}
		}

		private static JsonValue<ConfigPart> SettingsGet(ConfigPart config, string path)
		{
			var result = config.ByPathAsArray(path).SettingsGetSingle();
			return new JsonValue<ConfigPart>(result);
		}

		private static void SettingsSet(ConfigPart config, string path, string value)
		{
			var setConfig = config.ByPathAsArray(path).SettingsGetSingle();
			if (setConfig is IJsonSerializable jsonConfig)
			{
				var result = jsonConfig.FromJson(value);
				if (!result.Ok)
					throw new CommandException($"Failed to set the value ({result.Error}).", CommandExceptionReason.CommandError); // LOC: TODO
			}
			else
			{
				throw new CommandException("This value currently cannot be set.", CommandExceptionReason.CommandError); // LOC: TODO
			}
		}

		private static ConfigPart SettingsGetSingle(this ConfigPart[] configPartsList)
		{
			if (configPartsList.Length == 0)
			{
				throw new CommandException(strings.error_config_no_key_found, CommandExceptionReason.CommandError);
			}
			else if (configPartsList.Length == 1)
			{
				return configPartsList[0];
			}
			else
			{
				throw new CommandException(
					string.Format(
						strings.error_config_multiple_keys_found + "\n",
						string.Join("\n  ", configPartsList.Take(3).Select(kvp => kvp.Key))),
					CommandExceptionReason.CommandError);
			}
		}

		[Command("settings help")]
		public static string CommandSettingsHelp(ConfRoot config, string path)
		{
			var part = SettingsGet(config, path).Value;
			return string.IsNullOrEmpty(part.Documentation) ? strings.info_empty : part.Documentation;
		}

		[Command("song")]
		public static JsonValue<string> CommandSong(PlayManager playManager, ResourceFactoryManager factoryManager, Bot bot, CallerInfo caller, InvokerData invoker = null)
		{
			if (playManager.CurrentPlayData == null)
				throw new CommandException(strings.info_currently_not_playing, CommandExceptionReason.CommandError);
			if (bot.QuizMode && !caller.ApiCall && (invoker == null || playManager.CurrentPlayData.Invoker.ClientId != invoker.ClientId))
				throw new CommandException(strings.info_quizmode_is_active, CommandExceptionReason.CommandError);

			return new JsonValue<string>(
				playManager.CurrentPlayData.ResourceData.ResourceTitle,
				$"[url={factoryManager.RestoreLink(playManager.CurrentPlayData.ResourceData)}]{playManager.CurrentPlayData.ResourceData.ResourceTitle}[/url]");
		}

		[Command("song position")]
		public static JsonObject CommandSongPosition(IPlayerConnection playerConnection)
		{
			return JsonValue.Create(new
			{
				position = playerConnection.Position,
				length = playerConnection.Length,
			},
			x => x.length.TotalHours >= 1 || x.position.TotalHours >= 1
				? $"{x.position:hh\\:mm\\:ss}/{x.length:hh\\:mm\\:ss}"
				: $"{x.position:mm\\:ss}/{x.length:mm\\:ss}"
			);
		}

		[Command("stop")]
		public static void CommandStop(PlayManager playManager) => playManager.Stop();

		[Command("subscribe")]
		public static void CommandSubscribe(IVoiceTarget targetManager, InvokerData invoker)
		{
			if (invoker.ClientId.HasValue)
				targetManager.WhisperClientSubscribe(invoker.ClientId.Value);
		}

		[Command("subscribe tempchannel")]
		public static void CommandSubscribeTempChannel(IVoiceTarget targetManager, InvokerData invoker, ulong? channel = null)
		{
			var subChan = channel ?? invoker.ChannelId ?? 0;
			if (subChan != 0)
				targetManager.WhisperChannelSubscribe(subChan, true);
		}

		[Command("subscribe channel")]
		public static void CommandSubscribeChannel(IVoiceTarget targetManager, InvokerData invoker, ulong? channel = null)
		{
			var subChan = channel ?? invoker.ChannelId ?? 0;
			if (subChan != 0)
				targetManager.WhisperChannelSubscribe(subChan, false);
		}

		[Command("take")]
		[Usage("<count> <text>", "Take only <count> parts of the text")]
		[Usage("<count> <start> <text>", "Take <count> parts, starting with the part at <start>")]
		[Usage("<count> <start> <delimiter> <text>", "Specify another delimiter for the parts than spaces")]
		public static ICommandResult CommandTake(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			if (arguments.Count < 2)
				throw new CommandException(strings.error_cmd_at_least_two_argument, CommandExceptionReason.MissingParameter);

			int start = 0;
			string delimiter = null;

			// Get count
			var res = ((StringCommandResult)arguments[0].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
			if (!int.TryParse(res, out int count) || count < 0)
				throw new CommandException("Count must be an integer >= 0", CommandExceptionReason.CommandError); // LOC: TODO

			if (arguments.Count > 2)
			{
				// Get start
				res = ((StringCommandResult)arguments[1].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
				if (!int.TryParse(res, out start) || start < 0)
					throw new CommandException("Start must be an integer >= 0", CommandExceptionReason.CommandError); // LOC: TODO
			}

			// Get delimiter if exists
			if (arguments.Count > 3)
				delimiter = ((StringCommandResult)arguments[2].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;

			string text = ((StringCommandResult)arguments[Math.Min(arguments.Count - 1, 3)]
				.Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;

			var splitted = delimiter == null
				? text.Split()
				: text.Split(new[] { delimiter }, StringSplitOptions.None);
			if (splitted.Length < start + count)
				throw new CommandException(strings.cmd_take_not_enough_arguements, CommandExceptionReason.CommandError);
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

		[Command("unsubscribe")]
		public static void CommandUnsubscribe(IVoiceTarget targetManager, InvokerData invoker)
		{
			if (invoker.ClientId.HasValue)
				targetManager.WhisperClientUnsubscribe(invoker.ClientId.Value);
		}

		[Command("unsubscribe channel")]
		public static void CommandUnsubscribeChannel(IVoiceTarget targetManager, InvokerData invoker, ulong? channel = null)
		{
			var subChan = channel ?? invoker.ChannelId ?? 0;
			if (subChan != 0)
				targetManager.WhisperChannelUnsubscribe(subChan, false);
		}

		[Command("unsubscribe temporary")]
		public static void CommandUnsubscribeTemporary(IVoiceTarget targetManager) => targetManager.ClearTemporary();

		[Command("version")]
		public static JsonValue<BuildData> CommandVersion()
		{
			var data = SystemData.AssemblyData;
			return new JsonValue<BuildData>(data, data.ToLongString());
		}

		[Command("volume")]
		[Usage("<level>", "A new volume level between 0 and 100.")]
		[Usage("+/-<level>", "Adds or subtracts a value from the current volume.")]
		public static JsonValue<float> CommandVolume(ExecutionInformation info, IPlayerConnection playerConnection, CallerInfo caller, ConfBot config, UserSession session = null, string parameter = null)
		{
			if (string.IsNullOrEmpty(parameter))
				return new JsonValue<float>(playerConnection.Volume, string.Format(strings.cmd_volume_current, playerConnection.Volume));

			bool relPos = parameter.StartsWith("+", StringComparison.Ordinal);
			bool relNeg = parameter.StartsWith("-", StringComparison.Ordinal);
			string numberString = (relPos || relNeg) ? parameter.Remove(0, 1) : parameter;

			if (!float.TryParse(numberString, NumberStyles.Float, CultureInfo.InvariantCulture, out var volume))
				throw new CommandException(strings.cmd_volume_parse_error, CommandExceptionReason.CommandError);

			float newVolume;
			if (relPos) newVolume = playerConnection.Volume + volume;
			else if (relNeg) newVolume = playerConnection.Volume - volume;
			else newVolume = volume;

			if (newVolume < 0 || newVolume > AudioValues.MaxVolume)
				throw new CommandException(string.Format(strings.cmd_volume_is_limited, 0, AudioValues.MaxVolume), CommandExceptionReason.CommandError);

			if (newVolume <= config.Audio.MaxUserVolume || newVolume < playerConnection.Volume || caller.ApiCall)
			{
				playerConnection.Volume = newVolume;
			}
			else if (newVolume <= AudioValues.MaxVolume)
			{
				string ResponseVolume(string message)
				{
					if (TextUtil.GetAnswer(message) == Answer.Yes)
					{
						if (info.HasRights(RightHighVolume))
							playerConnection.Volume = newVolume;
						else
							return strings.cmd_volume_missing_high_volume_permission;
					}
					return null;
				}

				session.SetResponse(ResponseVolume);
				throw new CommandException(strings.cmd_volume_high_volume_confirm + YesNoOption, CommandExceptionReason.CommandError);
			}
			return null;
		}

		[Command("whisper all")]
		public static void CommandWhisperAll(IVoiceTarget targetManager) => CommandWhisperGroup(targetManager, GroupWhisperType.AllClients, GroupWhisperTarget.AllChannels);

		[Command("whisper group")]
		public static void CommandWhisperGroup(IVoiceTarget targetManager, GroupWhisperType type, GroupWhisperTarget target, ulong? targetId = null)
		{
			if (type == GroupWhisperType.ServerGroup || type == GroupWhisperType.ChannelGroup)
			{
				if (!targetId.HasValue)
					throw new CommandException(strings.cmd_whisper_group_missing_target, CommandExceptionReason.CommandError);
				targetManager.SetGroupWhisper(type, target, targetId.Value);
				targetManager.SendMode = TargetSendMode.WhisperGroup;
			}
			else
			{
				if (targetId.HasValue)
					throw new CommandException(strings.cmd_whisper_group_superfluous_target, CommandExceptionReason.CommandError);
				targetManager.SetGroupWhisper(type, target, 0);
				targetManager.SendMode = TargetSendMode.WhisperGroup;
			}
		}

		[Command("whisper list")]
		public static JsonObject CommandWhisperList(IVoiceTarget targetManager)
		{
			return JsonValue.Create(new
			{
				SendMode = targetManager.SendMode,
				GroupWhisper = targetManager.SendMode == TargetSendMode.WhisperGroup ?
				new
				{
					Target = targetManager.GroupWhisperTarget,
					TargetId = targetManager.GroupWhisperTargetId,
					Type = targetManager.GroupWhisperType,
				}
				: null,
				WhisperClients = targetManager.WhisperClients,
				WhisperChannel = targetManager.WhisperChannel,
			},
			x =>
			{
				var strb = new StringBuilder(strings.cmd_whisper_list_header);
				strb.AppendLine();
				switch (x.SendMode)
				{
				case TargetSendMode.None: strb.Append(strings.cmd_whisper_list_target_none); break;
				case TargetSendMode.Voice: strb.Append(strings.cmd_whisper_list_target_voice); break;
				case TargetSendMode.Whisper:
					strb.Append(strings.cmd_whisper_list_target_whisper_clients).Append(": [").Append(string.Join(",", x.WhisperClients)).Append("]\n");
					strb.Append(strings.cmd_whisper_list_target_whisper_channel).Append(": [").Append(string.Join(",", x.WhisperChannel)).Append("]");
					break;
				case TargetSendMode.WhisperGroup:
					strb.AppendFormat(strings.cmd_whisper_list_target_whispergroup, x.GroupWhisper.Type, x.GroupWhisper.Target, x.GroupWhisper.TargetId);
					break;
				default:
					throw new ArgumentOutOfRangeException();
				}
				return strb.ToString();
			});
		}

		[Command("whisper off")]
		public static void CommandWhisperOff(IVoiceTarget targetManager) => targetManager.SendMode = TargetSendMode.Voice;

		[Command("whisper subscription")]
		public static void CommandWhisperSubsription(IVoiceTarget targetManager) => targetManager.SendMode = TargetSendMode.Whisper;

		[Command("xecute")]
		public static void CommandXecute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			foreach (var arg in arguments)
				arg.Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnAnyPreferNothing);
		}
		// ReSharper enable UnusedMember.Global

		private static Playlist AutoGetPlaylist(UserSession session, InvokerData invoker)
		{
			if (session == null)
				throw new CommandException(strings.error_no_session_in_context, CommandExceptionReason.MissingContext);
			var result = session.Get<PlaylistManager, Playlist>();
			if (result)
				return result.Value;

			var newPlist = new Playlist(invoker.NickName, invoker.ClientUid);
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
			if (!info.TryGet<Ts3Client>(out var ts)) ts = null;
			if (!info.TryGet<Bot>(out var bot)) bot = null;
			return rightsManager.HasAllRights(caller, invoker, ts, bot, rights);
		}

		public static E<LocalStr> Write(this ExecutionInformation info, string message)
		{
			if (!info.TryGet<Ts3Client>(out var ts3Client))
				return new LocalStr(strings.error_no_teamspeak_in_context);

			if (!info.TryGet<InvokerData>(out var invoker))
				return new LocalStr(strings.error_no_invoker_in_context);

			if (!invoker.Visibiliy.HasValue || !invoker.ClientId.HasValue)
				return new LocalStr(strings.error_invoker_not_visible);

			switch (invoker.Visibiliy.Value)
			{
			case TextMessageTargetMode.Private:
				return ts3Client.SendMessage(message, invoker.ClientId.Value);
			case TextMessageTargetMode.Channel:
				return ts3Client.SendChannelMessage(message);
			case TextMessageTargetMode.Server:
				return ts3Client.SendServerMessage(message);
			default:
				throw Util.UnhandledDefault(invoker.Visibiliy.Value);
			}
		}
	}
}
