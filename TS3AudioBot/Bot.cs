// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Algorithm;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.CommandSystem.Text;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Helper;
using TS3AudioBot.History;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.Plugins;
using TS3AudioBot.ResourceFactories;
using TS3AudioBot.Sessions;
using TSLib;
using TSLib.Full;
using TSLib.Helper;
using TSLib.Messages;

namespace TS3AudioBot
{
	/// <summary>Core class managing all bots and utility modules.</summary>
	public sealed class Bot : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly ConfBot config;
		private TickWorker idleTickWorker;
		private TickWorker aloneTickWorker;

		internal object SyncRoot { get; } = new object();
		internal bool IsDisposed { get; private set; }
		internal BotInjector Injector { get; }

		public Id Id { get; }
		/// <summary>This is the template name. Can be null.</summary>
		public string Name => config.Name;
		public bool QuizMode { get; set; }

		private readonly ResourceResolver resourceResolver;
		private readonly CommandManager commandManager;
		private Ts3Client ts3client;
		private TsFullClient ts3FullClient;
		private SessionManager sessionManager;
		private PlayManager playManager;
		private IVoiceTarget targetManager;
		private Player player;

		public Bot(Id id, ConfBot config, BotInjector injector, ResourceResolver resourceFactory, CommandManager commandManager)
		{
			this.Id = id;
			this.config = config;
			this.Injector = injector;
			this.resourceResolver = resourceFactory;
			this.commandManager = commandManager;
		}

		public E<string> InitializeBot()
		{
			Log.Info("Bot \"{0}\" connecting to \"{1}\"", config.Name, config.Connect.Address);

			// Registering config changes
			config.Language.Changed += (s, e) =>
			{
				var langResult = LocalizationManager.LoadLanguage(e.NewValue, true);
				if (!langResult.Ok)
					Log.Error("Failed to load language file ({0})", langResult.Error);
			};
			config.Events.IdleDelay.Changed += (s, e) => EnableIdleTickWorker();
			config.Events.OnIdle.Changed += (s, e) => EnableIdleTickWorker();

			var builder = new DependencyBuilder(Injector);
			builder.AddModule(this);
			builder.AddModule(config);
			builder.AddModule(Injector);
			builder.AddModule(config.Playlists);
			builder.RequestModule<PlaylistIO>();
			builder.RequestModule<PlaylistManager>();
			builder.AddModule(Id);
			builder.AddModule(new TsFullClient());
			builder.RequestModule<TsBaseFunctions, TsFullClient>();
			builder.RequestModule<Ts3Client>();
			builder.RequestModule<Player>();
			builder.RequestModule<CustomTargetPipe>();
			builder.RequestModule<IVoiceTarget, CustomTargetPipe>();
			builder.RequestModule<SessionManager>();
			if (config.History.Enabled)
			{
				builder.AddModule(config.History);
				builder.RequestModule<HistoryManager>();
			}
			builder.RequestModule<PlayManager>();

			if (!builder.Build())
			{
				Log.Error("Missing bot module dependency");
				return "Could not load all bot modules";
			}

			ts3FullClient = Injector.GetModule<TsFullClient>();
			ts3client = Injector.GetModule<Ts3Client>();
			player = Injector.GetModule<Player>();
			player.SetTarget(Injector.GetModule<CustomTargetPipe>());
			Injector.AddModule(ts3FullClient.Book);

			playManager = Injector.GetModule<PlayManager>();
			targetManager = Injector.GetModule<IVoiceTarget>();
			sessionManager = Injector.GetModule<SessionManager>();

			player.OnSongEnd += playManager.SongStoppedEvent;
			player.OnSongUpdated += (s, e) => playManager.Update(e);
			// Update idle status events
			playManager.BeforeResourceStarted += (s, e) => DisableIdleTickWorker();
			playManager.AfterResourceStopped += (s, e) => EnableIdleTickWorker();
			// Used for the voice_mode script
			playManager.BeforeResourceStarted += BeforeResourceStarted;
			// Update the own status text to the current song title
			playManager.AfterResourceStarted += LoggedUpdateBotStatus;
			playManager.AfterResourceStopped += LoggedUpdateBotStatus;
			playManager.OnResourceUpdated += LoggedUpdateBotStatus;
			// Log our resource in the history
			if (Injector.TryGet<HistoryManager>(out var historyManager))
				playManager.AfterResourceStarted += (s, e) => historyManager.LogAudioResource(new HistorySaveData(e.PlayResource.BaseData, e.MetaData.ResourceOwnerUid));
			// Update our thumbnail
			playManager.AfterResourceStarted += GenerateStatusImage;
			playManager.AfterResourceStopped += GenerateStatusImage;
			// Register callback for all messages happening
			ts3client.OnMessageReceived += OnMessageReceived;
			// Register callback to remove open private sessions, when user disconnects
			ts3FullClient.OnEachClientLeftView += OnClientLeftView;
			ts3client.OnBotConnected += OnBotConnected;
			ts3client.OnBotDisconnect += OnBotDisconnect;
			// Alone mode
			ts3client.OnAloneChanged += OnAloneChanged;
			// Whisper stall
			ts3client.OnWhisperNoTarget += (s, e) => player.SetStall();

			// Restore all alias from the config
			foreach (var alias in config.Commands.Alias.GetAllItems())
				commandManager.RegisterAlias(alias.Key, alias.Value).UnwrapToLog(Log);

			// Connect the query after everyting is set up
			return ts3client.Connect();
		}

