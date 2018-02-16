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
	using Newtonsoft.Json;
	using Sessions;
	using System;
	using System.IO;
	using System.Threading;
	using Dependency;
	using TS3Client;
	using TS3Client.Messages;

	/// <summary>Core class managing all bots and utility modules.</summary>
	public sealed class Bot : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly Core core;
		private MainBotData mainBotData;

		internal object SyncRoot { get; } = new object();
		internal bool IsDisposed { get; private set; }

		internal BotInjector Injector { get; set; }

		internal TargetScript TargetScript { get; set; }
		/// <summary>Mangement for playlists.</summary>
		public PlaylistManager PlaylistManager { get; set; }
		/// <summary>Connection object for the current client.</summary>
		public TeamspeakControl QueryConnection { get; set; }
		/// <summary>Management for clients talking with the bot.</summary>
		public SessionManager SessionManager { get; set; }
		private HistoryManager historyManager = null;
		/// <summary>Stores all played songs. Can be used to search and restore played songs.</summary>
		public HistoryManager HistoryManager
		{
			get => historyManager ?? throw new CommandException("History has not been enabled", CommandExceptionReason.NotSupported);
			set => historyManager = value;
		}
		/// <summary>Redirects playing, enqueing and song events.</summary>
		public PlayManager PlayManager { get; private set; }
		/// <summary>Used to specify playing mode and active targets to send to.</summary>
		public ITargetManager TargetManager { get; private set; }
		/// <summary>Slim interface to control the audio player.</summary>
		public IPlayerConnection PlayerConnection { get; private set; }

		public bool QuizMode { get; set; }
		public string BadgesString { get; set; }

		public Bot(Core core)
		{
			this.core = core;
		}

		public R InitializeBot()
		{
			Log.Info("Bot connecting...");

			// Read Config File
			var conf = Injector.GetModule<ConfigFile>().Value; // XXX
			var afd = conf.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			var tfcd = conf.GetDataStruct<Ts3FullClientData>("QueryConnection", true);
			var hmd = conf.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			var pld = conf.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			mainBotData = conf.GetDataStruct<MainBotData>("MainBot", true);

			AudioValues.audioFrameworkData = afd;

			Injector.RegisterType<Bot>();
			Injector.RegisterType<BotInjector>();
			Injector.RegisterType<TargetScript>();
			Injector.RegisterType<PlaylistManager>();
			Injector.RegisterType<TeamspeakControl>();
			Injector.RegisterType<SessionManager>();
			Injector.RegisterType<HistoryManager>();
			Injector.RegisterType<PlayManager>();
			Injector.RegisterType<IPlayerConnection>();

			Injector.RegisterModule(this);
			Injector.RegisterModule(Injector);
			Injector.RegisterModule(new PlaylistManager(pld));
			var teamspeakClient = new Ts3Full(tfcd);
			Injector.RegisterModule(teamspeakClient);
			Injector.RegisterModule(new SessionManager(), x => x.Initialize());
			if (hmd.EnableHistory)
				Injector.RegisterModule(new HistoryManager(hmd), x => x.Initialize());
			Injector.RegisterModule(new PlayManager());
			Injector.RegisterModule(new TargetScript());

			TargetManager = teamspeakClient.TargetPipe;

			if (!Injector.AllResolved())
			{
				// TODO detailed log + for inner if
				Log.Warn("Cyclic bot module dependency");
				Injector.ForceCyclicResolve();
				if (!Injector.AllResolved())
				{
					Log.Error("Missing bot module dependency");
					return "Could not load all bot modules";
				}
			}

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
			QueryConnection.OnBotDisconnect += (s, e) => Dispose();
			QueryConnection.OnBotConnected += OnBotConnected;
			BadgesString = tfcd.ClientBadges;

			// Connect the query after everyting is set up
			try { QueryConnection.Connect(); }
			catch (Ts3Exception qcex)
			{
				Log.Info(qcex, "There is either a problem with your connection configuration, or the query has not all permissions it needs.");
				return "Query error";
			}
			return R.OkR;
		}

		private void OnBotConnected(object sender, EventArgs e)
		{
			QueryConnection.ChangeBadges(BadgesString);
		}

		private void TextCallback(object sender, TextMessage textMessage)
		{
			Log.Debug("Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			textMessage.Message = textMessage.Message.TrimStart(' ');
			if (!textMessage.Message.StartsWith("!", StringComparison.Ordinal))
				return;

			var refreshResult = QueryConnection.RefreshClientBuffer(true);
			if (!refreshResult.Ok)
				Log.Warn("Bot is not correctly set up. Some requests might fail or are slower. ({0})", refreshResult.Error);

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
					Log.Error(clientResult.Error);
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
				var execInfo = new ExecutionInformation(core, this, invoker, textMessage.Message, session);

				// check if the user has an open request
				if (session.ResponseProcessor != null)
				{
					var msg = session.ResponseProcessor(execInfo);
					session.ClearResponse();
					if (!string.IsNullOrEmpty(msg))
						execInfo.Write(msg).UnwrapThrow();
					return;
				}

				try
				{
					// parse (and execute) the command
					var res = core.CommandManager.CommandSystem.Execute(execInfo, textMessage.Message);
					// Write result to user
					if (res.ResultType == CommandResultType.String)
					{
						var sRes = (StringCommandResult)res;
						if (!string.IsNullOrEmpty(sRes.Content))
							execInfo.Write(sRes.Content).UnwrapThrow();
					}
					else if (res.ResultType == CommandResultType.Json)
					{
						var sRes = (JsonCommandResult)res;
						execInfo.Write("\nJson str: \n" + sRes.JsonObject.AsStringResult).UnwrapThrow();
						execInfo.Write("\nJson val: \n" + JsonConvert.SerializeObject(sRes.JsonObject)).UnwrapThrow();
					}
				}
				catch (CommandException ex)
				{
					Log.Debug(ex, "Command Error");
					execInfo.Write("Error: " + ex.Message); // XXX check return
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Unexpected command error: {0}", ex.UnrollException());
					execInfo.Write("An unexpected error occured: " + ex.Message); // XXX check return
				}
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
				Log.Warn(result.Error);
		}

		public R UpdateBotStatus(string overrideStr = null)
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
						? "<Quiztime!>"
						: PlayManager.CurrentPlayData.ResourceData.ResourceTitle;
				}
				else
				{
					setString = "<Sleeping>";
				}

				return QueryConnection.ChangeDescription(setString);
			}
		}

		private void GenerateStatusImage(object sender, EventArgs e)
		{
			if (!mainBotData.GenerateStatusAvatar)
				return;

			if (e is PlayInfoEventArgs startEvent)
			{
				var thumresult = core.FactoryManager.GetThumbnail(startEvent.PlayResource);
				if (!thumresult.Ok)
					return;

				using (var bmp = ImageUtil.BuildStringImage("Now playing: " + startEvent.ResourceData.ResourceTitle, thumresult.Value))
				{
					using (var mem = new MemoryStream())
					{
						bmp.Save(mem, System.Drawing.Imaging.ImageFormat.Jpeg);
						var result = QueryConnection.UploadAvatar(mem);
						if (!result.Ok)
							Log.Warn("Could not save avatar: {0}", result.Error);
					}
				}
			}
			else
			{
				using (var sleepPic = Util.GetEmbeddedFile("TS3AudioBot.Media.SleepingKitty.png"))
				{
					var result = QueryConnection.UploadAvatar(sleepPic);
					if (!result.Ok)
						Log.Warn("Could not save avatar: {0}", result.Error);
				}
			}
		}

		public BotLock GetBotLock()
		{
			Monitor.Enter(SyncRoot);
			return new BotLock(!IsDisposed, this);
		}

		public void Dispose()
		{
			core.Bots?.RemoveBot(this);

			lock (SyncRoot)
			{
				if (!IsDisposed) IsDisposed = true;
				else return;
				Log.Info("Bot disconnecting.");

				PlayManager?.Stop();

				PlayerConnection?.Dispose(); // before: logStream,
				PlayerConnection = null;

				QueryConnection?.Dispose(); // before: logStream,
				QueryConnection = null;
			}
		}
	}

#pragma warning disable CS0649
	internal class MainBotData : ConfigData
	{
		[Info("Teamspeak group id giving the Bot enough power to do his job", "0")]
		public ulong BotGroupId { get; set; }
		[Info("Generate fancy status images as avatar", "true")]
		public bool GenerateStatusAvatar { get; set; }
	}
#pragma warning restore CS0649
}
