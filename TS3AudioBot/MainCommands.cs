// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using CliWrap;
using CliWrap.Buffered;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Algorithm;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.CommandSystem.Ast;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.CommandSystem.Text;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Environment;
using TS3AudioBot.Helper;
using TS3AudioBot.Helper.Diagnose;
using TS3AudioBot.History;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.Plugins;
using TS3AudioBot.ResourceFactories;
using TS3AudioBot.Rights;
using TS3AudioBot.Sessions;
using TS3AudioBot.Web;
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
		public static async Task CommandAdd(PlayManager playManager, InvokerData invoker, string url, params string[] attributes)
			=> await playManager.Enqueue(invoker, url, meta: PlayManager.ParseAttributes(attributes));

		[Command("add")]
		public static async Task CommandAdd(PlayManager playManager, InvokerData invoker, IAudioResourceResult rsc, params string[] attributes)
			=> await playManager.Enqueue(invoker, rsc.AudioResource, meta: PlayManager.ParseAttributes(attributes));

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
		public static string? CommandAliasShow(CommandManager commandManager, string commandName)
			=> commandManager.GetAlias(commandName)?.AliasString;

		[Command("api token")]
		[Usage("[<duration>]", "Optionally specifies a duration this key is valid in hours.")]
		public static string CommandApiToken(TokenManager tokenManager, ClientCall invoker, double? validHours = null)
		{
			if (invoker.Visibiliy != null && invoker.Visibiliy != TextMessageTargetMode.Private)
				throw new CommandException(strings.error_use_private, CommandExceptionReason.CommandError);
			if (invoker.IsAnonymous || invoker.ClientUid == Uid.Null)
				throw new MissingContextCommandException(strings.error_no_uid_found, typeof(ClientCall));

			TimeSpan? validSpan = null;
			try
			{
				if (validHours != null)
					validSpan = TimeSpan.FromHours(validHours.Value);
			}
			catch (OverflowException oex)
			{
				throw new CommandException(strings.error_invalid_token_duration, oex, CommandExceptionReason.CommandError);
			}
			return tokenManager.GenerateToken(invoker.ClientUid.Value!, validSpan);
		}

		[Command("bot avatar set")]
		public static async Task CommandBotAvatarSet(Ts3Client ts3Client, string url)
		{
			url = TextUtil.ExtractUrlFromBb(url);
			await WebWrapper.Request(url).ToAction(async x =>
			{
				using var stream = await x.Content.ReadAsStreamAsync();
				using var image = await ImageUtil.ResizeImageSave(stream);
				await ts3Client.UploadAvatar(image.Stream);
			});
		}

		[Command("bot avatar clear")]
		public static Task CommandBotAvatarClear(Ts3Client ts3Client) => ts3Client.DeleteAvatar();

		[Command("bot badges")]
		public static Task CommandBotBadges(Ts3Client ts3Client, string badges) => ts3Client.ChangeBadges(badges);

		[Command("bot description set")]
		public static Task CommandBotDescriptionSet(Ts3Client ts3Client, string description) => ts3Client.ChangeDescription(description);

		[Command("bot diagnose", "_undocumented")]
		public static async Task<JsonArray<SelfDiagnoseMessage>> CommandBotDiagnose(Player player, IVoiceTarget target, Connection book, ConfRoot rootConf, WebServer webServer)
		{
			var problems = new List<SelfDiagnoseMessage>();
			// ** Diagnose common playback problems and more **

			var self = book.Self();
			var curChan = book.CurrentChannel();

			// Check talk power
			if (self != null && curChan != null && !self.TalkPowerGranted && self.TalkPower < curChan.NeededTalkPower)
				problems.Add(new SelfDiagnoseMessage("The bot does not have enough talk power.", "play", SelfDiagnoseLevel.Warning));

			// Check volume 0
			if (player.Volume == 0)
				problems.Add(new SelfDiagnoseMessage("The volume level is at 0.", "play", SelfDiagnoseLevel.Warning));

			// Check if send mode hasn't been selected yet
			if (target.SendMode == TargetSendMode.None)
				problems.Add(new SelfDiagnoseMessage("Send mode is currently 'None', use '!whisper off' for example to send via voice.", "play", SelfDiagnoseLevel.Warning));

			// - Check if ffmpeg exists
			// - Check ffmpeg https support (https://gitter.im/TS3AudioBot/Lobby?at=5eaf1e14f0377f1631656b7a)
			//   Seems like CentOS 7 for e.g. by default has no https
			try
			{
				var ffPath = rootConf.Tools.Ffmpeg.Path.Value;
				var result = await Cli.Wrap(ffPath)
					.WithArguments(new[] { "-hide_banner", "-protocols" })
					.ExecuteBufferedAsync();
				var protos = new HashSet<string>(result.StandardOutput
					.Split('\n')
					.Select(x => x.Trim())
					.SkipWhile(x => x != "Input:").Skip(1)
					.TakeWhile(x => x != "Output:"));
				foreach (var wantProto in new[] { "http", "https", "hls" })
				{
					if (!protos.Contains(wantProto))
						problems.Add(new SelfDiagnoseMessage($"Your ffmpeg binary is missing '{wantProto}'. Some streams might not play.", "play", SelfDiagnoseLevel.Warning));
				}
			}
			catch (Exception ex)
			{
				problems.Add(new SelfDiagnoseMessage($"Could not find or run ffmpeg binary. Playback will NOT work. ({ex.Message})", "play", SelfDiagnoseLevel.Error));
			}

			// Check if web path is found
			{
				if (!rootConf.Web.Interface.Enabled)
					problems.Add(new SelfDiagnoseMessage($"WebInterface is disabled.", "web", SelfDiagnoseLevel.Info));

				var webPath = webServer.FindWebFolder();
				if (rootConf.Web.Interface.Enabled &&
					(webPath is null || !Directory.Exists(webPath) || !System.IO.File.Exists(Path.Combine(webPath, "index.html"))))
					problems.Add(new SelfDiagnoseMessage($"WebInterface is enabled, but the required files are missing.", "web", SelfDiagnoseLevel.Error));
			}

			return new JsonArray<SelfDiagnoseMessage>(problems, x =>
			{
				if (x.Count == 0)
					return "No problems detected";
				var strb = new StringBuilder("The following issues have been found:");
				foreach (var prob in x)
					strb.Append("\n- ").Append(prob.Level).Append(": ").Append(prob.Description);
				return strb.ToString();
			});
		}

		[Command("bot disconnect")]
		public static async Task CommandBotDisconnect(Bot bot) => await bot.Stop();

		[Command("bot commander")]
		public static async Task<JsonValue<bool>> CommandBotCommander(Ts3Client ts3Client)
		{
			var value = await ts3Client.IsChannelCommander();
			return new JsonValue<bool>(value, string.Format(strings.info_status_channelcommander, value ? strings.info_on : strings.info_off));
		}
		[Command("bot commander on")]
		public static Task CommandBotCommanderOn(Ts3Client ts3Client) => ts3Client.SetChannelCommander(true);
		[Command("bot commander off")]
		public static Task CommandBotCommanderOff(Ts3Client ts3Client) => ts3Client.SetChannelCommander(false);

		[Command("bot come")]
		public static Task CommandBotCome(Ts3Client ts3Client, ClientCall invoker, string? password = null)
		{
			var channel = invoker?.ChannelId;
			if (channel == null)
				throw new CommandException(strings.error_no_target_channel, CommandExceptionReason.CommandError);
			return ts3Client.MoveTo(channel.Value, password);
		}

		[Command("bot connect template")]
		public static async Task<BotInfo> CommandBotConnectTo(BotManager bots, string name)
		{
			var botInfo = await bots.RunBotTemplate(name);
			if (!botInfo.Ok)
				throw new CommandException(strings.error_could_not_create_bot + $" ({botInfo.Error})", CommandExceptionReason.CommandError);
			return botInfo.Value;
		}

		[Command("bot connect to")]
		public static async Task<BotInfo> CommandBotConnectNew(BotManager bots, string address, string? password = null)
		{
			var botConf = bots.CreateNewBot();
			botConf.Connect.Address.Value = address;
			if (!string.IsNullOrEmpty(password))
				botConf.Connect.ServerPassword.Password.Value = password;
			var botInfo = await bots.RunBot(botConf);
			if (!botInfo.Ok)
				throw new CommandException(strings.error_could_not_create_bot + $" ({botInfo.Error})", CommandExceptionReason.CommandError);
			return botInfo.Value;
		}

		[Command("bot info")]
		public static BotInfo CommandBotInfo(Bot bot) => bot.GetInfo();

		[Command("bot info client", "_undocumented")]
		public static Client? CommandBotInfoClient(Connection book, ApiCall _) => book.Self();

		[Command("bot info template", "cmd_bot_info_help")]
		public static BotInfo CommandBotInfo(BotManager botManager, ConfRoot config, string name)
		{
			var bot = botManager.GetBotLock(name);
			if (bot != null)
				return CommandBotInfo(bot);
			var botInfo = GetOfflineBotInfo(config, name).UnwrapThrow();
			return botInfo;
		}

		[Command("bot list")]
		public static JsonArray<BotInfo> CommandBotList(BotManager bots, ConfRoot config)
		{
			var botInfoList = bots.GetBotInfolist();
			var botConfigList = config.GetAllBots() ?? Array.Empty<ConfBot>();
			var infoList = new Dictionary<string, BotInfo>();
			foreach (var botInfo in botInfoList)
			{
				if (string.IsNullOrEmpty(botInfo.Name))
					continue;
				infoList[botInfo.Name] = botInfo;
			}
			foreach (var botConfig in botConfigList)
			{
				var name = botConfig.Name;
				if (name is null || infoList.ContainsKey(name))
					continue;
				infoList[name] = GetOfflineBotInfo(botConfig);
			}
			return new JsonArray<BotInfo>(infoList.Values.Concat(botInfoList.Where(x => string.IsNullOrEmpty(x.Name))).ToArray(),
				bl => string.Join("\n", bl.Select(x => x.ToString())));
		}

		private static R<BotInfo, LocalStr> GetOfflineBotInfo(ConfRoot config, string name)
		{
			var result = config.GetBotConfig(name);
			if (!result.Ok)
				return new LocalStr(result.Error.Message);
			var botConfig = result.Value;
			return GetOfflineBotInfo(botConfig);
		}

		private static BotInfo GetOfflineBotInfo(ConfBot botConfig)
		{
			return new BotInfo
			{
				Id = null,
				Name = botConfig.Name,
				Server = botConfig.Connect.Address,
				Status = BotStatus.Offline,
			};
		}

		[Command("bot move")]
		public static Task CommandBotMove(Ts3Client ts3Client, ulong channel, string? password = null) => ts3Client.MoveTo((ChannelId)channel, password);

		[Command("bot name")]
		public static Task CommandBotName(Ts3Client ts3Client, string name) => ts3Client.ChangeName(name);

		[Command("bot save")]
		public static void CommandBotSetup(ConfBot botConfig, string name) => botConfig.SaveNew(name).UnwrapThrow();

		[Command("bot setup")]
		public static async Task CommandBotSetup(Ts3Client ts3Client, string? adminToken = null)
		{
			await ts3Client.SetupRights(adminToken);
		}

		[Command("bot template", "cmd_bot_use_help")]
		public static async Task<object?> CommandBotTemplate(ExecutionInformation info, BotManager bots, string botName, ICommand cmd)
		{
			var bot = bots.GetBotLock(botName);
			return await CommandBotUseInternal(info, bot, cmd);
		}

		[Command("bot use")]
		public static async Task<object?> CommandBotUse(ExecutionInformation info, BotManager bots, int botId, ICommand cmd)
		{
			var bot = bots.GetBotLock(botId);
			return await CommandBotUseInternal(info, bot, cmd);
		}

		private static async Task<object?> CommandBotUseInternal(ExecutionInformation info, Bot? bot, ICommand cmd)
		{
			if (bot is null)
				throw new CommandException(strings.error_bot_does_not_exist, CommandExceptionReason.CommandError);

			var backParent = info.ParentInjector;
			info.ParentInjector = bot.Injector;
			try
			{
				return await bot.Scheduler.InvokeAsync(() => cmd.Execute(info, Array.Empty<ICommand>()).AsTask());
			}
			finally
			{
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
			return CommandManager.GetTree(commandManager.RootGroup);
		}

		[Command("data song cover get", "_undocumented")]
		public static DataStream CommandData(ResolveContext resourceFactory, PlayManager playManager) =>
			new DataStream(async response =>
			{
				var cur = playManager.CurrentPlayData;
				if (cur is null)
					throw Error.LocalStr(strings.info_currently_not_playing);
				await resourceFactory.GetThumbnail(cur.PlayResource, async stream =>
				{
					using var image = await ImageUtil.ResizeImageSave(stream);
					response.ContentType = image.Mime;
					await image.Stream.CopyToAsync(response.Body);
				});
			});

		[Command("eval")]
		[Usage("<command> <arguments...>", "Executes the given command on arguments")]
		[Usage("<strings...>", "Concat the strings and execute them with the command system")]
		public static async Task<object?> CommandEval(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			// Evaluate the first argument on the rest of the arguments
			if (arguments.Count == 0)
				throw new CommandException(strings.error_cmd_at_least_one_argument, CommandExceptionReason.MissingParameter);
			var leftArguments = arguments.TrySegment(1);
			var arg0 = await arguments[0].Execute(info, Array.Empty<ICommand>());
			switch (arg0)
			{
			case ICommand command:
				return await command.Execute(info, leftArguments);
			case null:
				return null;
			default:
				var cmdStr = arg0.ToString()!;
				// We got a string back so parse and evaluate it
				var cmd = CommandManager.AstToCommandResult(CommandParser.ParseCommandRequest(cmdStr));
				return await cmd.Execute(info, leftArguments);
			}
		}

		[Command("from", "_undocumented")]
		public static async Task CommandFrom(PlayManager playManager, InvokerData invoker, string factoryName, string url)
			=> await playManager.Play(invoker, url, factoryName);

		[Command("get", "_undocumented")]
		[Usage("<index> <list...>", "Get an element out of a list")]
		public static object? CommandGet(uint index, System.Collections.IEnumerable list)
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
		public static string? CommandGetUid(ClientCall invoker)
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
		public static async Task<string> CommandGetUidById(Ts3Client ts3Client, ushort id) => (await ts3Client.GetFallbackedClientById((ClientId)id)).Uid?.Value ?? "";
		[Command("getuser name byid")]
		public static async Task<string> CommandGetNameById(Ts3Client ts3Client, ushort id) => (await ts3Client.GetFallbackedClientById((ClientId)id)).Name;
		[Command("getuser dbid byid")]
		public static async Task<ulong> CommandGetDbIdById(Ts3Client ts3Client, ushort id) => (await ts3Client.GetFallbackedClientById((ClientId)id)).DatabaseId.Value;
		[Command("getuser channel byid")]
		public static async Task<ulong> CommandGetChannelById(Ts3Client ts3Client, ushort id) => (await ts3Client.GetFallbackedClientById((ClientId)id)).ChannelId.Value;
		[Command("getuser all byid")]
		public static async Task<JsonValue<ClientList>> CommandGetUserById(Ts3Client ts3Client, ushort id)
		{
			var client = await ts3Client.GetFallbackedClientById((ClientId)id);
			return new JsonValue<ClientList>(client, $"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}");
		}
		[Command("getuser id byname")]
		public static async Task<ushort> CommandGetIdByName(Ts3Client ts3Client, string username) => (await ts3Client.GetClientByName(username)).ClientId.Value;
		[Command("getuser all byname")]
		public static async Task<JsonValue<ClientList>> CommandGetUserByName(Ts3Client ts3Client, string username)
		{
			var client = await ts3Client.GetClientByName(username);
			return new JsonValue<ClientList>(client, $"Client: Id:{client.ClientId} DbId:{client.DatabaseId} ChanId:{client.ChannelId} Uid:{client.Uid}");
		}
		[Command("getuser name bydbid")]
		public static async Task<string> CommandGetNameByDbId(Ts3Client ts3Client, ulong dbId) => (await ts3Client.GetDbClientByDbId((ClientDbId)dbId)).Name;
		[Command("getuser uid bydbid")]
		public static async Task<string?> CommandGetUidByDbId(Ts3Client ts3Client, ulong dbId) => (await ts3Client.GetDbClientByDbId((ClientDbId)dbId)).Uid.Value;

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
			var botComList = commandManager.RootGroup.Commands.Select(c => c.Key).ToArray();
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
		public static JsonObject CommandHelpCommand(CommandManager commandManager, IFilter? filter = null, params string[] command)
		{
			if (command.Length == 0)
			{
				return new JsonEmpty(strings.error_cmd_at_least_one_argument);
			}

			CommandGroup? group = commandManager.RootGroup;
			ICommand? target = group;
			filter ??= Filter.DefaultFilter;
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
				return JsonValue.Create(targetB);
			case CommandGroup targetCg:
				var subList = targetCg.Commands.Select(g => g.Key).ToArray();
				return new JsonArray<string>(subList, string.Format(strings.cmd_help_info_contains_subfunctions, string.Join(", ", subList)));
			case OverloadedFunctionCommand targetOfc:
				var strb = new StringBuilder();
				foreach (var botCom in targetOfc.Functions.OfType<BotCommand>())
					strb.Append(botCom);
				return JsonValue.Create(strb.ToString());
			case AliasCommand targetAlias:
				return JsonValue.Create(string.Format("'{0}' is an alias for:\n{1}", string.Join(" ", realPath), targetAlias.AliasString));
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
		public static async Task CommandHistoryQueue(HistoryManager historyManager, PlayManager playManager, InvokerData invoker, uint hid)
		{
			var ale = historyManager.GetEntryById(hid).UnwrapThrow();
			await playManager.Enqueue(invoker, ale.AudioResource);
		}

		[Command("history clean")]
		public static JsonEmpty CommandHistoryClean(DbStore database, CallerInfo caller, UserSession? session = null)
		{
			if (caller.ApiCall)
			{
				database.CleanFile();
				return new JsonEmpty(string.Empty);
			}

			Task<string?> ResponseHistoryClean(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					database.CleanFile();
					return Task.FromResult<string?>(strings.info_cleanup_done);
				}
				return Task.FromResult<string?>(null);
			}
			session.SetResponse(ResponseHistoryClean);
			return new JsonEmpty($"{strings.cmd_history_clean_confirm_clean} {strings.info_bot_might_be_unresponsive} {YesNoOption}");
		}

		[Command("history clean removedefective")]
		public static async Task<JsonEmpty> CommandHistoryCleanRemove(HistoryManager historyManager, ResolveContext resourceFactory, CallerInfo caller, UserSession? session = null)
		{
			if (caller.ApiCall)
			{
				await historyManager.RemoveBrokenLinks(resourceFactory);
				return new JsonEmpty(string.Empty);
			}

			async Task<string?> ResponseHistoryCleanRemove(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					await historyManager.RemoveBrokenLinks(resourceFactory);
					return strings.info_cleanup_done;
				}
				return null;
			}
			session.SetResponse(ResponseHistoryCleanRemove);
			return new JsonEmpty($"{strings.cmd_history_clean_removedefective_confirm_clean} {strings.info_bot_might_be_unresponsive} {YesNoOption}");
		}

		[Command("history clean upgrade", "_undocumented")]
		public static async Task CommandHistoryCleanUpgrade(HistoryManager historyManager, Ts3Client ts3Client)
		{
			await historyManager.UpdadeDbIdToUid(ts3Client);
		}

		[Command("history delete")]
		public static JsonEmpty CommandHistoryDelete(HistoryManager historyManager, CallerInfo caller, uint id, UserSession? session = null)
		{
			var ale = historyManager.GetEntryById(id).UnwrapThrow();

			if (caller.ApiCall)
			{
				historyManager.RemoveEntry(ale);
				return new JsonEmpty(string.Empty);
			}

			Task<string?> ResponseHistoryDelete(string message)
			{
				Answer answer = TextUtil.GetAnswer(message);
				if (answer == Answer.Yes)
				{
					historyManager.RemoveEntry(ale);
				}
				return Task.FromResult<string?>(null);
			}

			session.SetResponse(ResponseHistoryDelete);
			var name = ale.AudioResource.ResourceTitle;
			if (name?.Length > 100)
				name = name.Substring(100) + "...";
			return new JsonEmpty(string.Format(strings.cmd_history_delete_confirm + YesNoOption, name, id));
		}

		[Command("history from")]
		public static JsonArray<AudioLogEntry> CommandHistoryFrom(HistoryManager historyManager, string userUid, int? amount = null)
		{
			var query = new SeachQuery { UserUid = userUid };
			if (amount != null)
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
		public static async Task CommandHistoryLast(HistoryManager historyManager, PlayManager playManager, InvokerData invoker)
		{
			var ale = historyManager.Search(new SeachQuery { MaxResults = 1 }).FirstOrDefault();
			if (ale is null)
				throw new CommandException(strings.cmd_history_last_is_empty, CommandExceptionReason.CommandError);
			await playManager.Play(invoker, ale.AudioResource);
		}

		[Command("history play")]
		public static async Task CommandHistoryPlay(HistoryManager historyManager, PlayManager playManager, InvokerData invoker, uint hid)
		{
			var ale = historyManager.GetEntryById(hid).UnwrapThrow();
			await playManager.Play(invoker, ale.AudioResource);
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
			var tillTime = (time.ToLowerInvariant()) switch
			{
				"hour" => DateTime.Now.AddHours(-1),
				"today" => DateTime.Today,
				"yesterday" => DateTime.Today.AddDays(-1),
				"week" => DateTime.Today.AddDays(-7),
				_ => throw new CommandException(strings.error_unrecognized_descriptor, CommandExceptionReason.CommandError),
			};
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
		public static async Task<object?> CommandIf(ExecutionInformation info, string arg0, string cmp, string arg1, ICommand then, ICommand? other = null)
		{
			Func<double, double, bool> comparer = cmp switch
			{
				"<" => (a, b) => a < b,
				">" => (a, b) => a > b,
				"<=" => (a, b) => a <= b,
				">=" => (a, b) => a >= b,
				"==" => (a, b) => Math.Abs(a - b) < 1e-6,
				"!=" => (a, b) => Math.Abs(a - b) > 1e-6,
				_ => throw new CommandException(strings.cmd_if_unknown_operator, CommandExceptionReason.CommandError),
			};
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
				return await then.Execute(info, Array.Empty<ICommand>());
			// Else branch
			if (other != null)
				return await other.Execute(info, Array.Empty<ICommand>());

			return null;
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
				var subOffset = expression.AsSpan(1).Trim();
				if (subOffset.IsEmpty)
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
		public static JsonValue<QueueInfo> CommandInfo(ResolveContext resourceFactory, PlaylistManager playlistManager, string? offset = null, int? count = null)
			=> CommandInfo(resourceFactory, playlistManager, GetIndexExpression(playlistManager, offset ?? "@-1"), count);

		[Command("info")]
		public static JsonValue<QueueInfo> CommandInfo(ResolveContext resourceFactory, PlaylistManager playlistManager, int offset, int? count = null)
		{
			const int maxSongs = 20;
			var playIndex = playlistManager.Index;
			var plist = playlistManager.CurrentList;
			int offsetV = Tools.Clamp(offset, 0, plist.Items.Count);
			int countV = Tools.Clamp(count ?? 3, 0, Math.Min(maxSongs, plist.Items.Count - offsetV));
			var items = plist.Items.Skip(offsetV).Take(countV).Select(x => resourceFactory.ToApiFormat(x)).ToArray();

			var plInfo = new QueueInfo(".mix", plist.Title)
			{
				SongCount = plist.Items.Count,
				DisplayOffset = offsetV,
				Items = items,
				PlaybackIndex = playIndex,
			};

			return JsonValue.Create(plInfo, x =>
			{
				if (x.SongCount == 0 || x.Items is null)
					return strings.info_currently_not_playing;

				var tmb = new TextModBuilder();
				tmb.AppendFormat(strings.cmd_list_show_header, x.Title.Mod().Bold(), x.SongCount.ToString()).Append("\n");

				for (int i = 0; i < x.Items.Length; i++)
				{
					var line = $"{x.DisplayOffset + i}: {x.Items[i].Title}";
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
		public static async Task<JsonArray<object?>> CommandJsonMerge(ExecutionInformation info, ApiCall _, IReadOnlyList<ICommand> arguments)
		{
			if (arguments.Count == 0)
				return new JsonArray<object?>(Array.Empty<object>(), string.Empty);

			var jsonArr = new object?[arguments.Count];
			for (int i = 0; i < arguments.Count; i++)
			{
				object? res;
				try { res = await arguments[i].Execute(info, Array.Empty<ICommand>()); }
				catch (AudioBotException) { continue; }
				if (res is JsonObject o)
					jsonArr[i] = o.GetSerializeObject();
				else
					jsonArr[i] = res;
			}

			return new JsonArray<object?>(jsonArr, string.Empty);
		}

		[Command("json api", "_undocumented")]
		public static JsonObject CommandJsonApi(CommandManager commandManager, ApiCall _, BotManager? botManager = null)
		{
			var bots = botManager?.GetBotInfolist() ?? Array.Empty<BotInfo>();
			var api = OpenApiGenerator.Generate(commandManager, bots);
			return new JsonValue<JObject>(api, string.Empty);
		}

		[Command("jump")]
		public static async Task CommandJump(PlayManager playManager, PlaylistManager playlistManager, InvokerData invoker, string offset)
		{
			playlistManager.Index = GetIndexExpression(playlistManager, offset);
			await playManager.Play(invoker);
		}

		[Command("kickme")]
		public static Task CommandKickme(Ts3Client ts3Client, ClientCall invoker)
			=> CommandKickme(ts3Client, invoker, false);

		[Command("kickme far", "cmd_kickme_help")]
		public static Task CommandKickmeFar(Ts3Client ts3Client, ClientCall invoker)
			=> CommandKickme(ts3Client, invoker, true);

		private static async Task CommandKickme(Ts3Client ts3Client, ClientCall invoker, bool far)
		{
			if (invoker.ClientId is null)
				return;

			try
			{
				if (far) await ts3Client.KickClientFromServer(invoker.ClientId.Value);
				else await ts3Client.KickClientFromChannel(invoker.ClientId.Value);
			}
			catch (Exception ex) { throw new CommandException(strings.cmd_kickme_missing_permission, ex, CommandExceptionReason.CommandError); }
		}

		[Command("list add")]
		public static async Task<JsonValue<PlaylistItemGetData>> CommandListAdd(ResolveContext resourceFactory, PlaylistManager playlistManager, string listId, string link /* TODO param */)
		{
			PlaylistItemGetData? getData = null;
			var playResource = await resourceFactory.Load(link);
			playlistManager.ModifyPlaylist(listId, plist =>
			{
				var item = PlaylistItem.From(playResource);
				plist.Add(item).UnwrapThrow();
				getData = resourceFactory.ToApiFormat(item);
				//getData.Index = plist.Items.Count - 1;
			}).UnwrapThrow();
			return JsonValue.Create(getData!, strings.info_ok);
		}

		[Command("list clear")]
		public static void CommandListClear(PlaylistManager playlistManager, string listId)
			=> playlistManager.ModifyPlaylist(listId, plist => plist.Clear()).UnwrapThrow();

		[Command("list create", "_undocumented")]
		public static void CommandListCreate(PlaylistManager playlistManager, string listId, string? title = null)
			=> playlistManager.CreatePlaylist(listId, title ?? listId).UnwrapThrow();

		[Command("list delete")]
		public static JsonEmpty CommandListDelete(PlaylistManager playlistManager, UserSession session, string listId)
		{
			Task<string?> ResponseListDelete(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					playlistManager.DeletePlaylist(listId).UnwrapThrow();
				}
				return Task.FromResult<string?>(null);
			}

			session.SetResponse(ResponseListDelete);
			return new JsonEmpty(string.Format(strings.cmd_list_delete_confirm + YesNoOption, listId));
		}

		[Command("list delete")]
		public static void CommandListDelete(PlaylistManager playlistManager, ApiCall _, string listId)
			=> playlistManager.DeletePlaylist(listId).UnwrapThrow();

		[Command("list from", "_undocumented")]
		public static async Task<JsonValue<PlaylistInfo>> PropagiateLoad(PlaylistManager playlistManager, ResolveContext resolver, string resolverName, string listId, string url)
		{
			var getList = await resolver.LoadPlaylistFrom(url, resolverName);
			return ImportMerge(playlistManager, resolver, getList, listId);
		}

		[Command("list import", "cmd_list_get_help")] // TODO readjust help texts
		public static async Task<JsonValue<PlaylistInfo>> CommandListImport(PlaylistManager playlistManager, ResolveContext resolver, string listId, string link)
		{
			var getList = await resolver.LoadPlaylistFrom(link);
			return ImportMerge(playlistManager, resolver, getList, listId);
		}

		private static JsonValue<PlaylistInfo> ImportMerge(PlaylistManager playlistManager, ResolveContext resolver, Playlist addList, string listId)
		{
			if (!playlistManager.ExistsPlaylist(listId))
				playlistManager.CreatePlaylist(listId).UnwrapThrow();

			playlistManager.ModifyPlaylist(listId, playlist =>
			{
				playlist.AddRange(addList.Items).UnwrapThrow();
			}).UnwrapThrow();

			return CommandListShow(playlistManager, resolver, listId, null, null);
		}

		[Command("list insert", "_undocumented")]  // TODO Doc
		public static async Task<JsonValue<PlaylistItemGetData>> CommandListAdd(PlaylistManager playlistManager, ResolveContext resourceFactory, string listId, int index, string link /* TODO param */)
		{
			PlaylistItemGetData? getData = null;
			var playResource = await resourceFactory.Load(link);
			playlistManager.ModifyPlaylist(listId, plist =>
			{
				if (index < 0 || index >= plist.Items.Count)
					throw new CommandException(strings.error_playlist_item_index_out_of_range, CommandExceptionReason.CommandError);

				var item = PlaylistItem.From(playResource);
				plist.Insert(index, item).UnwrapThrow();
				getData = resourceFactory.ToApiFormat(item);
				//getData.Index = plist.Items.Count - 1;
			}).UnwrapThrow();
			return JsonValue.Create(getData!, strings.info_ok);
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
			PlaylistItem? deletedItem = null;
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
		public static JsonArray<PlaylistInfo> CommandListList(PlaylistManager playlistManager, string? pattern = null)
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
		public static async Task CommandListPlayInternal(PlaylistManager playlistManager, PlayManager playManager, InvokerData invoker, string listId, int? index = null)
		{
			var plist = playlistManager.LoadPlaylist(listId).UnwrapThrow();

			if (plist.Items.Count == 0)
				throw new CommandException(strings.error_playlist_is_empty);

			if (index != null && (index.Value < 0 || index.Value >= plist.Items.Count))
				throw new CommandException(strings.error_playlist_item_index_out_of_range);

			await playManager.Play(invoker, plist.Items, index ?? 0);
		}

		[Command("list queue")]
		public static async Task CommandListQueue(PlaylistManager playlistManager, PlayManager playManager, InvokerData invoker, string listId)
		{
			var plist = playlistManager.LoadPlaylist(listId).UnwrapThrow();
			await playManager.Enqueue(invoker, plist.Items);
		}

		[Command("list show")]
		[Usage("<name> <index>", "Lets you specify the starting index from which songs should be listed.")]
		public static JsonValue<PlaylistInfo> CommandListShow(PlaylistManager playlistManager, ResolveContext resourceFactory, string listId, int? offset = null, int? count = null)
		{
			const int maxSongs = 20;
			var plist = playlistManager.LoadPlaylist(listId).UnwrapThrow();
			int offsetV = Tools.Clamp(offset ?? 0, 0, plist.Items.Count);
			int countV = Tools.Clamp(count ?? maxSongs, 0, Math.Min(maxSongs, plist.Items.Count - offsetV));
			var items = plist.Items.Skip(offsetV).Take(countV).Select(x => resourceFactory.ToApiFormat(x)).ToArray();
			var plInfo = new PlaylistInfo(listId, plist.Title)
			{
				SongCount = plist.Items.Count,
				DisplayOffset = offsetV,
				Items = items,
			};

			return JsonValue.Create(plInfo, x =>
			{
				var tmb = new TextModBuilder();
				tmb.AppendFormat(strings.cmd_list_show_header, x.Title.Mod().Bold(), x.SongCount.ToString()).Append("\n");
				var index = x.DisplayOffset;
				foreach (var plitem in x.Items!)
					tmb.Append((index++).ToString()).Append(": ").AppendLine(plitem.Title);
				return tmb.ToString();
			});
		}

		[Command("next")]
		public static async Task CommandNext(PlayManager playManager, InvokerData invoker)
			=> await playManager.Next(invoker);

		[Command("param", "_undocumented")] // TODO add documentation, when name decided
		public static async Task<object?> CommandParam(ExecutionInformation info, int index)
		{
			if (!info.TryGet<AliasContext>(out var ctx) || ctx.Arguments is null)
				throw new CommandException("No parameter available", CommandExceptionReason.CommandError);

			if (index < 0 || index >= ctx.Arguments.Count)
				return null;

			var backup = ctx.Arguments;
			ctx.Arguments = null;
			var result = await backup[index].Execute(info, Array.Empty<ICommand>());
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
		public static Task CommandPmChannel(Ts3Client ts3Client, string message) => ts3Client.SendChannelMessage(message);

		[Command("pm server", "_undocumented")] // TODO
		public static Task CommandPmServer(Ts3Client ts3Client, string message) => ts3Client.SendServerMessage(message);

		[Command("pm user")]
		public static Task CommandPmUser(Ts3Client ts3Client, ushort clientId, string message) => ts3Client.SendMessage(message, (ClientId)clientId);

		[Command("pause")]
		public static void CommandPause(Player playerConnection) => playerConnection.Paused = !playerConnection.Paused;

		[Command("play")]
		public static async Task CommandPlay(PlayManager playManager, Player playerConnection, InvokerData invoker)
		{
			if (!playManager.IsPlaying)
				await playManager.Play(invoker);
			else
				playerConnection.Paused = false;
		}

		[Command("play")]
		public static async Task CommandPlay(PlayManager playManager, InvokerData invoker, string url, params string[] attributes)
			=> await playManager.Play(invoker, url, meta: PlayManager.ParseAttributes(attributes));

		[Command("play")]
		public static async Task CommandPlay(PlayManager playManager, InvokerData invoker, IAudioResourceResult rsc, params string[] attributes)
			=> await playManager.Play(invoker, rsc.AudioResource, meta: PlayManager.ParseAttributes(attributes));

		[Command("plugin list")]
		public static JsonArray<PluginStatusInfo> CommandPluginList(PluginManager pluginManager, Bot? bot = null)
			=> new JsonArray<PluginStatusInfo>(pluginManager.GetPluginOverview(bot), PluginManager.FormatOverview);

		[Command("plugin unload")]
		public static void CommandPluginUnload(PluginManager pluginManager, string identifier, Bot? bot = null)
		{
			var result = pluginManager.StopPlugin(identifier, bot);
			if (result != PluginResponse.Ok)
				throw new CommandException(string.Format(strings.error_plugin_error, result /*TODO*/), CommandExceptionReason.CommandError);
		}

		[Command("plugin load")]
		public static void CommandPluginLoad(PluginManager pluginManager, string identifier, Bot? bot = null)
		{
			var result = pluginManager.StartPlugin(identifier, bot);
			if (result != PluginResponse.Ok)
				throw new CommandException(string.Format(strings.error_plugin_error, result /*TODO*/), CommandExceptionReason.CommandError);
		}

		[Command("previous")]
		public static async Task CommandPrevious(PlayManager playManager, InvokerData invoker)
			=> await playManager.Previous(invoker);

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
		public static async Task CommandQuizOn(Bot bot, PlayManager playManager)
		{
			bot.QuizMode = true;
			if (playManager.IsPlaying)
				await bot.GenerateStatusImage(true, playManager.CurrentPlayData);
			await bot.UpdateBotStatus();
		}
		[Command("quiz off")]
		public static async Task CommandQuizOff(Bot bot, PlayManager playManager, ClientCall? invoker = null)
		{
			if (invoker != null && invoker.Visibiliy == TextMessageTargetMode.Private)
				throw new CommandException(strings.cmd_quiz_off_no_cheating, CommandExceptionReason.CommandError);
			bot.QuizMode = false;
			if (playManager.IsPlaying)
				await bot.GenerateStatusImage(true, playManager.CurrentPlayData);
			await bot.UpdateBotStatus();
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
			=> new JsonValue<LoopMode>(playlistManager.Loop, x => x switch
			{
				LoopMode.Off => strings.cmd_repeat_info_off,
				LoopMode.One => strings.cmd_repeat_info_one,
				LoopMode.All => strings.cmd_repeat_info_all,
				_ => throw Tools.UnhandledDefault(playlistManager.Loop),
			});
		[Command("repeat off")]
		public static void CommandRepeatOff(PlaylistManager playlistManager) => playlistManager.Loop = LoopMode.Off;
		[Command("repeat one")]
		public static void CommandRepeatOne(PlaylistManager playlistManager) => playlistManager.Loop = LoopMode.One;
		[Command("repeat all")]
		public static void CommandRepeatAll(PlaylistManager playlistManager) => playlistManager.Loop = LoopMode.All;

		[Command("rights can")]
		public static async Task<JsonArray<string>> CommandRightsCan(ExecutionInformation info, RightsManager rightsManager, params string[] rights)
			=> new JsonArray<string>(await rightsManager.GetRightsSubset(info, rights), r => r.Count > 0 ? string.Join(", ", r) : strings.info_empty);

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
			if (first != null && second != null)
			{
				return Tools.Random.Next(Math.Min(first.Value, second.Value), Math.Max(first.Value, second.Value));
			}
			else if (first != null)
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
		public static async Task CommandSeek(Player player, TimeSpan position)
		{
			//if (!parsed)
			//	throw new CommandException(strings.cmd_seek_invalid_format, CommandExceptionReason.CommandError);
			if (position < TimeSpan.Zero || position > player.Length)
				throw new CommandException(strings.cmd_seek_out_of_range, CommandExceptionReason.CommandError);

			await player.Seek(position);
		}

		private static IList<AudioResource> GetSearchResult(this UserSession session)
		{
			if (!session.Get<IList<AudioResource>>(SessionConst.SearchResult, out var sessionList))
				throw new CommandException(strings.error_select_empty, CommandExceptionReason.CommandError);

			return sessionList;
		}

		private static AudioResource GetSingleSearchResult(this UserSession session, int index)
		{
			var sessionList = session.GetSearchResult();

			if (index < 0 || index >= sessionList.Count)
				throw new CommandException(string.Format(strings.error_value_not_in_range, 0, sessionList.Count), CommandExceptionReason.CommandError);

			return sessionList[index];
		}

		private static JsonArray<AudioResource> FormatSearchResult(IList<AudioResource> list, CallerInfo callerInfo)
			=> new JsonArray<AudioResource>(list, searchResults =>
			{
				if (searchResults.Count == 0)
					return strings.cmd_search_no_result;

				var tmb = new TextModBuilder(callerInfo.IsColor);
				tmb.AppendFormat(
					strings.cmd_search_header.Mod().Bold(),
					$"!search play <{strings.info_number}>".Mod().Italic(),
					$"!search add <{strings.info_number}>".Mod().Italic()).Append("\n");
				for (int i = 0; i < searchResults.Count; i++)
				{
					tmb.AppendFormat("{0}: {1}\n", i.ToString().Mod().Bold(), searchResults[i].ResourceTitle);
				}

				return tmb.ToString();
			});

		[Command("search add", "_undocumented")] // TODO Doc
		public static async Task CommandSearchAdd(PlayManager playManager, InvokerData invoker, UserSession session, int index)
			=> await playManager.Enqueue(invoker, session.GetSingleSearchResult(index));

		[Command("search from", "_undocumented")] // TODO Doc
		public static async Task<JsonArray<AudioResource>> PropagiateSearch(UserSession session, CallerInfo callerInfo, ResolveContext resolver, string resolverName, string query)
		{
			var list = await resolver.Search(resolverName, query);
			session.Set(SessionConst.SearchResult, list);
			return FormatSearchResult(list, callerInfo);
		}

		[Command("search get", "_undocumented")] // TODO Doc
		public static AudioResource CommandSearchGet(UserSession session, int index)
			=> session.GetSingleSearchResult(index);

		[Command("search play", "_undocumented")] // TODO Doc
		public static async Task CommandSeachPlay(PlayManager playManager, ClientCall clientCall, UserSession session, int index)
			=> await playManager.Play(clientCall, session.GetSingleSearchResult(index));

		[Command("search show", "_undocumented")] // TODO Doc
		public static JsonArray<AudioResource> CommandSearchShow(UserSession session, CallerInfo callerInfo)
			=> FormatSearchResult(session.GetSearchResult(), callerInfo);

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
		public static ConfigPart CommandSettingsGet(ConfBot config, string? path = null)
			=> SettingsGet(config, path);

		[Command("settings set")]
		public static void CommandSettingsSet(ConfBot config, string path, string? value = null)
		{
			SettingsSet(config, path, value);
			if (!config.SaveWhenExists())
			{
				throw new CommandException("Value was set but could not be saved to file. All changes are temporary and will be lost when the bot restarts.",
					CommandExceptionReason.CommandError);
			}
		}

		[Command("settings bot get", "cmd_settings_get_help")]
		public static async Task<ConfigPart> CommandSettingsBotGet(BotManager bots, ConfRoot config, string botName, string? path = null)
		{
			var bot = bots.GetBotLock(botName);
			return await GetConf(bot, config, botName, b => CommandSettingsGet(b, path));
		}

		[Command("settings bot set", "cmd_settings_set_help")]
		public static async Task CommandSettingsBotSet(BotManager bots, ConfRoot config, string botName, string path, string? value = null)
		{
			var bot = bots.GetBotLock(botName);
			await GetConf(bot, config, botName, b => { CommandSettingsSet(b, path, value); return null!; });
		}

		[Command("settings bot reload")]
		public static void CommandSettingsReload(ConfRoot config, string? name = null)
		{
			if (string.IsNullOrEmpty(name))
				config.ClearBotConfigCache();
			else
				config.ClearBotConfigCache(name);
		}

		[Command("settings global get")]
		public static ConfigPart CommandSettingsGlobalGet(ConfRoot config, string? path = null)
			=> SettingsGet(config, path);

		[Command("settings global set")]
		public static void CommandSettingsGlobalSet(ConfRoot config, string path, string? value = null)
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

		private static async Task<ConfigPart> GetConf(Bot? bot, ConfRoot config, string name, Func<ConfBot, ConfigPart> scheduledAction)
		{
			if (bot != null)
			{
				if (bot.Injector.TryGet<ConfBot>(out var conf))
					return await bot.Scheduler.InvokeAsync(() => Task.FromResult(scheduledAction(conf)));
				else
					throw new CommandException("Missing ConfBot", CommandExceptionReason.CommandError);
			}
			else
			{
				var getTemplateResult = config.GetBotConfig(name);
				if (!getTemplateResult.Ok)
					throw new CommandException(strings.error_bot_does_not_exist, getTemplateResult.Error, CommandExceptionReason.CommandError);
				return await Task.FromResult(scheduledAction(getTemplateResult.Value));
			}
		}

		private static ConfigPart SettingsGet(ConfigPart config, string? path = null) => config.ByPathAsArray(path ?? "").SettingsGetSingle();

		private static void SettingsSet(ConfigPart config, string path, string? value)
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
		public static JsonValue<CurrentSongInfo> CommandSong(PlayManager playManager, Player player, Bot bot, ClientCall? invoker = null)
		{
			if (playManager.CurrentPlayData is null)
				throw new CommandException(strings.info_currently_not_playing, CommandExceptionReason.CommandError);
			if (bot.QuizMode && invoker != null && playManager.CurrentPlayData.Invoker.ClientUid != invoker.ClientUid)
				throw new CommandException(strings.info_quizmode_is_active, CommandExceptionReason.CommandError);

			var position = player.Position ?? TimeSpan.Zero;
			var length = player.Length ?? playManager.CurrentPlayData.PlayResource.SongInfo?.Length ?? TimeSpan.Zero;
			return JsonValue.Create(
				new CurrentSongInfo
				{
					Title = playManager.CurrentPlayData.ResourceData.ResourceTitle,
					AudioType = playManager.CurrentPlayData.ResourceData.AudioType,
					Link = playManager.CurrentPlayData.SourceLink,
					Position = position,
					Length = length,
					Paused = player.Paused,
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
			if (invoker.ClientId != null)
				targetManager.WhisperClientSubscribe(invoker.ClientId.Value);
		}

		[Command("subscribe tempchannel")]
		public static void CommandSubscribeTempChannel(IVoiceTarget targetManager, ClientCall? invoker = null, ChannelId? channel = null)
		{
			var subChan = channel ?? invoker?.ChannelId ?? ChannelId.Null;
			if (subChan != ChannelId.Null)
				targetManager.WhisperChannelSubscribe(true, subChan);
		}

		[Command("subscribe channel")]
		public static void CommandSubscribeChannel(IVoiceTarget targetManager, ClientCall? invoker = null, params ChannelId[] channels)
		{
			if (channels.Length == 0)
			{
				var subChan = invoker?.ChannelId;
				if (subChan.HasValue)
					targetManager.WhisperChannelSubscribe(false, subChan.Value);
			}
			else targetManager.WhisperChannelSubscribe(false, channels);
		}

		[Command("subscribe client")]
		public static void CommandSubscribeUser(IVoiceTarget targetManager, ClientId client)
		{
			targetManager.WhisperClientSubscribe(client);
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
		public static JsonEmpty CommandSystemQuit(Core core, CallerInfo caller, UserSession? session = null, string? param = null)
		{
			const string force = "force";

			if (caller.ApiCall || param == force)
			{
				core.Stop();
				return new JsonEmpty(string.Empty);
			}

			Task<string?> ResponseQuit(string message)
			{
				if (TextUtil.GetAnswer(message) == Answer.Yes)
				{
					CommandSystemQuit(core, caller, session, force);
				}
				return Task.FromResult<string?>(null);
			}

			session.SetResponse(ResponseQuit);
			return new JsonEmpty(strings.cmd_quit_confirm + YesNoOption);
		}

		[Command("take")]
		[Usage("<count> <text>", "Take only <count> parts of the text")]
		[Usage("<count> <start> <text>", "Take <count> parts, starting with the part at <start>")]
		[Usage("<count> <start> <delimiter> <text>", "Specify another delimiter for the parts than spaces")]
		public static async Task<string> CommandTake(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			if (arguments.Count < 2)
				throw new CommandException(strings.error_cmd_at_least_two_argument, CommandExceptionReason.MissingParameter);

			int start = 0;
			string? delimiter = null;

			// Get count
			var res = await arguments[0].ExecuteToString(info, Array.Empty<ICommand>());
			if (!int.TryParse(res, out int count) || count < 0)
				throw new CommandException("Count must be an integer >= 0", CommandExceptionReason.CommandError); // LOC: TODO

			if (arguments.Count > 2)
			{
				// Get start
				res = await arguments[1].ExecuteToString(info, Array.Empty<ICommand>());
				if (!int.TryParse(res, out start) || start < 0)
					throw new CommandException("Start must be an integer >= 0", CommandExceptionReason.CommandError); // LOC: TODO
			}

			// Get delimiter if exists
			if (arguments.Count > 3)
				delimiter = await arguments[2].ExecuteToString(info, Array.Empty<ICommand>());

			string text = await arguments[Math.Min(arguments.Count - 1, 3)].ExecuteToString(info, Array.Empty<ICommand>());

			var splitted = delimiter is null
				? text.Split()
				: text.Split(new[] { delimiter }, StringSplitOptions.None);
			if (splitted.Length < start + count)
				throw new CommandException(strings.cmd_take_not_enough_arguements, CommandExceptionReason.CommandError);
			var splittedarr = splitted.Skip(start).Take(count).ToArray();

			return string.Join(delimiter ?? " ", splittedarr);
		}

		[Command("unsubscribe")]
		public static void CommandUnsubscribe(IVoiceTarget targetManager, ClientCall invoker)
		{
			if (invoker.ClientId != null)
				targetManager.WhisperClientUnsubscribe(invoker.ClientId.Value);
		}

		[Command("unsubscribe channel")]
		public static void CommandUnsubscribeChannel(IVoiceTarget targetManager, ClientCall? invoker = null, params ChannelId[] channels)
		{
			if (channels.Length == 0)
			{
				var subChan = invoker?.ChannelId;
				if (subChan.HasValue)
					targetManager.WhisperChannelUnsubscribe(false, subChan.Value);
			}
			else
			{
				targetManager.WhisperChannelUnsubscribe(false, channels);
			}
		}

		[Command("unsubscribe temporary")]
		public static void CommandUnsubscribeTemporary(IVoiceTarget targetManager) => targetManager.ClearTemporary();

		[Command("unsubscribe client")]
		public static void CommandUnsubscribeUser(IVoiceTarget targetManager, ClientId client)
		{
			targetManager.WhisperClientUnsubscribe(client);
		}

		[Command("version")]
		public static JsonValue<BuildData> CommandVersion() => new JsonValue<BuildData>(SystemData.AssemblyData, d => d.ToLongString());

		[Command("volume")]
		public static JsonValue<float> CommandVolume(Player playerConnection)
			=> new JsonValue<float>(playerConnection.Volume, string.Format(strings.cmd_volume_current, playerConnection.Volume.ToString("0.#")));

		[Command("volume")]
		[Usage("<level>", "A new volume level between 0 and 100.")]
		[Usage("+/-<level>", "Adds or subtracts a value from the current volume.")]
		public static void CommandVolume(ExecutionInformation info, Player playerConnection, CallerInfo caller, ConfBot config, string volume, UserSession? session = null)
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
			else if (session != null)
			{
				async Task<string?> ResponseVolume(string message)
				{
					if (TextUtil.GetAnswer(message) == Answer.Yes)
					{
						if (await info.HasRights(RightHighVolume))
							playerConnection.Volume = newVolume;
						else
							return strings.cmd_volume_missing_high_volume_permission;
					}
					return null;
				}

				session.SetResponse(ResponseVolume);
				throw new CommandException(strings.cmd_volume_high_volume_confirm + YesNoOption, CommandExceptionReason.CommandError);
			}
		}

		[Command("whisper all")]
		public static void CommandWhisperAll(IVoiceTarget targetManager) => CommandWhisperGroup(targetManager, GroupWhisperType.AllClients, GroupWhisperTarget.AllChannels);

		[Command("whisper group")]
		public static void CommandWhisperGroup(IVoiceTarget targetManager, GroupWhisperType type, GroupWhisperTarget target, ulong? targetId = null)
		{
			if (type == GroupWhisperType.ServerGroup || type == GroupWhisperType.ChannelGroup)
			{
				if (targetId is null)
					throw new CommandException(strings.cmd_whisper_group_missing_target, CommandExceptionReason.CommandError);
				targetManager.SetGroupWhisper(type, target, targetId.Value);
				targetManager.SendMode = TargetSendMode.WhisperGroup;
			}
			else
			{
				if (targetId != null)
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
					if (x.GroupWhisper is null) throw new ArgumentNullException();
					strb.AppendFormat(strings.cmd_whisper_list_target_whispergroup, x.GroupWhisper.Type, x.GroupWhisper.Target, x.GroupWhisper.TargetId);
					break;
				default:
					throw Tools.UnhandledDefault(x.SendMode);
				}
				return strb.ToString();
			});
		}

		[Command("whisper off")]
		public static void CommandWhisperOff(IVoiceTarget targetManager) => targetManager.SendMode = TargetSendMode.Voice;

		[Command("whisper subscription")]
		public static void CommandWhisperSubsription(IVoiceTarget targetManager) => targetManager.SendMode = TargetSendMode.Whisper;

		[Command("xecute")]
		public static async Task CommandXecute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			foreach (var arg in arguments)
				await arg.Execute(info, Array.Empty<ICommand>());
		}
		// ReSharper enable UnusedMember.Global

		public static async ValueTask<bool> HasRights(this ExecutionInformation info, params string[] rights)
		{
			if (!info.TryGet<CallerInfo>(out var caller)) caller = null;
			if (caller?.SkipRightsChecks ?? false)
				return true;
			if (!info.TryGet<RightsManager>(out var rightsManager))
				return false;
			return await rightsManager.HasAllRights(info, rights);
		}

		public static async Task Write(this ExecutionInformation info, string message)
		{
			if (!info.TryGet<Ts3Client>(out var ts3Client))
				throw new CommandException(strings.error_no_teamspeak_in_context);

			if (!info.TryGet<ClientCall>(out var invoker))
				throw new CommandException(strings.error_no_invoker_in_context);

			if (invoker.Visibiliy is null || invoker.ClientId is null)
				throw new CommandException(strings.error_invoker_not_visible);

			var behaviour = LongTextBehaviour.Split;
			var limit = 1;
			if (info.TryGet<ConfBot>(out var config))
			{
				behaviour = config.Commands.LongMessage;
				limit = config.Commands.LongMessageSplitLimit;
			}

			foreach (var msgPart in LongTextTransform.Split(message, behaviour, ts3Client.ServerConstants.MaxSizeTextMessage, limit))
			{
				switch (invoker.Visibiliy.Value)
				{
				case TextMessageTargetMode.Private:
					await ts3Client.SendMessage(msgPart, invoker.ClientId.Value);
					break;
				case TextMessageTargetMode.Channel:
					await ts3Client.SendChannelMessage(msgPart);
					break;
				case TextMessageTargetMode.Server:
					await ts3Client.SendServerMessage(msgPart);
					break;
				default:
					throw Tools.UnhandledDefault(invoker.Visibiliy.Value);
				}
			}
		}

		public static void UseComplexityTokens(this ExecutionInformation info, int count)
		{
			if (!info.TryGet<CallerInfo>(out var caller) || caller.CommandComplexityCurrent + count > caller.CommandComplexityMax)
				throw new CommandException(strings.error_cmd_complexity_reached, CommandExceptionReason.CommandError);
			caller.CommandComplexityCurrent += count;
		}
	}
}