		private void OnBotConnected(object sender, EventArgs e)
		{
			Log.Info("Bot \"{0}\"({1}) connected.", config.Name, Id);

			EnableIdleTickWorker();

			var badges = config.Connect.Badges.Value;
			if (!string.IsNullOrEmpty(badges))
				ts3client?.ChangeBadges(badges);

			var onStart = config.Events.OnConnect.Value;
			if (!string.IsNullOrEmpty(onStart))
			{
				var info = CreateExecInfo();
				CallScript(info, onStart, false, true);
			}
		}

		private void OnBotDisconnect(object sender, DisconnectEventArgs e)
		{
			DisableIdleTickWorker();

			var onStop = config.Events.OnDisconnect.Value;
			if (!string.IsNullOrEmpty(onStop))
			{
				var info = CreateExecInfo();
				CallScript(info, onStop, false, true);
			}

			Dispose();
		}

		private void OnMessageReceived(object sender, TextMessage textMessage)
		{
			if (textMessage?.Message == null)
			{
				Log.Warn("Invalid TextMessage: {@textMessage}", textMessage);
				return;
			}
			Log.Debug("TextMessage: {@textMessage}", textMessage);

			var langResult = LocalizationManager.LoadLanguage(config.Language, false);
			if (!langResult.Ok)
				Log.Error("Failed to load language file ({0})", langResult.Error);

			textMessage.Message = textMessage.Message.TrimStart(' ');
			if (!textMessage.Message.StartsWith("!", StringComparison.Ordinal))
				return;

			Log.Info("User {0} requested: {1}", textMessage.InvokerName, textMessage.Message);

			ts3client.InvalidateClientBuffer();

			ChannelId? channelId = null;
			ClientDbId? databaseId = null;
			ChannelGroupId? channelGroup = null;
			ServerGroupId[] serverGroups = null;

			if (ts3FullClient.Book.Clients.TryGetValue(textMessage.InvokerId, out var bookClient))
			{
				channelId = bookClient.Channel;
				databaseId = bookClient.DatabaseId;
				serverGroups = bookClient.ServerGroups.ToArray();
				channelGroup = bookClient.ChannelGroup;
			}
			else if (!ts3client.GetClientInfoById(textMessage.InvokerId).GetOk(out var infoClient).GetError(out var infoClientError))
			{
				channelId = infoClient.ChannelId;
				databaseId = infoClient.DatabaseId;
				serverGroups = infoClient.ServerGroups;
				channelGroup = infoClient.ChannelGroup;
			}
			else if (!ts3client.GetCachedClientById(textMessage.InvokerId).GetOk(out var cachedClient).GetError(out var cachedClientError))
			{
				channelId = cachedClient.ChannelId;
				databaseId = cachedClient.DatabaseId;
				channelGroup = cachedClient.ChannelGroup;
			}
			else
			{
				Log.Warn(
					"The bot is missing teamspeak permissions to view the communicating client. " +
					"Some commands or permission checks might not work " +
					"(clientlist:{0}, clientinfo:{1}).",
					cachedClientError.Str, infoClientError.Str);
			}

			var invoker = new ClientCall(textMessage.InvokerUid, textMessage.Message,
				clientId: textMessage.InvokerId,
				visibiliy: textMessage.Target,
				nickName: textMessage.InvokerName,
				channelId: channelId,
				databaseId: databaseId,
				serverGroups: serverGroups,
				channelGroup: channelGroup);

			var session = sessionManager.GetOrCreateSession(textMessage.InvokerId);
			var info = CreateExecInfo(invoker, session);

			using (session.GetLock())
			{
				// check if the user has an open request
				if (session.ResponseProcessor != null)
				{
					TryCatchCommand(info, answer: true, () =>
					{
						var msg = session.ResponseProcessor(textMessage.Message);
						if (!string.IsNullOrEmpty(msg))
							info.Write(msg).UnwrapToLog(Log);
					});
					session.ClearResponse();
					return;
				}

				CallScript(info, textMessage.Message, answer: true, false);
			}
		}

