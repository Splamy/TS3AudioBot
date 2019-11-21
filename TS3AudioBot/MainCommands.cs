// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using TS3AudioBot.Algorithm;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.CommandSystem.Ast;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.CommandSystem.Text;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Helper;
using TS3AudioBot.Helper.Diagnose;
using TS3AudioBot.Helper.Environment;
using TS3AudioBot.History;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.Plugins;
using TS3AudioBot.ResourceFactories;
using TS3AudioBot.Rights;
using TS3AudioBot.Sessions;
using TS3AudioBot.Web.Api;
using TS3AudioBot.Web.Model;
using TSLib;
using TSLib.Audio;
using TSLib.Full.Book;
using TSLib.Helper;
using TSLib.Messages;

namespace TS3AudioBot
{
	public static class MainCommands
	{
		internal static ICommandBag Bag { get; } = new MainCommandsBag();

		internal class MainCommandsBag : ICommandBag
		{
			public IReadOnlyCollection<BotCommand> BagCommands { get; } = CommandManager.GetBotCommands(null, typeof(MainCommands)).ToArray();
			public IReadOnlyCollection<string> AdditionalRights { get; } = new string[] { RightHighVolume, RightDeleteAllPlaylists };
		}

		public const string RightHighVolume = "ts3ab.admin.volume";
		public const string RightDeleteAllPlaylists = "ts3ab.admin.list";

		private const string YesNoOption = " !(yes|no)";

		// [...] = Optional
		// <name> = Placeholder for a text
		// [text] = Option for fixed text
		// (a|b) = either or switch

		// ReSharper disable UnusedMember.Global
		[Command("add")]
		public static void CommandAdd(PlayManager playManager, InvokerData invoker, string url, params string[] attributes)
			=> playManager.Enqueue(invoker, url, meta: PlayManager.ParseAttributes(attributes)).UnwrapThrow();

		[Command("add")]
		public static void CommandAdd(PlayManager playManager, InvokerData invoker, IAudioResourceResult rsc, params string[] attributes)
			=> playManager.Enqueue(invoker, rsc.AudioResource, meta: PlayManager.ParseAttributes(attributes)).UnwrapThrow();

		[Command("alias add")]
		public static void CommandAliasAdd(CommandManager commandManager, ConfBot confBot, string commandName, string command)
		{
			commandManager.RegisterAlias(commandName, command).UnwrapThrow();

			var confEntry = confBot.Commands.Alias.GetOrCreateItem(commandName);
			confEntry.Value = command;
			confBot.SaveWhenExists().UnwrapThrow();
		}

		[Command("alias remove")]
		public static void CommandAliasRemove(CommandManager commandManager, ConfBot confBot, string commandName)
		{
			commandManager.UnregisterAlias(commandName).UnwrapThrow();

			confBot.Commands.Alias.RemoveItem(commandName);
			confBot.SaveWhenExists().UnwrapThrow();
		}

		[Command("alias list")]
		public static JsonArray<string> CommandAliasList(CommandManager commandManager)
			=> new JsonArray<string>(commandManager.AllAlias.ToArray(), x => string.Join(",", x));

		[Command("alias show")]
		public static string CommandAliasShow(CommandManager commandManager, string commandName)
			=> commandManager.GetAlias(commandName)?.AliasString;

