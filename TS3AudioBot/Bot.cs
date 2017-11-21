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
	using Sessions;
	using System;
	using System.IO;
	using TS3Client;
	using TS3Client.Messages;

	/// <summary>Core class managing all bots and utility modules.</summary>
	public sealed class Bot : IDisposable
	{
		private bool isDisposed;
		private readonly Core core;
		private MainBotData mainBotData;

		internal TargetScript TargetScript { get; set; }
		/// <summary>Mangement for playlists.</summary>
		public PlaylistManager PlaylistManager { get; private set; }
		/// <summary>Connection object for the current client.</summary>
		public TeamspeakControl QueryConnection { get; private set; }
		/// <summary>Management for clients talking with the bot.</summary>
		public SessionManager SessionManager { get; private set; }
		private HistoryManager historyManager = null;
		/// <summary>Stores all played songs. Can be used to search and restore played songs.</summary>
		public HistoryManager HistoryManager => historyManager ?? throw new CommandException("History has not been enabled", CommandExceptionReason.NotSupported);
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

		public bool InitializeBot()
		{
			Log.Write(Log.Level.Info, "Bot connecting...");

			// Read Config File
			var conf = core.ConfigManager;
			var afd = conf.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			var tfcd = conf.GetDataStruct<Ts3FullClientData>("QueryConnection", true);
			var hmd = conf.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			var pld = conf.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			mainBotData = conf.GetDataStruct<MainBotData>("MainBot", true);

			AudioValues.audioFrameworkData = afd;
			var teamspeakClient = new Ts3Full(tfcd);
			QueryConnection = teamspeakClient;
			PlayerConnection = teamspeakClient;
			PlaylistManager = new PlaylistManager(pld);
			SessionManager = new SessionManager(core.Database);
			if (hmd.EnableHistory)
				historyManager = new HistoryManager(hmd, core.Database);
			PlayManager = new PlayManager(core, this);
			TargetManager = teamspeakClient;
			TargetScript = new TargetScript(core, this);
			
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
			QueryConnection.OnClientConnect += OnClientConnect;
			BadgesString = tfcd.ClientBadges;

			// Connect the query after everyting is set up
			try { QueryConnection.Connect(); }
			catch (Ts3Exception qcex)
			{
				Log.Write(Log.Level.Error, "There is either a problem with your connection configuration, or the query has not all permissions it needs. ({0})", qcex);
				return false;
			}
			return true;
		}

		private void OnClientConnect(object sender, ClientEnterView e)
		{
			var me = QueryConnection.GetSelf();
			if (e.ClientId == me.Value.ClientId)
			{
				QueryConnection.ChangeBadges(BadgesString);
			}
		}

		private void TextCallback(object sender, TextMessage textMessage)
		{
			Log.Write(Log.Level.Debug, "MB Got message from {0}: {1}", textMessage.InvokerName, textMessage.Message);

			textMessage.Message = textMessage.Message.TrimStart(' ');
			if (!textMessage.Message.StartsWith("!", StringComparison.Ordinal))
				return;

			var refreshResult = QueryConnection.RefreshClientBuffer(true);
			if (!refreshResult.Ok)
				Log.Write(Log.Level.Warning, "Bot is not correctly set up. Some requests might fail or are slower. ({0})", refreshResult.Message);

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
					Log.Write(Log.Level.Error, clientResult.Message);
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
						execInfo.Write(msg);
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
							execInfo.Write(sRes.Content);
					}
					else if (res.ResultType == CommandResultType.Json)
					{
						var sRes = (JsonCommandResult)res;
						execInfo.Write("\nJson str: \n" + sRes.JsonObject.AsStringResult);
						execInfo.Write("\nJson val: \n" + Util.Serializer.Serialize(sRes.JsonObject));
					}
				}
				catch (CommandException ex)
				{
					execInfo.Write("Error: " + ex.Message);
				}
				catch (Exception ex)
				{
					Log.Write(Log.Level.Error, "MB Unexpected command error: {0}", ex.UnrollException());
					execInfo.Write("An unexpected error occured: " + ex.Message);
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
				Log.Write(Log.Level.Warning, result.Message);
		}

		public R UpdateBotStatus(string overrideStr = null)
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
							Log.Write(Log.Level.Warning, "Could not save avatar: {0}", result.Message);
					}
				}
			}
			else
			{
				using (var sleepPic = Util.GetEmbeddedFile("TS3AudioBot.Media.SleepingKitty.png"))
				{
					var result = QueryConnection.UploadAvatar(sleepPic);
					if (!result.Ok)
						Log.Write(Log.Level.Warning, "Could not save avatar: {0}", result.Message);
				}
			}
		}

		public void Dispose()
		{
			if (!isDisposed) isDisposed = true;
			else return;
			Log.Write(Log.Level.Info, "Bot disconnecting.");

			core.Bots.StopBot(this);

			PlayManager?.Stop();

			PlayerConnection?.Dispose(); // before: logStream,
			PlayerConnection = null;

			QueryConnection?.Dispose(); // before: logStream,
			QueryConnection = null;
		}
	}

#pragma warning disable CS0649
	internal class MainBotData : ConfigData
	{
		[Info("Path to the logfile", "ts3audiobot.log")]
		public string LogFile { get; set; }
		[Info("Teamspeak group id giving the Bot enough power to do his job", "0")]
		public ulong BotGroupId { get; set; }
		[Info("Generate fancy status images as avatar", "true")]
		public bool GenerateStatusAvatar { get; set; }
	}
#pragma warning restore CS0649
}