		private void OnClientLeftView(object sender, ClientLeftView eventArgs)
		{
			targetManager.WhisperClientUnsubscribe(eventArgs.ClientId);
			sessionManager.RemoveSession(eventArgs.ClientId);
		}

		private void OnAloneChanged(object sender, AloneChanged e)
		{
			string script;
			TimeSpan delay;
			if (e.Alone)
			{
				script = config.Events.OnAlone.Value;
				delay = config.Events.AloneDelay.Value;
			}
			else
			{
				script = config.Events.OnParty.Value;
				delay = config.Events.PartyDelay.Value;
			}
			if (string.IsNullOrEmpty(script))
				return;

			void RunEvent()
			{
				var info = CreateExecInfo();
				CallScript(info, script, false, true);
			};

			SetAloneTickWorker(null);
			if (delay <= TimeSpan.Zero)
			{
				RunEvent();
			}
			else
			{
				var worker = TickPool.RegisterTickOnce(RunEvent, delay);
				SetAloneTickWorker(worker);
			}
		}

		private void SetAloneTickWorker(TickWorker worker)
		{
			var oldWoker = Interlocked.Exchange(ref aloneTickWorker, worker);
			if (oldWoker != null)
			{
				TickPool.UnregisterTicker(oldWoker);
			}
		}

		private void LoggedUpdateBotStatus(object sender, EventArgs e)
		{
			if (IsDisposed)
				return;
			UpdateBotStatus().UnwrapToLog(Log);
		}

		public E<LocalStr> UpdateBotStatus(string overrideStr = null)
		{
			if (!config.SetStatusDescription)
				return R.Ok;

			string setString;
			if (overrideStr != null)
			{
				setString = overrideStr;
			}
			else if (playManager.IsPlaying)
			{
				setString = QuizMode
					? strings.info_botstatus_quiztime
					: playManager.CurrentPlayData?.ResourceData?.ResourceTitle;
			}
			else
			{
				setString = strings.info_botstatus_sleeping;
			}

			return ts3client.ChangeDescription(setString ?? "");
		}

		private void GenerateStatusImage(object sender, EventArgs e)
		{
			if (!config.GenerateStatusAvatar || IsDisposed)
				return;

			Stream GetRandomFile(string prefix)
			{
				try
				{
					if (string.IsNullOrEmpty(config.LocalConfigDir))
						return null;
					var avatarPath = new DirectoryInfo(Path.Combine(config.LocalConfigDir, BotPaths.Avatars));
					if (!avatarPath.Exists)
						return null;
					var avatars = avatarPath.EnumerateFiles(prefix).ToArray();
					if (avatars.Length == 0)
						return null;
					var pickedAvatar = Tools.PickRandom(avatars);
					return pickedAvatar.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "Failed to load local avatar");
					return null;
				}
			}

			void Upload(Stream setStream)
			{
				if (setStream != null)
				{
					using (setStream)
					{
						var result = ts3client.UploadAvatar(setStream);
						if (!result.Ok)
							Log.Warn("Could not save avatar: {0}", result.Error);
					}
				}
			}