		[Command("api token")]
		[Usage("[<duration>]", "Optionally specifies a duration this key is valid in hours.")]
		public static string CommandApiToken(TokenManager tokenManager, ClientCall invoker, double? validHours = null)
		{
			if (invoker.Visibiliy.HasValue && invoker.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException(strings.error_use_private, CommandExceptionReason.CommandError);
			if (invoker.IsAnonymous)
				throw new MissingContextCommandException(strings.error_no_uid_found, typeof(ClientCall));

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
			return tokenManager.GenerateToken(invoker.ClientUid.Value, validSpan);
		}

		[Command("bot avatar set")]
		public static void CommandBotAvatarSet(Ts3Client ts3Client, string url)
		{
			url = TextUtil.ExtractUrlFromBb(url);
			Uri uri;
			try { uri = new Uri(url); }
			catch (Exception ex) { throw new CommandException(strings.error_media_invalid_uri, ex, CommandExceptionReason.CommandError); }

			WebWrapper.GetResponseLoc(uri, x =>
			{
				using (var stream = x.GetResponseStream())
				{
					var imageResult = ImageUtil.ResizeImageSave(stream, out _);
					if (!imageResult.Ok)
						return imageResult.Error;
					return ts3Client.UploadAvatar(imageResult.Value);
				}
			}).UnwrapThrow();
		}

		[Command("bot avatar clear")]
		public static void CommandBotAvatarClear(Ts3Client ts3Client) => ts3Client.DeleteAvatar().UnwrapThrow();

		[Command("bot badges")]
		public static void CommandBotBadges(Ts3Client ts3Client, string badges) => ts3Client.ChangeBadges(badges).UnwrapThrow();

		[Command("bot description set")]
		public static void CommandBotDescriptionSet(Ts3Client ts3Client, string description) => ts3Client.ChangeDescription(description).UnwrapThrow();

		[Command("bot diagnose", "_undocumented")]
		public static JsonArray<SelfDiagnoseMessage> CommandBotDiagnose(Player player, IVoiceTarget target, Connection book)
		{
			var problems = new List<SelfDiagnoseMessage>();
			// ** Diagnose common playback problems and more **

			var self = book.Self();
			var curChan = book.CurrentChannel();

			// Check talk power
			if (!self.TalkPowerGranted && self.TalkPower < curChan.NeededTalkPower)
				problems.Add(new SelfDiagnoseMessage { Description = "The bot does not have enough talk power.", LevelValue = SelfDiagnoseLevel.Warning });

			// Check volume 0
			if (player.Volume == 0)
				problems.Add(new SelfDiagnoseMessage { Description = "The volume level is at 0.", LevelValue = SelfDiagnoseLevel.Warning });

			// Check if send mode hasn't been selected yet
			if (target.SendMode == TargetSendMode.None)
				problems.Add(new SelfDiagnoseMessage { Description = "Send mode is currently 'None', use '!whisper off' for example to send via voice.", LevelValue = SelfDiagnoseLevel.Warning });

			// ... more

			return new JsonArray<SelfDiagnoseMessage>(problems, x =>
			{
				if (x.Count == 0)
					return "No problems detected";
				var strb = new StringBuilder("The following issues have been found:");
				foreach (var prob in x)
					strb.Append("\n- ").Append(prob.Description);
				return strb.ToString();
			});
		}

		[Command("bot disconnect")]
		public static void CommandBotDisconnect(BotManager bots, Bot bot) => bots.StopBot(bot);

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
		public static void CommandBotCome(Ts3Client ts3Client, ClientCall invoker, string password = null)
		{
			var channel = invoker?.ChannelId;
			if (channel == null)
				throw new CommandException(strings.error_no_target_channel, CommandExceptionReason.CommandError);
			ts3Client.MoveTo(channel.Value, password).UnwrapThrow();
		}

		[Command("bot connect template")]
		public static BotInfo CommandBotConnectTo(BotManager bots, string name)
		{
			var botInfo = bots.RunBotTemplate(name);
			if (!botInfo.Ok)
				throw new CommandException(strings.error_could_not_create_bot + $" ({botInfo.Error})", CommandExceptionReason.CommandError);
			return botInfo.Value;
		}

		[Command("bot connect to")]
		public static BotInfo CommandBotConnectNew(BotManager bots, string address, string password = null)
		{
			var botConf = bots.CreateNewBot();
			botConf.Connect.Address.Value = address;
			if (!string.IsNullOrEmpty(password))
				botConf.Connect.ServerPassword.Password.Value = password;
			var botInfo = bots.RunBot(botConf);
			if (!botInfo.Ok)
				throw new CommandException(strings.error_could_not_create_bot + $" ({botInfo.Error})", CommandExceptionReason.CommandError);
			return botInfo.Value;
		}

		[Command("bot info")]
		public static BotInfo CommandBotInfo(Bot bot) => bot.GetInfo();

		[Command("bot info client", "_undocumented")]
		public static JsonValue<ClientInfo> CommandBotInfoClient(Ts3Client ts3Client, ApiCall _)
			=> new JsonValue<ClientInfo>(ts3Client.GetSelf().UnwrapThrow(), string.Empty);

		[Command("bot list")]
		public static JsonArray<BotInfo> CommandBotList(BotManager bots, ConfRoot config)
		{
			var botInfoList = bots.GetBotInfolist();
			var botConfigList = config.GetAllBots();
			var infoList = new Dictionary<string, BotInfo>();
			foreach (var botInfo in botInfoList.Where(x => !string.IsNullOrEmpty(x.Name)))
				infoList[botInfo.Name] = botInfo;
			foreach (var botConfig in botConfigList)
			{
				if (infoList.ContainsKey(botConfig.Name))
					continue;
				infoList[botConfig.Name] = new BotInfo
				{
					Id = null,
					Name = botConfig.Name,
					Server = botConfig.Connect.Address,
					Status = BotStatus.Offline,
				};
			}
			return new JsonArray<BotInfo>(infoList.Values.Concat(botInfoList.Where(x => string.IsNullOrEmpty(x.Name))).ToArray(),
				bl => string.Join("\n", bl.Select(x => x.ToString())));
		}

		[Command("bot move")]
		public static void CommandBotMove(Ts3Client ts3Client, ulong channel, string password = null) => ts3Client.MoveTo((ChannelId)channel, password).UnwrapThrow();

		[Command("bot name")]
		public static void CommandBotName(Ts3Client ts3Client, string name) => ts3Client.ChangeName(name).UnwrapThrow();

		[Command("bot save")]
		public static void CommandBotSetup(ConfBot botConfig, string name) => botConfig.SaveNew(name).UnwrapThrow();

		[Command("bot setup")]
		public static void CommandBotSetup(Ts3Client ts3Client, string adminToken = null)
		{
			if (!ts3Client.SetupRights(adminToken))
				throw new CommandException(strings.cmd_bot_setup_error, CommandExceptionReason.CommandError);
		}

		[Command("bot template", "cmd_bot_use_help")]
		public static object CommandBotTemplate(ExecutionInformation info, IReadOnlyList<Type> returnTypes, BotManager bots, string botName, ICommand cmd)
		{
			using (var botLock = bots.GetBotLock(botName))
				return CommandBotUseInternal(info, returnTypes, botLock, cmd);
		}

		[Command("bot use")]
		public static object CommandBotUse(ExecutionInformation info, IReadOnlyList<Type> returnTypes, BotManager bots, int botId, ICommand cmd)
		{
			using (var botLock = bots.GetBotLock(botId))
				return CommandBotUseInternal(info, returnTypes, botLock, cmd);
		}

		private static object CommandBotUseInternal(ExecutionInformation info, IReadOnlyList<Type> returnTypes, BotLock botLock, ICommand cmd)
		{
			if (botLock is null)
				throw new CommandException(strings.error_bot_does_not_exist, CommandExceptionReason.CommandError);

			var backParent = info.ParentInjector;
			info.ParentInjector = botLock.Bot.Injector;
			string backUpId = NLog.MappedDiagnosticsContext.Get("BotId");
			NLog.MappedDiagnosticsContext.Set("BotId", botLock.Bot.Id.ToString());
			try
			{
				return cmd.Execute(info, Array.Empty<ICommand>(), returnTypes);
			}
			finally
			{
				NLog.MappedDiagnosticsContext.Set("BotId", backUpId);
				info.ParentInjector = backParent;
			}
		}

		[Command("clear")]
		public static void CommandClear(PlaylistManager playlistManager) => playlistManager.Clear();

		[Command("command parse", "cmd_parse_command_help")]
		public static JsonValue<AstNode> CommandParse(string parameter)
		{
			var node = CommandParser.ParseCommandRequest(parameter);
			var strb = new StringBuilder();
			strb.AppendLine();
			node.Write(strb, 0);
			return new JsonValue<AstNode>(node, strb.ToString());
		}

		[Command("command tree", "_undocumented")]
		public static string CommandTree(CommandManager commandManager)
		{
			return XCommandSystem.GetTree(commandManager.CommandSystem.RootCommand);
		}

		[Command("data song cover get", "_undocumented")]
		public static DataStream CommandData(ResourceResolver resourceFactory, PlayManager playManager) =>
			new DataStream(response =>
			{
				var cur = playManager.CurrentPlayData;
				if (cur == null)
					return false;
				if (resourceFactory.GetThumbnail(cur.PlayResource).GetOk(out var stream)
					&& ImageUtil.ResizeImageSave(stream, out var mime).GetOk(out var image))
				{
					using (image)
					{
						response.ContentType = mime;
						image.CopyTo(response.Body);
						return true;
					}
				}
				return false;
			});

		[Command("eval")]
		[Usage("<command> <arguments...>", "Executes the given command on arguments")]
		[Usage("<strings...>", "Concat the strings and execute them with the command system")]
		public static object CommandEval(ExecutionInformation info, CommandManager commandManager, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			// Evaluate the first argument on the rest of the arguments
			if (arguments.Count == 0)
				throw new CommandException(strings.error_cmd_at_least_one_argument, CommandExceptionReason.MissingParameter);
			var leftArguments = arguments.TrySegment(1);
			var arg0 = arguments[0].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnCommandOrString);
			if (arg0 is ICommand cmd)
				return cmd.Execute(info, leftArguments, returnTypes);

			// We got a string back so parse and evaluate it
			var args = ((IPrimitiveResult<string>)arg0).Get();

			cmd = commandManager.CommandSystem.AstToCommandResult(CommandParser.ParseCommandRequest(args));
			return cmd.Execute(info, leftArguments, returnTypes);
		}

		[Command("get", "_undocumented")]
		[Usage("<index> <list...>", "Get an element out of a list")]
		public static object CommandGet(uint index, System.Collections.IEnumerable list)
		{
			foreach (var i in list)
			{
				if (index == 0)
					return i;
				index--;
			}
			return null;
		}

		[Command("getmy id")]
		public static ushort CommandGetId(ClientCall invoker)
			=> invoker.ClientId?.Value ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy uid")]
		public static string CommandGetUid(ClientCall invoker)
			=> invoker.ClientUid.Value;
		[Command("getmy name")]
		public static string CommandGetName(ClientCall invoker)
			=> invoker.NickName ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy dbid")]
		public static ulong CommandGetDbId(ClientCall invoker)
			=> invoker.DatabaseId?.Value ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy channel")]
		public static ulong CommandGetChannel(ClientCall invoker)
			=> invoker.ChannelId?.Value ?? throw new CommandException(strings.error_not_found, CommandExceptionReason.CommandError);
		[Command("getmy all")]
		public static JsonValue<ClientCall> CommandGetUser(ClientCall invoker)
			=> new JsonValue<ClientCall>(invoker, $"Client: Id:{invoker.ClientId} DbId:{invoker.DatabaseId} ChanId:{invoker.ChannelId} Uid:{invoker.ClientUid}"); // LOC: TODO

