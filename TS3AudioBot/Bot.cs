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
	using Audio;
	using Algorithm;
	using CommandSystem;
	using CommandSystem.CommandResults;
	using CommandSystem.Text;
	using Config;
	using Dependency;
	using Helper;
	using History;
	using Localization;
	using Playlists;
	using Plugins;
	using Sessions;
	using System;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using TS3AudioBot.ResourceFactories;
	using TS3Client;
	using TS3Client.Messages;
	using TS3Client.Helper;
	using TS3Client.Full;

	/// <summary>Core class managing all bots and utility modules.</summary>
	public sealed class Bot : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly ConfBot config;
		private TickWorker idleTickWorker;

		internal object SyncRoot { get; } = new object();
		internal bool IsDisposed { get; private set; }
		internal BotInjector Injector { get; }

		public Id Id { get; }
		/// <summary>This is the template name. Can be null.</summary>
		public string Name => config.Name;
		public bool QuizMode { get; set; }

		private readonly ResourceFactory resourceFactory;
		private readonly CommandManager commandManager;
		private Ts3Client clientConnection;
		private Ts3FullClient tsFullClient;
		private SessionManager sessionManager;
		private PlayManager playManager;
		private IVoiceTarget targetManager;
		private IPlayerConnection playerConnection;

		public Bot(Id id, ConfBot config, BotInjector injector, ResourceFactory resourceFactory, CommandManager commandManager)
		{
			this.Id = id;
			this.config = config;
			this.Injector = injector;
			this.resourceFactory = resourceFactory;
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
			config.Events.IdleTime.Changed += (s, e) => EnableIdleTickWorker();
			config.Events.OnIdle.Changed += (s, e) => EnableIdleTickWorker();

			var builder = new DependencyBuilder(Injector);
			builder.AddModule(this);
			builder.AddModule(config);
			builder.AddModule(Injector);
			builder.AddModule(config.Playlists);
			builder.RequestModule<PlaylistIO>();
			builder.RequestModule<PlaylistManager>();
			builder.AddModule(Id);
			builder.AddModule(new Ts3FullClient());
			builder.RequestModule<Ts3BaseFunctions, Ts3FullClient>();
			builder.RequestModule<Ts3Client>();
			builder.RequestModule<IPlayerConnection, Ts3Client>();
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

			tsFullClient = Injector.GetModule<Ts3FullClient>();
			clientConnection = Injector.GetModule<Ts3Client>();
			playerConnection = clientConnection;
			Injector.AddModule<IVoiceTarget>(clientConnection.TargetPipe);
			Injector.AddModule(tsFullClient.Book);

			playManager = Injector.GetModule<PlayManager>();
			targetManager = Injector.GetModule<IVoiceTarget>();
			sessionManager = Injector.GetModule<SessionManager>();

			playerConnection.OnSongEnd += playManager.SongStoppedEvent;
			playerConnection.OnSongUpdated += (s, e) => playManager.Update(e);
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
				playManager.AfterResourceStarted += (s, e) => historyManager.LogAudioResource(new HistorySaveData(e.PlayResource.BaseData, e.Invoker.ClientUid));
			// Update our thumbnail
			playManager.AfterResourceStarted += GenerateStatusImage;
			playManager.AfterResourceStopped += GenerateStatusImage;
			// Register callback for all messages happening
			clientConnection.OnMessageReceived += OnMessageReceived;
			// Register callback to remove open private sessions, when user disconnects
			tsFullClient.OnEachClientLeftView += OnClientLeftView;
			clientConnection.OnBotConnected += OnBotConnected;
			clientConnection.OnBotDisconnect += OnBotDisconnect;

			// Restore all alias from the config
			foreach (var alias in config.Commands.Alias.GetAllItems())
				commandManager.RegisterAlias(alias.Key, alias.Value).UnwrapToLog(Log);

			// Connect the query after everyting is set up
			return clientConnection.Connect();
		}

		private void OnBotConnected(object sender, EventArgs e)
		{
			Log.Info("Bot \"{0}\"({1}) connected.", config.Name, Id);

			EnableIdleTickWorker();

			var badges = config.Connect.Badges.Value;
			if (!string.IsNullOrEmpty(badges))
				clientConnection?.ChangeBadges(badges);

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

			clientConnection.InvalidateClientBuffer();

			ulong? channelId = null, databaseId = null, channelGroup = null;
			ulong[] serverGroups = null;

			if (tsFullClient.Book.Clients.TryGetValue(textMessage.InvokerId, out var bookClient))
			{
				channelId = bookClient.Channel;
				databaseId = bookClient.DatabaseId;
				serverGroups = bookClient.ServerGroups.ToArray();
				channelGroup = bookClient.ChannelGroup;
			}
			else if (!clientConnection.GetClientInfoById(textMessage.InvokerId).GetOk(out var infoClient).GetError(out var infoClientError))
			{
				channelId = infoClient.ChannelId;
				databaseId = infoClient.DatabaseId;
				serverGroups = infoClient.ServerGroups;
				channelGroup = infoClient.ChannelGroup;
			}
			else if (!clientConnection.GetCachedClientById(textMessage.InvokerId).GetOk(out var cachedClient).GetError(out var cachedClientError))
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

			return clientConnection.ChangeDescription(setString ?? "");
		}

		private void GenerateStatusImage(object sender, EventArgs e)
		{
			if (!config.GenerateStatusAvatar || IsDisposed)
				return;

			if (e is PlayInfoEventArgs startEvent)
			{
				Task.Run(() =>
				{
					var thumresult = resourceFactory.GetThumbnail(startEvent.PlayResource);
					if (!thumresult.Ok)
					{
						clientConnection.DeleteAvatar();
						return;
					}

					using (var image = ImageUtil.ResizeImage(thumresult.Value))
					{
						if (image is null)
							return;
						var result = clientConnection.UploadAvatar(image);
						if (!result.Ok)
							Log.Warn("Could not save avatar: {0}", result.Error);
					}
				});
			}
			else
			{
				using (var sleepPic = Util.GetEmbeddedFile("TS3AudioBot.Media.SleepingKitty.png"))
				{
					var result = clientConnection.UploadAvatar(sleepPic);
					if (!result.Ok)
						Log.Warn("Could not save avatar: {0}", result.Error);
				}
			}
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
				// parse (and execute) the command
				var res = commandManager.CommandSystem.Execute(info, command);

				if (!answer)
					return;

				// Write result to user
				switch (res.ResultType)
				{
				case CommandResultType.String:
					var sRes = (StringCommandResult)res;
					if (!string.IsNullOrEmpty(sRes.Content))
						info.Write(sRes.Content).UnwrapToLog(Log);
					break;

				case CommandResultType.Empty:
					break;

				default:
					Log.Warn("Got result which is not a string/empty. Result: {0}", res.ToString());
					break;
				}
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
			var idleTime = config.Events.IdleTime.Value;
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
			Server = tsFullClient.ConnectionData.Address,
			Status = tsFullClient.Connected ? BotStatus.Connected : BotStatus.Connecting,
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
				Injector.GetModule<IPlayerConnection>()?.Dispose();
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