			Task.Run(() =>
			{
				Stream setStream = null;
				if (e is PlayInfoEventArgs startEvent)
				{
					setStream = ImageUtil.ResizeImageSave(resourceResolver.GetThumbnail(startEvent.PlayResource).OkOr(null), out _).OkOr(null);
					setStream = setStream ?? GetRandomFile("play*");
					Upload(setStream);
				}
				else
				{
					setStream = GetRandomFile("sleep*");
					setStream = setStream ?? Util.GetEmbeddedFile("TS3AudioBot.Media.SleepingKitty.png");
					Upload(setStream);
				}

				if (setStream is null)
				{
					ts3client.DeleteAvatar();
					return;
				}
			});
		}

		private void BeforeResourceStarted(object sender, PlayInfoEventArgs e)
		{
			const string DefaultVoiceScript = "!whisper off";
			const string DefaultWhisperScript = "!xecute (!whisper subscription) (!unsubscribe temporary) (!subscribe channeltemp (!getmy channel))";

			var mode = config.Audio.SendMode.Value;
			string script;
			if (mode.StartsWith("!", StringComparison.Ordinal))
				script = mode;
			else if (mode.Equals("voice", StringComparison.OrdinalIgnoreCase))
				script = DefaultVoiceScript;
			else if (mode.Equals("whisper", StringComparison.OrdinalIgnoreCase))
				script = DefaultWhisperScript;
			else
			{
				Log.Error("Invalid voice mode");
				return;
			}

			var info = CreateExecInfo(e.Invoker);
			CallScript(info, script, false, true);
		}

		private void CallScript(ExecutionInformation info, string command, bool answer, bool skipRights)
		{
			Log.Debug("Calling script (skipRights:{0}, answer:{1}): {2}", skipRights, answer, command);

			info.AddModule(new CallerInfo(false)
			{
				SkipRightsChecks = skipRights,
				CommandComplexityMax = config.Commands.CommandComplexity,
				IsColor = config.Commands.Color,
			});

			TryCatchCommand(info, answer, () =>
			{
				// parse and execute the command
				var s = commandManager.CommandSystem.ExecuteCommand(info, command);

				if (!answer)
					return;

				// Write result to user
				if (!string.IsNullOrEmpty(s))
					info.Write(s).UnwrapToLog(Log);
			});
		}

		private ExecutionInformation CreateExecInfo(InvokerData invoker = null, UserSession session = null)
		{
			var info = new ExecutionInformation(Injector);
			if (invoker is ClientCall ci)
				info.AddModule(ci);
			info.AddModule(invoker ?? InvokerData.Anonymous);
			info.AddModule(session ?? new AnonymousSession());
			info.AddModule(Filter.GetFilterByNameOrDefault(config.Commands.Matcher));
			return info;
		}

		private void OnIdle()
		{
			// DisableIdleTickWorker(); // fire once only ??

			var onIdle = config.Events.OnIdle.Value;
			if (!string.IsNullOrEmpty(onIdle))
			{
				var info = CreateExecInfo();
				CallScript(info, onIdle, false, true);
			}
		}

		private void EnableIdleTickWorker()
		{
			var idleTime = config.Events.IdleDelay.Value;
			if (idleTime <= TimeSpan.Zero || string.IsNullOrEmpty(config.Events.OnIdle.Value))
				return;
			var newWorker = TickPool.RegisterTick(OnIdle, idleTime, false);
			SetIdleTickWorker(newWorker);
			newWorker.Active = true;
		}

		private void DisableIdleTickWorker() => SetIdleTickWorker(null);

		private void SetIdleTickWorker(TickWorker worker)
		{
			var oldWoker = Interlocked.Exchange(ref idleTickWorker, worker);
			if (oldWoker != null)
			{
				TickPool.UnregisterTicker(oldWoker);
			}
		}

		private void TryCatchCommand(ExecutionInformation info, bool answer, Action action)
		{
			try
			{
				action.Invoke();
			}
			catch (CommandException ex)
			{
				NLog.LogLevel commandErrorLevel = answer ? NLog.LogLevel.Debug : NLog.LogLevel.Warn;
				Log.Log(commandErrorLevel, ex, "Command Error ({0})", ex.Message);
				if (answer)
				{
					info.Write(TextMod.Format(config.Commands.Color, strings.error_call_error.Mod().Color(Color.Red).Bold(), ex.Message))
						.UnwrapToLog(Log);
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected command error: {0}", ex.UnrollException());
				if (answer)
				{
					info.Write(TextMod.Format(config.Commands.Color, strings.error_call_unexpected_error.Mod().Color(Color.Red).Bold(), ex.Message))
						.UnwrapToLog(Log);
				}
			}
		}

		public BotLock GetBotLock()
		{
			Monitor.Enter(SyncRoot);
			if (IsDisposed)
			{
				Monitor.Exit(SyncRoot);
				return null;
			}
			return new BotLock(this);
		}

		public BotInfo GetInfo() => new BotInfo
		{
			Id = Id,
			Name = config.Name,
			Server = ts3FullClient.ConnectionData.Address,
			Status = ts3FullClient.Connected ? BotStatus.Connected : BotStatus.Connecting,
		};

		public void Dispose()
		{
			Injector.GetModule<BotManager>()?.RemoveBot(this);

			lock (SyncRoot)
			{
				if (!IsDisposed) IsDisposed = true;
				else return;

				Log.Info("Bot ({0}) disconnecting.", Id);

				DisableIdleTickWorker();

				Injector.GetModule<PluginManager>()?.StopPlugins(this);
				Injector.GetModule<PlayManager>()?.Stop();
				Injector.GetModule<Player>()?.Dispose();
				Injector.GetModule<Ts3Client>()?.Dispose();
				config.ClearEvents();
			}
		}
	}

	public class BotInfo
	{
		public int? Id { get; set; }
		public string Name { get; set; }
		public string Server { get; set; }
		public BotStatus Status { get; set; }

		public override string ToString() => $"Id: {Id} Name: {Name} Server: {Server} Status: {Status.ToString()}"; // LOC: TODO
	}

	public enum BotStatus
	{
		Offline,
		Connecting,
		Connected,
	}
}