		[Command("getuser uid byid")]
		public static string CommandGetUidById(Ts3Client ts3Client, ushort id) => ts3Client.GetFallbackedClientById((ClientId)id).UnwrapThrow().Uid.Value;
		[Command("getuser name byid")]
		public static string CommandGetNameById(Ts3Client ts3Client, ushort id) => ts3Client.GetFallbackedClientById((ClientId)id).UnwrapThrow().Name;
		[Command("getuser dbid byid")]
		public static ulong CommandGetDbIdById(Ts3Client ts3Client, ushort id) => ts3Client.GetFallbackedClientById((ClientId)id).UnwrapThrow().DatabaseId.Value;
		[Command("getuser channel byid")]
		public static ulong CommandGetChannelById(Ts3Client ts3Client, ushort id) => ts3Client.GetFallbackedClientById((ClientId)id).UnwrapThrow().ChannelId.Value;
		[Command("getuser all byid")]
		public static JsonValue<ClientList> CommandGetUserById(Ts3Client ts3Client, ushort id)
		{
			var client = ts3Client.GetFallbackedClientById((ClientId)id).UnwrapThrow();
			return new JsonValue<ClientList>(client, $"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}");
		}
		[Command("getuser id byname")]
		public static ushort CommandGetIdByName(Ts3Client ts3Client, string username) => ts3Client.GetClientByName(username).UnwrapThrow().ClientId.Value;
		[Command("getuser all byname")]
		public static JsonValue<ClientList> CommandGetUserByName(Ts3Client ts3Client, string username)
		{
			var client = ts3Client.GetClientByName(username).UnwrapThrow();
			return new JsonValue<ClientList>(client, $"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}");
		}
		[Command("getuser name bydbid")]
		public static string CommandGetNameByDbId(Ts3Client ts3Client, ulong dbId) => ts3Client.GetDbClientByDbId((ClientDbId)dbId).UnwrapThrow().Name;
		[Command("getuser uid bydbid")]
		public static string CommandGetUidByDbId(Ts3Client ts3Client, ulong dbId) => ts3Client.GetDbClientByDbId((ClientDbId)dbId).UnwrapThrow().Uid.Value;

		private static readonly TextMod HelpCommand = new TextMod(TextModFlag.Bold);
		private static readonly TextMod HelpCommandParam = new TextMod(TextModFlag.Italic);

		[Command("help")]
		public static string CommandHelp(CallerInfo callerInfo)
		{
			var tmb = new TextModBuilder(callerInfo.IsColor);
			tmb.AppendLine("TS3AudioBot at your service!");
			tmb.AppendLine("To get some basic help on how to get started use one of the following commands:");
			tmb.Append("!help play", HelpCommand).AppendLine(" : basics for playing songs");
			tmb.Append("!help playlists", HelpCommand).AppendLine(" : how to manage playlists");
			tmb.Append("!help history", HelpCommand).AppendLine(" : viewing and accesing the play history");
			tmb.Append("!help bot", HelpCommand).AppendLine(" : useful features to configure your bot");
			tmb.Append("!help all", HelpCommand).AppendLine(" : show all commands");
			tmb.Append("!help command", HelpCommand).Append(" <command path>", HelpCommandParam).AppendLine(" : help text of a specific command");
			var str = tmb.ToString();
			return str;
		}

		[Command("help all", "_undocumented")]
		public static JsonArray<string> CommandHelpAll(CommandManager commandManager)
		{
			var botComList = commandManager.CommandSystem.RootCommand.Commands.Select(c => c.Key).ToArray();
			return new JsonArray<string>(botComList, bcl =>
			{
				var strb = new StringBuilder();
				foreach (var botCom in bcl)
					strb.Append(botCom).Append(", ");
				strb.Length -= 2;
				return strb.ToString();
			});
		}

		[Command("help command", "_undocumented")]
		public static JsonObject CommandHelpCommand(CommandManager commandManager, IFilter filter = null, params string[] command)
		{
			if (command.Length == 0)
			{
				return new JsonEmpty(strings.error_cmd_at_least_one_argument);
			}

			CommandGroup group = commandManager.CommandSystem.RootCommand;
			ICommand target = group;
			filter = filter ?? Filter.DefaultFilter;
			var realPath = new List<string>();
			for (int i = 0; i < command.Length; i++)
			{
				var possibilities = filter.Filter(group.Commands, command[i]).ToList();
				if (possibilities.Count <= 0)
					throw new CommandException(strings.cmd_help_error_no_matching_command, CommandExceptionReason.CommandError);
				if (possibilities.Count > 1)
					throw new CommandException(string.Format(strings.cmd_help_error_ambiguous_command, string.Join(", ", possibilities.Select(kvp => kvp.Key))), CommandExceptionReason.CommandError);

				realPath.Add(possibilities[0].Key);
				target = possibilities[0].Value;

				if (i < command.Length - 1)
				{
					group = target as CommandGroup;
					if (group is null)
						throw new CommandException(string.Format(strings.cmd_help_error_no_further_subfunctions, string.Join(" ", realPath, 0, i)), CommandExceptionReason.CommandError);
				}
			}

			switch (target)
			{
			case BotCommand targetB:
				return new JsonValue<BotCommand>(targetB);
			case CommandGroup targetCg:
				var subList = targetCg.Commands.Select(g => g.Key).ToArray();
				return new JsonArray<string>(subList, string.Format(strings.cmd_help_info_contains_subfunctions, string.Join(", ", subList)));
			case OverloadedFunctionCommand targetOfc:
				var strb = new StringBuilder();
				foreach (var botCom in targetOfc.Functions.OfType<BotCommand>())
					strb.Append(botCom);
				return new JsonValue<string>(strb.ToString());
			case AliasCommand targetAlias:
				return new JsonValue<string>(string.Format("'{0}' is an alias for:\n{1}", string.Join(" ", realPath), targetAlias.AliasString));
			default:
				throw new CommandException(strings.cmd_help_error_unknown_error, CommandExceptionReason.CommandError);
			}
		}

