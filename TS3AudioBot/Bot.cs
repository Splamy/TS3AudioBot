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
	using Algorithm;
	using CommandSystem;
	using CommandSystem.CommandResults;
	using Config;
	using Dependency;
	using Helper;
	using History;
	using Localization;
	using Playlists;
	using Plugins;
	using Sessions;
	using System;
	using System.Threading;
	using TS3Client;
	using TS3Client.Full;
	using TS3Client.Messages;

	/// <summary>Core class managing all bots and utility modules.</summary>
	public sealed class Bot : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly ConfBot config;

		internal object SyncRoot { get; } = new object();
		internal bool IsDisposed { get; private set; }
		internal BotInjector Injector { get; set; }

		public int Id { get; internal set; }
		public bool QuizMode { get; set; }
		public string BadgesString { get; set; }

		// Injected dependencies

		public ConfRoot CoreConfig { get; set; }
		public ResourceFactories.ResourceFactoryManager FactoryManager { get; set; }
		public CommandManager CommandManager { get; set; }
		public BotManager BotManager { get; set; }
		public PluginManager PluginManager { get; set; }

		// Own modules

		/// <summary>Connection object for the current client.</summary>
		public TeamspeakControl ClientConnection { get; set; }
		public SessionManager SessionManager { get; set; }
		public PlayManager PlayManager { get; set; }
		public IVoiceTarget TargetManager { get; private set; }
		public IPlayerConnection PlayerConnection { get; private set; }
		public Filter Filter { get; private set; }

		public Bot(ConfBot config)
		{
			this.config = config;
		}

		public E<string> InitializeBot()
		{
			Log.Info("Bot connecting...");

			// Registering config changes
			config.CommandMatcher.Changed += (s, e) =>
			{
				var newMatcher = Filter.GetFilterByName(e.NewValue);
				if (newMatcher.Ok)
					Filter.Current = newMatcher.Value;
			};
			config.Language.Changed += (s, e) =>
			{
				var langResult = LocalizationManager.LoadLanguage(e.NewValue);
				if (!langResult.Ok)
					Log.Error("Failed to load language file ({0})", langResult.Error);
			};

			Injector.RegisterType<Bot>();
			Injector.RegisterType<ConfBot>();
			Injector.RegisterType<BotInjector>();
			Injector.RegisterType<PlaylistManager>();
			Injector.RegisterType<TeamspeakControl>();
			Injector.RegisterType<SessionManager>();
			Injector.RegisterType<HistoryManager>();
			Injector.RegisterType<PlayManager>();
			Injector.RegisterType<IPlayerConnection>();
			Injector.RegisterType<IVoiceTarget>();
			Injector.RegisterType<Ts3BaseFunctions>();
			Injector.RegisterType<Filter>();

			Injector.RegisterModule(this);
			Injector.RegisterModule(config);
			Injector.RegisterModule(Injector);
			Injector.RegisterModule(new PlaylistManager(config.Playlists));
			var teamspeakClient = new Ts3Full(config);
			Injector.RegisterModule(teamspeakClient);
			Injector.RegisterModule(teamspeakClient.GetLowLibrary<Ts3FullClient>());
			Injector.RegisterModule(new SessionManager());
			HistoryManager historyManager = null;
			if (config.History.Enabled)
				Injector.RegisterModule(historyManager = new HistoryManager(config.History), x => x.Initialize());
			Injector.RegisterModule(new PlayManager());
			Injector.RegisterModule(teamspeakClient.TargetPipe);

			var filter = Filter.GetFilterByName(config.CommandMatcher);
			Injector.RegisterModule(new Filter { Current = filter.OkOr(Filter.DefaultAlgorithm) });
			if (!filter.Ok) Log.Warn("Unknown command_matcher config. Using default.");

			if (!Injector.AllResolved())
			{
				Log.Warn("Cyclic bot module dependency");
				Injector.ForceCyclicResolve();
				if (!Injector.AllResolved())
				{
					Log.Error("Missing bot module dependency");
					return "Could not load all bot modules";
				}
			}

			PlayerConnection.OnSongEnd += PlayManager.SongStoppedHook;
			PlayManager.BeforeResourceStarted += BeforeResourceStarted;
			// In own favor update the own status text to the current song title
			PlayManager.AfterResourceStarted += LoggedUpdateBotStatus;
			PlayManager.AfterResourceStopped += LoggedUpdateBotStatus;
			// Log our resource in the history
			if (config.History.Enabled)
				PlayManager.AfterResourceStarted += (s, e) => historyManager.LogAudioResource(new HistorySaveData(e.PlayResource.BaseData, e.Owner));
			// Update our thumbnail
			PlayManager.AfterResourceStarted += GenerateStatusImage;
			PlayManager.AfterResourceStopped += GenerateStatusImage;
			// Register callback for all messages happening
			ClientConnection.OnMessageReceived += TextCallback;
			// Register callback to remove open private sessions, when user disconnects
			ClientConnection.OnClientDisconnect += OnClientDisconnect;
			ClientConnection.OnBotConnected += OnBotConnected;
			ClientConnection.OnBotDisconnect += OnBotDisconnect;
			BadgesString = config.Connect.Badges;

			// Connect the query after everyting is set up
			return ClientConnection.Connect();
		}

		private void OnBotConnected(object sender, EventArgs e)
		{
			Log.Info("Bot connected.");
			if (!string.IsNullOrEmpty(BadgesString))
				ClientConnection?.ChangeBadges(BadgesString);
		}

		private void OnBotDisconnect(object sender, DisconnectEventArgs e)
		{
			Dispose();
		}

		private void TextCallback(object sender, TextMessage textMessage)
		{
			var langResult = LocalizationManager.LoadLanguage(config.Language);
			if (!langResult.Ok)
				Log.Error("Failed to load language file ({0})", langResult.Error);

			Log.Debug("Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			textMessage.Message = textMessage.Message.TrimStart(' ');
			if (!textMessage.Message.StartsWith("!", StringComparison.Ordinal))
				return;

			var refreshResult = ClientConnection.RefreshClientBuffer(true);
			if (!refreshResult.Ok)
				Log.Warn("Bot is not correctly set up. Some commands might not work or are slower.", refreshResult.Error.Str);

			var clientResult = ClientConnection.GetClientById(textMessage.InvokerId);

			// get the current session
			UserSession session = null;
			var result = SessionManager.GetSession(textMessage.InvokerId);
			if (result.Ok)
			{
				session = result.Value;
			}
			else
			{
				if (clientResult.Ok)
					session = SessionManager.CreateSession(clientResult.Value);
				else
					Log.Warn("Could not create session with user, some commands might not work ({0})", clientResult.Error.Str);
			}

			var invoker = new InvokerData(textMessage.InvokerUid)
			{
				ClientId = textMessage.InvokerId,
				Visibiliy = textMessage.Target,
				NickName = textMessage.InvokerName,
			};
			if (clientResult.Ok)
			{
				invoker.ChannelId = clientResult.Value.ChannelId;
				invoker.DatabaseId = clientResult.Value.DatabaseId;
			}

			var info = CreateExecInfo(invoker, session);

			UserSession.SessionToken sessionLock = null;
			try
			{
				if (session != null)
				{
					sessionLock = session.GetLock();
					// check if the user has an open request
					if (session.ResponseProcessor != null)
					{
						var msg = session.ResponseProcessor(textMessage.Message);
						session.ClearResponse();
						if (!string.IsNullOrEmpty(msg))
							info.Write(msg).UnwrapThrow();
						return;
					}
				}

				CallScript(info, textMessage.Message, true, false);
			}
			finally
			{
				sessionLock?.Dispose();
			}
		}

		private void OnClientDisconnect(object sender, ClientLeftView eventArgs)
		{
			TargetManager.WhisperClientUnsubscribe(eventArgs.ClientId);
			SessionManager.RemoveSession(eventArgs.ClientId);
		}

		private void LoggedUpdateBotStatus(object sender, EventArgs e)
		{
			var result = UpdateBotStatus();
			if (!result)
				Log.Warn(result.Error.Str);
		}

		public E<LocalStr> UpdateBotStatus(string overrideStr = null)
		{
			lock (SyncRoot)
			{
				string setString;
				if (overrideStr != null)
				{
					setString = overrideStr;
				}
				else if (PlayManager.IsPlaying)
				{
					setString = QuizMode
						? strings.info_botstatus_quiztime
						: PlayManager.CurrentPlayData.ResourceData.ResourceTitle;
				}
				else
				{
					setString = strings.info_botstatus_sleeping;
				}

				return ClientConnection.ChangeDescription(setString);
			}
		}

		private void GenerateStatusImage(object sender, EventArgs e)
		{
			if (!config.GenerateStatusAvatar)
				return;

			if (e is PlayInfoEventArgs startEvent)
			{
#if NET46
				var thumresult = FactoryManager.GetThumbnail(startEvent.PlayResource);
				if (!thumresult.Ok)
					return;

				System.Drawing.Image bmpOrig;
				try
				{
					using (var stream = thumresult.Value)
						bmpOrig = System.Drawing.Image.FromStream(stream);
				}
				catch (ArgumentException)
				{
					Log.Warn("Inavlid image data");
					return;
				}

				// TODO: remove 'now playing' text and use better imaging lib
				using (var bmp = ImageUtil.BuildStringImage("Now playing: " + startEvent.ResourceData.ResourceTitle, bmpOrig))
				{
					using (var mem = new System.IO.MemoryStream())
					{
						bmp.Save(mem, System.Drawing.Imaging.ImageFormat.Jpeg);
						var result = ClientConnection.UploadAvatar(mem);
						if (!result.Ok)
							Log.Warn("Could not save avatar: {0}", result.Error);
					}
				}
#endif
			}
			else
			{
				using (var sleepPic = Util.GetEmbeddedFile("TS3AudioBot.Media.SleepingKitty.png"))
				{
					var result = ClientConnection.UploadAvatar(sleepPic);
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
			info.AddDynamicObject(new CallerInfo(command, false) { SkipRightsChecks = skipRights });

			try
			{
				// parse (and execute) the command
				var res = CommandManager.CommandSystem.Execute(info, command);

				if (!answer)
					return;

				// Write result to user
				if (res.ResultType == CommandResultType.String)
				{
					var sRes = (StringCommandResult)res;
					if (!string.IsNullOrEmpty(sRes.Content))
						info.Write(sRes.Content).UnwrapThrow();
				}
				else if (res.ResultType == CommandResultType.Json)
				{
					var sRes = (JsonCommandResult)res;
					info.Write("\nJson str: \n" + sRes.JsonObject).UnwrapThrow();
					info.Write("\nJson val: \n" + sRes.JsonObject.Serialize()).UnwrapThrow();
				}
			}
			catch (CommandException ex)
			{
				Log.Debug(ex, "Command Error ({0})", ex.Message);
				if (answer) info.Write("Error: " + ex.Message); // XXX check return
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected command error: {0}", ex.UnrollException());
				if (answer) info.Write("An unexpected error occured: " + ex.Message); // XXX check return
			}
		}

		private ExecutionInformation CreateExecInfo(InvokerData invoker = null, UserSession session = null)
		{
			var info = new ExecutionInformation(Injector.CloneRealm<DependencyRealm>());
			if (invoker != null)
				info.AddDynamicObject(invoker);
			if (session != null)
				info.AddDynamicObject(session);
			return info;
		}

		public BotLock GetBotLock()
		{
			Monitor.Enter(SyncRoot);
			return new BotLock(!IsDisposed, this);
		}

		public BotInfo GetInfo() => new BotInfo
		{
			Id = Id,
			NickName = ClientConnection.GetSelf().OkOr(null)?.Name,
			Server = config.Connect.Address,
		};

		public void Dispose()
		{
			BotManager.RemoveBot(this);

			lock (SyncRoot)
			{
				if (!IsDisposed) IsDisposed = true;
				else return;
				Log.Info("Bot disconnecting.");

				PluginManager.StopPlugins(this);

				PlayManager.Stop();
				PlayManager = null;

				PlayerConnection.Dispose();
				PlayerConnection = null;

				ClientConnection.Dispose();
				ClientConnection = null;
			}
		}
	}

	public class BotInfo
	{
		public int Id { get; set; }
		public string NickName { get; set; }
		public string Server { get; set; }

		public override string ToString() => $"Id: {Id} Name: {NickName} Server: {Server}"; // LOC: TODO
	}
}
