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
	using CommandSystem.CommandResults;
	using Dependency;
	using Helper;
	using History;
	using Plugins;
	using Sessions;
	using System;
	using System.IO;
	using System.Threading;
	using TS3Client;
	using TS3Client.Full;
	using TS3Client.Messages;

	/// <summary>Core class managing all bots and utility modules.</summary>
	public sealed class Bot : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private MainBotData mainBotData;

		internal object SyncRoot { get; } = new object();
		internal bool IsDisposed { get; private set; }
		internal BotInjector Injector { get; set; }

		public int Id { get; internal set; }
		public bool QuizMode { get; set; }
		public string BadgesString { get; set; }

		// Injected dependencies

		public ConfigFile Config { get; set; }
		public ResourceFactories.ResourceFactoryManager FactoryManager { get; set; }
		public CommandManager CommandManager { get; set; }
		public BotManager BotManager { get; set; }
		public PluginManager PluginManager { get; set; }

		// Onw modules

		/// <summary>Connection object for the current client.</summary>
		public TeamspeakControl QueryConnection { get; set; }
		public SessionManager SessionManager { get; set; }
		public PlayManager PlayManager { get; set; }
		public ITargetManager TargetManager { get; private set; }
		public IPlayerConnection PlayerConnection { get; private set; }

		public R InitializeBot()
		{
			Log.Info("Bot connecting...");

			// Read Config File
			var afd = Config.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			var tfcd = Config.GetDataStruct<Ts3FullClientData>("QueryConnection", true);
			var hmd = Config.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			var pld = Config.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			mainBotData = Config.GetDataStruct<MainBotData>("MainBot", true);

			AudioValues.audioFrameworkData = afd;

			Injector.RegisterType<Bot>();
			Injector.RegisterType<BotInjector>();
			Injector.RegisterType<PlaylistManager>();
			Injector.RegisterType<TeamspeakControl>();
			Injector.RegisterType<SessionManager>();
			Injector.RegisterType<HistoryManager>();
			Injector.RegisterType<PlayManager>();
			Injector.RegisterType<IPlayerConnection>();
			Injector.RegisterType<ITargetManager>();
			Injector.RegisterType<Ts3BaseFunctions>();

			Injector.RegisterModule(this);
			Injector.RegisterModule(Injector);
			Injector.RegisterModule(new PlaylistManager(pld));
			var teamspeakClient = new Ts3Full(tfcd);
			Injector.RegisterModule(teamspeakClient);
			Injector.RegisterModule(teamspeakClient.GetLowLibrary<Ts3FullClient>());
			Injector.RegisterModule(new SessionManager());
			HistoryManager historyManager = null;
			if (hmd.EnableHistory)
				Injector.RegisterModule(historyManager = new HistoryManager(hmd), x => x.Initialize());
			Injector.RegisterModule(new PlayManager());
			Injector.RegisterModule(teamspeakClient.TargetPipe);

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
			if (hmd.EnableHistory)
				PlayManager.AfterResourceStarted += (s, e) => historyManager.LogAudioResource(new HistorySaveData(e.PlayResource.BaseData, e.Owner));
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
				Log.Warn("Bot is not correctly set up. Some commands might not work or are slower. ({0})", refreshResult.Error);

			var clientResult = QueryConnection.GetClientById(textMessage.InvokerId);

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
					Log.Warn("Could not create session with user, some commands might not work ({0})", clientResult.Error);
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
				var thumresult = FactoryManager.GetThumbnail(startEvent.PlayResource);
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

		private void BeforeResourceStarted(object sender, PlayInfoEventArgs e)
		{
			const string DefaultVoiceScript = "!whisper off";
			const string DefaultWhisperScript = "!xecute (!whisper subscription) (!unsubscribe temporary) (!subscribe channeltemp (!getmy channel))";

			var mode = AudioValues.audioFrameworkData.AudioMode;
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

		public BotInfo GetInfo() => new BotInfo { Id = Id, NickName = QueryConnection.GetSelf().OkOr(null)?.Name };

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

				PlayerConnection.Dispose(); // before: logStream,
				PlayerConnection = null;

				QueryConnection.Dispose(); // before: logStream,
				QueryConnection = null;
			}
		}
	}

	public class BotInfo
	{
		public int Id { get; set; }
		public string NickName { get; set; }
		public string Server { get; set; }

		public override string ToString() => $"Id: {Id} Name: {NickName} Server: {Server}";
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