		[Command("help play", "_undocumented")]
		public static string CommandHelpPlay()
		{
			return "";
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
		public static JsonEmpty CommandHistoryCleanRemove(HistoryManager historyManager, ResourceResolver resourceFactory, CallerInfo caller, UserSession session = null)
		{
			if (caller.ApiCall)
			{
				historyManager.RemoveBrokenLinks(resourceFactory);
				return new JsonEmpty(string.Empty);
			}

			string ResponseHistoryCleanRemove(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					historyManager.RemoveBrokenLinks(resourceFactory);
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
			return new JsonValue<AudioLogEntry>(result, r => historyManager.Format(r));
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
			if (ale is null)
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
			switch (time.ToLowerInvariant())
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
		public static object CommandIf(ExecutionInformation info, IReadOnlyList<Type> returnTypes, string arg0, string cmp, string arg1, ICommand then, ICommand other = null)
		{
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
				return then.Execute(info, Array.Empty<ICommand>(), returnTypes);
			// Else branch
			if (other != null)
				return other.Execute(info, Array.Empty<ICommand>(), returnTypes);

			// Try to return nothing
			if (returnTypes.Contains(null))
				return null;
			throw new CommandException(strings.error_nothing_to_return, CommandExceptionReason.NoReturnMatch);
		}

		private static readonly TextMod SongDone = new TextMod(TextModFlag.Color, Color.Gray);
		private static readonly TextMod SongCurrent = new TextMod(TextModFlag.Bold);

		private static int GetIndexExpression(PlaylistManager playlistManager, string expression)
		{
			int index;
			if (string.IsNullOrEmpty(expression))
			{
				index = playlistManager.Index;
			}
			else if (expression.StartsWith("@"))
			{
				var subOffset = expression.Substring(1).Trim();
				if (string.IsNullOrEmpty(subOffset))
					index = 0;
				else if (!int.TryParse(subOffset, out index))
					throw new CommandException(strings.error_unrecognized_descriptor, CommandExceptionReason.CommandError);
				index += playlistManager.Index;
			}
			else if (!int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
			{
				throw new CommandException(strings.error_unrecognized_descriptor, CommandExceptionReason.CommandError);
			}
			return index;
		}

		[Command("info")]
		public static JsonValue<QueueInfo> CommandInfo(ResourceResolver resourceFactory, PlaylistManager playlistManager, string offset = null, int? count = null)
			=> CommandInfo(resourceFactory, playlistManager, GetIndexExpression(playlistManager, offset ?? "@-1"), count);

		[Command("info")]
		public static JsonValue<QueueInfo> CommandInfo(ResourceResolver resourceFactory, PlaylistManager playlistManager, int offset, int? count = null)
		{
			const int maxSongs = 20;
			var playIndex = playlistManager.Index;
			var plist = playlistManager.CurrentList;
			int offsetV = Tools.Clamp(offset, 0, plist.Items.Count);
			int countV = Tools.Clamp(count ?? 3, 0, Math.Min(maxSongs, plist.Items.Count - offsetV));
			var items = plist.Items.Skip(offsetV).Take(countV).Select(x => resourceFactory.ToApiFormat(x)).ToArray();

			var plInfo = new QueueInfo
			{
				Id = ".mix",
				Title = plist.Title,
				SongCount = plist.Items.Count,
				DisplayOffset = offsetV,
				Items = items,
				PlaybackIndex = playIndex,
			};

			return JsonValue.Create(plInfo, x =>
			{
				if (x.SongCount == 0)
					return strings.info_currently_not_playing;

				var tmb = new TextModBuilder();

				string CurLine(int i) => $"{x.DisplayOffset + i}: {x.Items[i].Title}";

				tmb.AppendFormat(strings.cmd_list_show_header, x.Title.Mod().Bold(), x.SongCount.ToString()).Append("\n");

				for (int i = 0; i < x.Items.Length; i++)
				{
					var line = CurLine(i);
					var plIndex = x.DisplayOffset + i;
					if (plIndex == x.PlaybackIndex)
						tmb.AppendLine("> " + line, SongCurrent);
					else if (plIndex < x.PlaybackIndex)
						tmb.AppendLine(line, SongDone);
					else if (plIndex > x.PlaybackIndex)
						tmb.AppendLine(line);
					else
						break; // ?
				}

				return tmb.ToString();
			});
		}

		[Command("json merge")]
		public static JsonArray<object> CommandJsonMerge(ExecutionInformation info, ApiCall _, IReadOnlyList<ICommand> arguments)
		{
			if (arguments.Count == 0)
				return new JsonArray<object>(Array.Empty<object>(), string.Empty);

			var jsonArr = arguments
				.Select(arg =>
				{
					object res;
					try { res = arg.Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnJson); }
					catch (CommandException) { return null; }
					if (res is JsonObject o)
						return o.GetSerializeObject();
					else
						throw new CommandException(strings.error_nothing_to_return, CommandExceptionReason.NoReturnMatch);
				})
				.ToArray();

			return new JsonArray<object>(jsonArr, string.Empty);
		}

		[Command("json api", "_undocumented")]
		public static JsonObject CommandJsonApi(CommandManager commandManager, ApiCall _, BotManager botManager = null)
		{
			var bots = botManager?.GetBotInfolist() ?? Array.Empty<BotInfo>();
			var api = OpenApiGenerator.Generate(commandManager, bots);
			return new JsonValue<JObject>(api, string.Empty);
		}

		[Command("kickme")]
		public static void CommandKickme(Ts3Client ts3Client, ClientCall invoker)
			=> CommandKickme(ts3Client, invoker, false);

		[Command("kickme far", "cmd_kickme_help")]
		public static void CommandKickmeFar(Ts3Client ts3Client, ClientCall invoker)
			=> CommandKickme(ts3Client, invoker, true);

		private static void CommandKickme(Ts3Client ts3Client, ClientCall invoker, bool far)
		{
			if (!invoker.ClientId.HasValue)
				return;

			E<LocalStr> result = far
				? ts3Client.KickClientFromServer(invoker.ClientId.Value)
				: ts3Client.KickClientFromChannel(invoker.ClientId.Value);
			if (!result.Ok)
				throw new CommandException(strings.cmd_kickme_missing_permission, CommandExceptionReason.CommandError);
		}

		[Command("list add")]
		public static JsonValue<PlaylistItemGetData> CommandListAddInternal(ResourceResolver resourceFactory, PlaylistManager playlistManager, string listId, string link /* TODO param */)
		{
			PlaylistItemGetData getData = null;
			playlistManager.ModifyPlaylist(listId, plist =>
			{
				var playResource = resourceFactory.Load(link).UnwrapThrow();
				var item = new PlaylistItem(playResource.BaseData);
				plist.Add(item).UnwrapThrow();
				getData = resourceFactory.ToApiFormat(item);
				//getData.Index = plist.Items.Count - 1;
			}).UnwrapThrow();
			return JsonValue.Create(getData, strings.info_ok);
		}

		[Command("list create", "_undocumented")]
		public static void CommandListCreate(PlaylistManager playlistManager, string listId, string title = null)
			=> playlistManager.CreatePlaylist(listId, title ?? listId).UnwrapThrow();

		[Command("list delete")]
		public static JsonEmpty CommandListDelete(PlaylistManager playlistManager, UserSession session, string listId)
		{
			string ResponseListDelete(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					playlistManager.DeletePlaylist(listId).UnwrapThrow();
				}
				return null;
			}

			session.SetResponse(ResponseListDelete);
			return new JsonEmpty(string.Format(strings.cmd_list_delete_confirm, listId));
		}

		[Command("list delete")]
		public static void CommandListDelete(PlaylistManager playlistManager, ApiCall _, string listId)
			=> playlistManager.DeletePlaylist(listId).UnwrapThrow();

		[Command("list import", "cmd_list_get_help")] // TODO readjust help texts
		public static JsonValue<PlaylistInfo> CommandListImport(PlaylistManager playlistManager, ResourceResolver resourceFactory, string listId, string link)
		{
			var getList = resourceFactory.LoadPlaylistFrom(link).UnwrapThrow();

			if (!playlistManager.ExistsPlaylist(listId))
				playlistManager.CreatePlaylist(listId).UnwrapThrow();

			playlistManager.ModifyPlaylist(listId, playlist =>
			{
				playlist.AddRange(getList.Items).UnwrapThrow();
			}).UnwrapThrow();

			return CommandListShow(playlistManager, resourceFactory, listId, null, null);
		}

		[Command("list insert", "_undocumented")]  // TODO Doc
		public static JsonValue<PlaylistItemGetData> CommandListAddInternal(ResourceResolver resourceFactory, PlaylistManager playlistManager, string listId, int index, string link /* TODO param */)
		{
			PlaylistItemGetData getData = null;
			playlistManager.ModifyPlaylist(listId, plist =>
			{
				if (index < 0 || index >= plist.Items.Count)
					throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);

				var playResource = resourceFactory.Load(link).UnwrapThrow();
				var item = new PlaylistItem(playResource.BaseData);
				plist.Insert(index, item).UnwrapThrow();
				getData = resourceFactory.ToApiFormat(item);
				//getData.Index = plist.Items.Count - 1;
			}).UnwrapThrow();
			return JsonValue.Create(getData, strings.info_ok);
		}

		[Command("list item get", "_undocumented")]
		public static PlaylistItem CommandListItemMove(PlaylistManager playlistManager, string name, int index)
		{
			var plist = playlistManager.LoadPlaylist(name).UnwrapThrow();
			if (index < 0 || index >= plist.Items.Count)
				throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);

			return plist[index];
		}

		[Command("list item move")] // TODO return modified elements
		public static void CommandListItemMove(PlaylistManager playlistManager, string listId, int from, int to)
		{
			playlistManager.ModifyPlaylist(listId, playlist =>
			{
				if (from < 0 || from >= playlist.Items.Count
					|| to < 0 || to >= playlist.Items.Count)
				{
					throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);
				}

				if (from == to)
					return;

				var plitem = playlist[from];
				playlist.RemoveAt(from);
				playlist.Insert(to, plitem).UnwrapThrow();
			}).UnwrapThrow();
		}

		[Command("list item delete")] // TODO return modified elements
		public static JsonEmpty CommandListItemDelete(PlaylistManager playlistManager, string listId, int index /* TODO param */)
		{
			PlaylistItem deletedItem = null;
			playlistManager.ModifyPlaylist(listId, plist =>
			{
				if (index < 0 || index >= plist.Items.Count)
					throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);

				deletedItem = plist[index];
				plist.RemoveAt(index);
			}).UnwrapThrow();
			return new JsonEmpty(string.Format(strings.info_removed, deletedItem));
		}

		[Command("list item name")] // TODO return modified elements
		public static void CommandListItemName(PlaylistManager playlistManager, string listId, int index, string title)
		{
			playlistManager.ModifyPlaylist(listId, plist =>
			{
				if (index < 0 || index >= plist.Items.Count)
					throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);

				plist[index].AudioResource.ResourceTitle = title;
			}).UnwrapThrow();
		}

		[Command("list list")]
		[Usage("<pattern>", "Filters all lists cantaining the given pattern.")]
		public static JsonArray<PlaylistInfo> CommandListList(PlaylistManager playlistManager, string pattern = null)
		{
			var files = playlistManager.GetAvailablePlaylists(pattern).UnwrapThrow();
			if (files.Length <= 0)
				return new JsonArray<PlaylistInfo>(files, strings.error_playlist_not_found);

			return new JsonArray<PlaylistInfo>(files, fi => string.Join(", ", fi.Select(x => x.Id)));
		}

		[Command("list merge")]
		public static void CommandListMerge(PlaylistManager playlistManager, string baseListId, string mergeListId) // future overload?: (IROP, IROP) -> IROP
		{
			var otherList = playlistManager.LoadPlaylist(mergeListId).UnwrapThrow();
			playlistManager.ModifyPlaylist(baseListId, playlist =>
			{
				playlist.AddRange(otherList.Items).UnwrapThrow();
			}).UnwrapThrow();
		}

		[Command("list name")]
		public static void CommandListName(PlaylistManager playlistManager, string listId, string title)
			=> playlistManager.ModifyPlaylist(listId, plist => plist.SetTitle(title)).UnwrapThrow();

		[Command("list play")]
		public static void CommandListPlayInternal(PlaylistManager playlistManager, PlayManager playManager, InvokerData invoker, string listId, int? index = null)
		{
			var plist = playlistManager.LoadPlaylist(listId).UnwrapThrow();

			if (plist.Items.Count == 0)
				throw new CommandException(strings.error_playlist_is_empty, CommandExceptionReason.CommandError);

			if (index.HasValue && (index.Value < 0 || index.Value >= plist.Items.Count))
				throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);

			playManager.Play(invoker, plist.Items, index ?? 0).UnwrapThrow();
		}

		[Command("list queue")]
		public static void CommandListQueue(PlaylistManager playlistManager, PlayManager playManager, InvokerData invoker, string listId)
		{
			var plist = playlistManager.LoadPlaylist(listId).UnwrapThrow();
			playManager.Enqueue(invoker, plist.Items).UnwrapThrow();
		}

		[Command("list show")]
		[Usage("<name> <index>", "Lets you specify the starting index from which songs should be listed.")]
		public static JsonValue<PlaylistInfo> CommandListShow(PlaylistManager playlistManager, ResourceResolver resourceFactory, string listId, int? offset = null, int? count = null)
		{
			const int maxSongs = 20;
			var plist = playlistManager.LoadPlaylist(listId).UnwrapThrow();
			int offsetV = Tools.Clamp(offset ?? 0, 0, plist.Items.Count);
			int countV = Tools.Clamp(count ?? maxSongs, 0, Math.Min(maxSongs, plist.Items.Count - offsetV));
			var items = plist.Items.Skip(offsetV).Take(countV).Select(x => resourceFactory.ToApiFormat(x)).ToArray();
			var plInfo = new PlaylistInfo
			{
				Id = listId,
				Title = plist.Title,
				SongCount = plist.Items.Count,
				DisplayOffset = offsetV,
				Items = items,
			};

			return JsonValue.Create(plInfo, x =>
			{
				var tmb = new TextModBuilder();
				tmb.AppendFormat(strings.cmd_list_show_header, x.Title.Mod().Bold(), x.SongCount.ToString()).Append("\n");
				var index = x.DisplayOffset;
				foreach (var plitem in x.Items)
					tmb.Append((index++).ToString()).Append(": ").AppendLine(plitem.Title);
				return tmb.ToString();
			});
		}

		[Command("next")]
		public static void CommandNext(PlayManager playManager, InvokerData invoker)
			=> playManager.Next(invoker).UnwrapThrow();

		[Command("param", "_undocumented")] // TODO add documentation, when name decided
		public static object CommandParam(ExecutionInformation info, IReadOnlyList<Type> resultTypes, int index)
		{
			if (!info.TryGet<AliasContext>(out var ctx) || ctx.Arguments == null)
				throw new CommandException("No parameter available", CommandExceptionReason.CommandError);

			if (index < 0 || index >= ctx.Arguments.Count)
				return XCommandSystem.GetEmpty(resultTypes);

			var backup = ctx.Arguments;
			ctx.Arguments = null;
			var result = backup[index].Execute(info, Array.Empty<ICommand>(), resultTypes);
			ctx.Arguments = backup;
			return result;
		}

		[Command("pm")]
		public static string CommandPm(ClientCall invoker)
		{
			invoker.Visibiliy = TextMessageTargetMode.Private;
			return string.Format(strings.cmd_pm_hi, invoker.NickName ?? "Anonymous");
		}

		[Command("pm channel", "_undocumented")] // TODO
		public static void CommandPmChannel(Ts3Client ts3Client, string message) => ts3Client.SendChannelMessage(message).UnwrapThrow();

		[Command("pm server", "_undocumented")] // TODO
		public static void CommandPmServer(Ts3Client ts3Client, string message) => ts3Client.SendServerMessage(message).UnwrapThrow();

		[Command("pm user")]
		public static void CommandPmUser(Ts3Client ts3Client, ushort clientId, string message) => ts3Client.SendMessage(message, (ClientId)clientId).UnwrapThrow();

		[Command("pause")]
		public static void CommandPause(Player playerConnection) => playerConnection.Paused = !playerConnection.Paused;

		[Command("play")]
		public static void CommandPlay(PlayManager playManager, Player playerConnection, InvokerData invoker)
		{
			if (!playManager.IsPlaying)
				playManager.Play(invoker).UnwrapThrow();
			else
				playerConnection.Paused = false;
		}

		[Command("play")]
		public static void CommandPlay(PlayManager playManager, InvokerData invoker, string url, params string[] attributes)
			=> playManager.Play(invoker, url, meta: PlayManager.ParseAttributes(attributes)).UnwrapThrow();

		[Command("play")]
		public static void CommandPlay(PlayManager playManager, InvokerData invoker, IAudioResourceResult rsc, params string[] attributes)
			=> playManager.Play(invoker, rsc.AudioResource, meta: PlayManager.ParseAttributes(attributes)).UnwrapThrow();

		[Command("plugin list")]
		public static JsonArray<PluginStatusInfo> CommandPluginList(PluginManager pluginManager, Bot bot = null)
			=> new JsonArray<PluginStatusInfo>(pluginManager.GetPluginOverview(bot), PluginManager.FormatOverview);

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

		[Command("quiz")]
		public static JsonValue<bool> CommandQuiz(Bot bot) => new JsonValue<bool>(bot.QuizMode, string.Format(strings.info_status_quizmode, bot.QuizMode ? strings.info_on : strings.info_off));
		[Command("quiz on")]
		public static void CommandQuizOn(Bot bot)
		{
			bot.QuizMode = true;
			bot.UpdateBotStatus().UnwrapThrow();
		}
		[Command("quiz off")]
		public static void CommandQuizOff(Bot bot, ClientCall invoker = null)
		{
			if (invoker != null && invoker.Visibiliy.HasValue && invoker.Visibiliy == TextMessageTargetMode.Private)
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
		public static JsonValue<LoopMode> CommandRepeat(PlaylistManager playlistManager)
			=> new JsonValue<LoopMode>(playlistManager.Loop, x =>
				x == LoopMode.Off ? strings.cmd_repeat_info_off :
				x == LoopMode.One ? strings.cmd_repeat_info_one :
				x == LoopMode.All ? strings.cmd_repeat_info_all : throw Tools.UnhandledDefault(playlistManager.Loop));
		[Command("repeat off")]
		public static void CommandRepeatOff(PlaylistManager playlistManager) => playlistManager.Loop = LoopMode.Off;
		[Command("repeat one")]
		public static void CommandRepeatOne(PlaylistManager playlistManager) => playlistManager.Loop = LoopMode.One;
		[Command("repeat all")]
		public static void CommandRepeatAll(PlaylistManager playlistManager) => playlistManager.Loop = LoopMode.All;

		[Command("rights can")]
		public static JsonArray<string> CommandRightsCan(ExecutionInformation info, RightsManager rightsManager, params string[] rights)
			=> new JsonArray<string>(rightsManager.GetRightsSubset(info, rights), r => r.Count > 0 ? string.Join(", ", r) : strings.info_empty);

		[Command("rights reload")]
		public static JsonEmpty CommandRightsReload(RightsManager rightsManager)
		{
			if (rightsManager.Reload())
				return new JsonEmpty(strings.info_ok);

			// TODO: this can be done nicer by returning the errors and warnings from parsing
			throw new CommandException(strings.cmd_rights_reload_error_parsing_file, CommandExceptionReason.CommandError);
		}

		[Command("rng")]
		[Usage("", "Gets a number between 0 and 100")]
		[Usage("<max>", "Gets a number between 0 and <max>")]
		[Usage("<min> <max>", "Gets a number between <min> and <max>")]
		public static int CommandRng(int? first = null, int? second = null)
		{
			if (first.HasValue && second.HasValue)
			{
				return Tools.Random.Next(Math.Min(first.Value, second.Value), Math.Max(first.Value, second.Value));
			}
			else if (first.HasValue)
			{
				if (first.Value <= 0)
					throw new CommandException(strings.cmd_rng_value_must_be_positive, CommandExceptionReason.CommandError);
				return Tools.Random.Next(first.Value);
			}
			else
			{
				return Tools.Random.Next(0, 100);
			}
		}

		[Command("seek")]
		[Usage("<sec>", "Time in seconds")]
		[Usage("<min:sec>", "Time in Minutes:Seconds")]
		[Usage("<0h0m0s>", "Time in hours, minutes and seconds")]
		public static void CommandSeek(Player playerConnection, TimeSpan position)
		{
			//if (!parsed)
			//	throw new CommandException(strings.cmd_seek_invalid_format, CommandExceptionReason.CommandError);
			if (position < TimeSpan.Zero || position > playerConnection.Length)
				throw new CommandException(strings.cmd_seek_out_of_range, CommandExceptionReason.CommandError);

			playerConnection.Position = position;
		}

		private static AudioResource GetSearchResult(this UserSession session, int index)
		{
			if (!session.Get<IList<AudioResource>>(SessionConst.SearchResult, out var sessionList))
				throw new CommandException(strings.error_select_empty, CommandExceptionReason.CommandError);

			if (index < 0 || index >= sessionList.Count)
				throw new CommandException(string.Format(strings.error_value_not_in_range, 0, sessionList.Count), CommandExceptionReason.CommandError);

			return sessionList[index];
		}

		[Command("search add", "_undocumented")] // TODO Doc
		public static void CommandSearchAdd(PlayManager playManager, InvokerData invoker, UserSession session, int index)
			=> playManager.Enqueue(invoker, session.GetSearchResult(index)).UnwrapThrow();

		[Command("search play", "_undocumented")] // TODO Doc
		public static void CommandSeachPlay(PlayManager playManager, ClientCall clientCall, UserSession session, int index)
			=> playManager.Play(clientCall, session.GetSearchResult(index)).UnwrapThrow();

		[Command("search get", "_undocumented")] // TODO Doc
		public static void CommandSearchGet(UserSession session, int index)
			=> session.GetSearchResult(index);

		[Command("server tree", "_undocumented")]
		public static JsonValue<Connection> CommandServerTree(Connection book, ApiCall _)
		{
			return JsonValue.Create(book);
		}

		[Command("settings")]
		public static void CommandSettings()
			=> throw new CommandException(string.Format(strings.cmd_settings_empty_usage, "'rights.path', 'web.api.enabled', 'tools.*'"), CommandExceptionReason.MissingParameter);

		[Command("settings copy")]
		public static void CommandSettingsCopy(ConfRoot config, string from, string to) => config.CopyBotConfig(from, to).UnwrapThrow();

		[Command("settings create")]
		public static void CommandSettingsCreate(ConfRoot config, string name) => config.CreateBotConfig(name).UnwrapThrow();

		[Command("settings delete")]
		public static void CommandSettingsDelete(ConfRoot config, string name) => config.DeleteBotConfig(name).UnwrapThrow();

		[Command("settings get")]
		public static ConfigPart CommandSettingsGet(ConfBot config, string path = null)
			=> SettingsGet(config, path);

		[Command("settings set")]
		public static void CommandSettingsSet(ConfBot config, string path, string value = null)
		{
			SettingsSet(config, path, value);
			if (!config.SaveWhenExists())
			{
				throw new CommandException("Value was set but could not be saved to file. All changes are temporary and will be lost when the bot restarts.",
					CommandExceptionReason.CommandError);
			}
		}

		[Command("settings bot get", "cmd_settings_get_help")]
		public static ConfigPart CommandSettingsBotGet(BotManager bots, ConfRoot config, string bot, string path = null)
		{
			using (var botlock = bots.GetBotLock(bot))
			{
				var confBot = GetConf(botlock?.Bot, config, bot);
				return CommandSettingsGet(confBot, path);
			}
		}

		[Command("settings bot set", "cmd_settings_set_help")]
		public static void CommandSettingsBotSet(BotManager bots, ConfRoot config, string bot, string path, string value = null)
		{
			using (var botlock = bots.GetBotLock(bot))
			{
				var confBot = GetConf(botlock?.Bot, config, bot);
				CommandSettingsSet(confBot, path, value);
			}
		}

		[Command("settings bot reload")]
		public static void CommandSettingsReload(ConfRoot config, string name = null)
		{
			if (string.IsNullOrEmpty(name))
				config.ClearBotConfigCache();
			else
				config.ClearBotConfigCache(name);
		}

		[Command("settings global get")]
		public static ConfigPart CommandSettingsGlobalGet(ConfRoot config, string path = null)
			=> SettingsGet(config, path);

		[Command("settings global set")]
		public static void CommandSettingsGlobalSet(ConfRoot config, string path, string value = null)
		{
			SettingsSet(config, path, value);
			if (!config.Save())
			{
				throw new CommandException("Value was set but could not be saved to file. All changes are temporary and will be lost when the bot restarts.",
					CommandExceptionReason.CommandError);
			}
		}

		//[Command("settings global reload")]
		public static void CommandSettingsGlobalReload(ConfRoot config)
		{
			// TODO
			throw new NotImplementedException();
		}

		private static ConfBot GetConf(Bot bot, ConfRoot config, string name)
		{
			if (bot != null)
			{
				if (bot.Injector.TryGet<ConfBot>(out var conf))
					return conf;
				else
					throw new CommandException(strings.error_call_unexpected_error, CommandExceptionReason.CommandError);
			}
			else
			{
				var getTemplateResult = config.GetBotConfig(name);
				if (!getTemplateResult.Ok)
					throw new CommandException(strings.error_bot_does_not_exist, getTemplateResult.Error, CommandExceptionReason.CommandError);
				return getTemplateResult.Value;
			}
		}

		private static ConfigPart SettingsGet(ConfigPart config, string path = null) => config.ByPathAsArray(path ?? "").SettingsGetSingle();

		private static void SettingsSet(ConfigPart config, string path, string value)
		{
			var setConfig = config.ByPathAsArray(path).SettingsGetSingle();
			if (setConfig is IJsonSerializable jsonConfig)
			{
				var result = jsonConfig.FromJson(value ?? "");
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
			var part = SettingsGet(config, path);
			return string.IsNullOrEmpty(part.Documentation) ? strings.info_empty : part.Documentation;
		}

		[Command("song")]
		public static JsonValue<SongInfo> CommandSong(PlayManager playManager, Player playerConnection, Bot bot, ClientCall invoker = null)
		{
			if (playManager.CurrentPlayData is null)
				throw new CommandException(strings.info_currently_not_playing, CommandExceptionReason.CommandError);
			if (bot.QuizMode && invoker != null && playManager.CurrentPlayData.Invoker.ClientUid != invoker.ClientUid)
				throw new CommandException(strings.info_quizmode_is_active, CommandExceptionReason.CommandError);

			return JsonValue.Create(
				new SongInfo
				{
					Title = playManager.CurrentPlayData.ResourceData.ResourceTitle,
					AudioType = playManager.CurrentPlayData.ResourceData.AudioType,
					Link = playManager.CurrentPlayData.SourceLink,
					Position = playerConnection.Position,
					Length = playerConnection.Length,
					Paused = playerConnection.Paused,
				},
				x =>
				{
					var tmb = new StringBuilder();
					tmb.Append(x.Paused ? "⏸ " : "► ");
					tmb.AppendFormat("[url={0}]{1}[/url]", x.Link, x.Title);
					tmb.Append(" [");
					tmb.Append(x.Length.TotalHours >= 1 || x.Position.TotalHours >= 1
						? $"{x.Position:hh\\:mm\\:ss}/{x.Length:hh\\:mm\\:ss}"
						: $"{x.Position:mm\\:ss}/{x.Length:mm\\:ss}");
					tmb.Append("]");
					return tmb.ToString();
				}
			);
		}

		[Command("stop")]
		public static void CommandStop(PlayManager playManager) => playManager.Stop();

		[Command("subscribe")]
		public static void CommandSubscribe(IVoiceTarget targetManager, ClientCall invoker)
		{
			if (invoker.ClientId.HasValue)
				targetManager.WhisperClientSubscribe(invoker.ClientId.Value);
		}

		[Command("subscribe tempchannel")]
		public static void CommandSubscribeTempChannel(IVoiceTarget targetManager, ClientCall invoker = null, ChannelId? channel = null)
		{
			var subChan = channel ?? invoker?.ChannelId ?? ChannelId.Null;
			if (subChan != ChannelId.Null)
				targetManager.WhisperChannelSubscribe(true, subChan);
		}

		[Command("subscribe channel")]
		public static void CommandSubscribeChannel(IVoiceTarget targetManager, ClientCall invoker = null, ChannelId? channel = null)
		{
			var subChan = channel ?? invoker?.ChannelId ?? ChannelId.Null;
			if (subChan != ChannelId.Null)
				targetManager.WhisperChannelSubscribe(false, subChan);
		}

		[Command("system info", "_undocumented")]
		public static JsonValue CommandSystemInfo(SystemMonitor systemMonitor)
		{
			var sysInfo = systemMonitor.GetReport();
			return JsonValue.Create(new
			{
				memory = sysInfo.Memory,
				cpu = sysInfo.Cpu,
				starttime = systemMonitor.StartTime,
			}, x => new TextModBuilder().AppendFormat(
				"\ncpu: {0}% \nmemory: {1} \nstartime: {2}".Mod().Bold(),
					(x.cpu.Last() * 100).ToString("0.#"),
					Util.FormatBytesHumanReadable(x.memory.Last()),
					x.starttime.ToString(Thread.CurrentThread.CurrentCulture)).ToString()
			);
		}

		[Command("system quit", "cmd_quit_help")]
		public static JsonEmpty CommandSystemQuit(Core core, CallerInfo caller, UserSession session = null, string param = null)
		{
			const string force = "force";

			if (caller.ApiCall || param == force)
			{
				core.Dispose();
				return new JsonEmpty(string.Empty);
			}

			string ResponseQuit(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					CommandSystemQuit(core, caller, session, force);
				}
				return null;
			}

			session.SetResponse(ResponseQuit);
			return new JsonEmpty(strings.cmd_quit_confirm + YesNoOption);
		}

		[Command("take")]
		[Usage("<count> <text>", "Take only <count> parts of the text")]
		[Usage("<count> <start> <text>", "Take <count> parts, starting with the part at <start>")]
		[Usage("<count> <start> <delimiter> <text>", "Specify another delimiter for the parts than spaces")]
		public static object CommandTake(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			if (arguments.Count < 2)
				throw new CommandException(strings.error_cmd_at_least_two_argument, CommandExceptionReason.MissingParameter);

			int start = 0;
			string delimiter = null;

			// Get count
			var res = ((IPrimitiveResult<string>)arguments[0].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Get();
			if (!int.TryParse(res, out int count) || count < 0)
				throw new CommandException("Count must be an integer >= 0", CommandExceptionReason.CommandError); // LOC: TODO

			if (arguments.Count > 2)
			{
				// Get start
				res = ((IPrimitiveResult<string>)arguments[1].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Get();
				if (!int.TryParse(res, out start) || start < 0)
					throw new CommandException("Start must be an integer >= 0", CommandExceptionReason.CommandError); // LOC: TODO
			}

			// Get delimiter if exists
			if (arguments.Count > 3)
				delimiter = ((IPrimitiveResult<string>)arguments[2].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Get();

			string text = ((IPrimitiveResult<string>)arguments[Math.Min(arguments.Count - 1, 3)]
				.Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Get();

			var splitted = delimiter is null
				? text.Split()
				: text.Split(new[] { delimiter }, StringSplitOptions.None);
			if (splitted.Length < start + count)
				throw new CommandException(strings.cmd_take_not_enough_arguements, CommandExceptionReason.CommandError);
			var splittedarr = splitted.Skip(start).Take(count).ToArray();

			foreach (var returnType in returnTypes)
			{
				if (returnType == typeof(string))
					return new PrimitiveResult<string>(string.Join(delimiter ?? " ", splittedarr));
			}

			throw new CommandException(strings.error_nothing_to_return, CommandExceptionReason.NoReturnMatch);
		}

		[Command("unsubscribe")]
		public static void CommandUnsubscribe(IVoiceTarget targetManager, ClientCall invoker)
		{
			if (invoker.ClientId.HasValue)
				targetManager.WhisperClientUnsubscribe(invoker.ClientId.Value);
		}

		[Command("unsubscribe channel")]
		public static void CommandUnsubscribeChannel(IVoiceTarget targetManager, ClientCall invoker = null, ulong? channel = null)
		{
			var subChan = (ChannelId?)channel ?? invoker?.ChannelId;
			if (subChan.HasValue)
				targetManager.WhisperChannelUnsubscribe(false, subChan.Value);
		}

		[Command("unsubscribe temporary")]
		public static void CommandUnsubscribeTemporary(IVoiceTarget targetManager) => targetManager.ClearTemporary();

		[Command("version")]
		public static JsonValue<BuildData> CommandVersion() => new JsonValue<BuildData>(SystemData.AssemblyData, d => d.ToLongString());

		[Command("volume")]
		public static JsonValue<float> CommandVolume(Player playerConnection)
			=> new JsonValue<float>(playerConnection.Volume, string.Format(strings.cmd_volume_current, playerConnection.Volume.ToString("0.#")));

		[Command("volume")]
		[Usage("<level>", "A new volume level between 0 and 100.")]
		[Usage("+/-<level>", "Adds or subtracts a value from the current volume.")]
		public static JsonValue<float> CommandVolume(ExecutionInformation info, Player playerConnection, CallerInfo caller, ConfBot config, string volume, UserSession session = null)
		{
			volume = volume.Trim();
			bool relPos = volume.StartsWith("+", StringComparison.Ordinal);
			bool relNeg = volume.StartsWith("-", StringComparison.Ordinal);
			string numberString = (relPos || relNeg) ? volume.Remove(0, 1).TrimStart() : volume;

			if (!float.TryParse(numberString, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedVolume))
				throw new CommandException(strings.cmd_volume_parse_error, CommandExceptionReason.CommandError);

			float curVolume = playerConnection.Volume;
			float newVolume;
			if (relPos) newVolume = curVolume + parsedVolume;
			else if (relNeg) newVolume = curVolume - parsedVolume;
			else newVolume = parsedVolume;

			if (newVolume < AudioValues.MinVolume || newVolume > AudioValues.MaxVolume)
				throw new CommandException(string.Format(strings.cmd_volume_is_limited, AudioValues.MinVolume, AudioValues.MaxVolume), CommandExceptionReason.CommandError);

			if (newVolume <= config.Audio.MaxUserVolume || newVolume <= curVolume || caller.ApiCall)
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
#pragma warning disable IDE0037
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
#pragma warning restore IDE0037
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

		//private static string GetEditPlaylist(this UserSession session)
		//{
		//	if (session is null)
		//		throw new MissingContextCommandException(strings.error_no_session_in_context, typeof(UserSession));
		//	var result = session.Get<string>(SessionConst.Playlist);
		//	if (result)
		//		return result.Value;

		//	throw new CommandException("You are currently not editing any playlist.", CommandExceptionReason.CommandError); // TODO: Loc
		//}

		public static bool HasRights(this ExecutionInformation info, params string[] rights)
		{
			if (!info.TryGet<CallerInfo>(out var caller)) caller = null;
			if (caller?.SkipRightsChecks ?? false)
				return true;
			if (!info.TryGet<RightsManager>(out var rightsManager))
				return false;
			return rightsManager.HasAllRights(info, rights);
		}

		public static E<LocalStr> Write(this ExecutionInformation info, string message)
		{
			if (!info.TryGet<Ts3Client>(out var ts3Client))
				return new LocalStr(strings.error_no_teamspeak_in_context);

			if (!info.TryGet<ClientCall>(out var invoker))
				return new LocalStr(strings.error_no_invoker_in_context);

			if (!invoker.Visibiliy.HasValue || !invoker.ClientId.HasValue)
				return new LocalStr(strings.error_invoker_not_visible);

			var behaviour = LongTextBehaviour.Split;
			var limit = 1;
			if (info.TryGet<ConfBot>(out var config))
			{
				behaviour = config.Commands.LongMessage;
				limit = config.Commands.LongMessageSplitLimit;
			}

			foreach (var msgPart in LongTextTransform.Transform(message, behaviour, limit))
			{
				E<LocalStr> result;
				switch (invoker.Visibiliy.Value)
				{
				case TextMessageTargetMode.Private:
					result = ts3Client.SendMessage(msgPart, invoker.ClientId.Value);
					break;
				case TextMessageTargetMode.Channel:
					result = ts3Client.SendChannelMessage(msgPart);
					break;
				case TextMessageTargetMode.Server:
					result = ts3Client.SendServerMessage(msgPart);
					break;
				default:
					throw Tools.UnhandledDefault(invoker.Visibiliy.Value);
				}

				if (!result.Ok)
					return result;
			}
			return R.Ok;
		}

		public static void UseComplexityTokens(this ExecutionInformation info, int count)
		{
			if (!info.TryGet<CallerInfo>(out var caller) || caller.CommandComplexityCurrent + count > caller.CommandComplexityMax)
				throw new CommandException(strings.error_cmd_complexity_reached, CommandExceptionReason.CommandError);
			caller.CommandComplexityCurrent += count;
		}
	}
}
