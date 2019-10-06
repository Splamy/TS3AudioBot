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
	using Config;
	using Helper;
	using Helper.Environment;
	using Localization;
	using RExtensions;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3AudioBot.Algorithm;
	using TS3AudioBot.ResourceFactories;
	using TS3Client;
	using TS3Client.Audio;
	using TS3Client.Commands;
	using TS3Client.Full;
	using TS3Client.Helper;
	using TS3Client.Messages;

	public sealed class Ts3Client : IPlayerConnection, IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly Id id;
		private const Codec SendCodec = Codec.OpusMusic;

		public event EventHandler<EventArgs> OnBotConnected;
		public event EventHandler<DisconnectEventArgs> OnBotDisconnect;
		public event EventHandler<TextMessage> OnMessageReceived;

		private static readonly string[] QuitMessages = {
			"I'm outta here", "You're boring", "Have a nice day", "Bye", "Good night",
			"Nothing to do here", "Taking a break", "Lorem ipsum dolor sit amet…",
			"Nothing can hold me back", "It's getting quiet", "Drop the bazzzzzz",
			"Never gonna give you up", "Never gonna let you down", "Keep rockin' it",
			"?", "c(ꙩ_Ꙩ)ꜿ", "I'll be back", "Your advertisement could be here",
			"connection lost", "disconnected", "Requested by API.",
			"Robert'); DROP TABLE students;--", "It works!! No, wait...",
			"Notice me, senpai", ":wq", "Soon™"
		};

		private bool closed = false;
		private TickWorker reconnectTick = null;
		private int reconnectCounter;
		private ReconnectType? lastReconnect;

		private readonly ConfBot config;
		private readonly Ts3FullClient tsFullClient;
		private IdentityData identity;
		private List<ClientList> clientbuffer;
		private bool clientbufferOutdated = true;
		// dbid -> DbData
		private readonly TimedCache<ulong, ClientDbInfo> clientDbNames;
		// uid -> dbid
		private readonly LruCache<string, ulong> dbIdCache;

		private readonly StallCheckPipe stallCheckPipe;
		private readonly VolumePipe volumePipe;
		private readonly FfmpegProducer ffmpegProducer;
		private readonly PreciseTimedPipe timePipe;
		private readonly PassiveMergePipe mergePipe;
		private readonly EncoderPipe encoderPipe;
		internal CustomTargetPipe TargetPipe { get; }

		public bool Connected => tsFullClient.Connected;
		public ConnectionData ConnectionData => tsFullClient.ConnectionData;

		public Ts3Client(ConfBot config, Ts3FullClient tsFullClient, Id id)
		{
			this.id = id;
			Util.Init(out clientDbNames);
			Util.Init(out clientbuffer);
			dbIdCache = new LruCache<string, ulong>(1024);

			this.tsFullClient = tsFullClient;
			tsFullClient.OnEachTextMessage += ExtendedTextMessage;
			tsFullClient.OnErrorEvent += TsFullClient_OnErrorEvent;
			tsFullClient.OnConnected += TsFullClient_OnConnected;
			tsFullClient.OnDisconnected += TsFullClient_OnDisconnected;

			int ScaleBitrate(int value) => Util.Clamp(value, 1, 255) * 1000;

			this.config = config;
			this.config.Audio.Bitrate.Changed += (s, e) => encoderPipe.Bitrate = ScaleBitrate(e.NewValue);

			ffmpegProducer = new FfmpegProducer(config.GetParent().Tools.Ffmpeg, id);
			stallCheckPipe = new StallCheckPipe();
			volumePipe = new VolumePipe();
			Volume = config.Audio.Volume.Default;
			encoderPipe = new EncoderPipe(SendCodec) { Bitrate = ScaleBitrate(config.Audio.Bitrate) };
			timePipe = new PreciseTimedPipe { ReadBufferSize = encoderPipe.PacketSize };
			timePipe.Initialize(encoderPipe, id);
			TargetPipe = new CustomTargetPipe(tsFullClient);
			mergePipe = new PassiveMergePipe();

			mergePipe.Add(ffmpegProducer);
			mergePipe.Into(timePipe).Chain<CheckActivePipe>().Chain(stallCheckPipe).Chain(volumePipe).Chain(encoderPipe).Chain(TargetPipe);

			identity = null;
		}

		public E<string> Connect()
		{
			// get or compute identity
			var identityConf = config.Connect.Identity;
			if (string.IsNullOrEmpty(identityConf.PrivateKey))
			{
				identity = Ts3Crypt.GenerateNewIdentity();
				identityConf.PrivateKey.Value = identity.PrivateKeyString;
				identityConf.Offset.Value = identity.ValidKeyOffset;
			}
			else
			{
				var identityResult = Ts3Crypt.LoadIdentityDynamic(identityConf.PrivateKey.Value, identityConf.Offset.Value);
				if (!identityResult.Ok)
				{
					Log.Error("The identity from the config file is corrupted. Remove it to generate a new one next start; or try to repair it.");
					return "Corrupted identity";
				}
				identity = identityResult.Value;
				identityConf.PrivateKey.Value = identity.PrivateKeyString;
				identityConf.Offset.Value = identity.ValidKeyOffset;
			}

			// check required security level
			if (identityConf.Level.Value >= 0 && identityConf.Level.Value <= 160)
				UpdateIndentityToSecurityLevel(identityConf.Level.Value);
			else if (identityConf.Level.Value != -1)
				Log.Warn("Invalid config value for 'Level', enter a number between '0' and '160' or '-1' to adapt automatically.");
			config.SaveWhenExists();

			reconnectCounter = 0;
			lastReconnect = null;
			tsFullClient.QuitMessage = QuitMessages[Util.Random.Next(0, QuitMessages.Length)];
			ClearAllCaches();
			return ConnectClient();
		}

		private E<string> ConnectClient()
		{
			StopReconnectTickWorker();
			if (closed)
				return "Bot disposed";

			VersionSign versionSign;
			if (!string.IsNullOrEmpty(config.Connect.ClientVersion.Build.Value))
			{
				var versionConf = config.Connect.ClientVersion;
				versionSign = new VersionSign(versionConf.Build, versionConf.Platform.Value, versionConf.Sign);

				if (!versionSign.CheckValid())
				{
					Log.Warn("Invalid version sign, falling back to unknown :P");
					versionSign = VersionSign.VER_WIN_3_X_X;
				}
			}
			else if (SystemData.IsLinux)
			{
				versionSign = VersionSign.VER_LIN_3_X_X;
			}
			else
			{
				versionSign = VersionSign.VER_WIN_3_X_X;
			}

			try
			{
				var connectionConfig = new ConnectionDataFull
				{
					Username = config.Connect.Name,
					ServerPassword = config.Connect.ServerPassword.Get(),
					Address = config.Connect.Address,
					Identity = identity,
					VersionSign = versionSign,
					DefaultChannel = config.Connect.Channel,
					DefaultChannelPassword = config.Connect.ChannelPassword.Get(),
					LogId = id,
				};
				config.SaveWhenExists();

				tsFullClient.Connect(connectionConfig);
				return R.Ok;
			}
			catch (Ts3Exception qcex)
			{
				Log.Error(qcex, "There is either a problem with your connection configuration, or the bot has not all permissions it needs.");
				return "Connect error";
			}
		}

		private void UpdateIndentityToSecurityLevel(int targetLevel)
		{
			if (Ts3Crypt.GetSecurityLevel(identity) < targetLevel)
			{
				Log.Info("Calculating up to required security level: {0}", targetLevel);
				Ts3Crypt.ImproveSecurity(identity, targetLevel);
				config.Connect.Identity.Offset.Value = identity.ValidKeyOffset;
			}
		}

		private void StopReconnectTickWorker()
		{
			var reconnectTickLocal = reconnectTick;
			reconnectTick = null;
			if (reconnectTickLocal != null)
				TickPool.UnregisterTicker(reconnectTickLocal);
		}

		[Obsolete(AttributeStrings.UnderDevelopment)]
		public void MixInStreamOnce(StreamAudioProducer producer)
		{
			mergePipe.Add(producer);
			producer.HitEnd += (s, e) => mergePipe.Remove(producer);
			timePipe.Paused = false;
		}

		#region Ts3Client functions wrapper

		public E<LocalStr> SendMessage(string message, ushort clientId)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return tsFullClient.SendPrivateMessage(message, clientId).FormatLocal();
		}

		public E<LocalStr> SendChannelMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return tsFullClient.SendChannelMessage(message).FormatLocal();
		}

		public E<LocalStr> SendServerMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return tsFullClient.SendServerMessage(message, 1).FormatLocal();
		}

		public E<LocalStr> KickClientFromServer(ushort clientId) => tsFullClient.KickClientFromServer(new[] { clientId }).FormatLocal();
		public E<LocalStr> KickClientFromChannel(ushort clientId) => tsFullClient.KickClientFromChannel(new[] { clientId }).FormatLocal();

		public E<LocalStr> ChangeDescription(string description)
			=> tsFullClient.ChangeDescription(description, tsFullClient.ClientId).FormatLocal();

		public E<LocalStr> ChangeBadges(string badgesString)
		{
			if (!badgesString.StartsWith("overwolf=") && !badgesString.StartsWith("badges="))
				badgesString = "overwolf=0:badges=" + badgesString;
			return tsFullClient.ChangeBadges(badgesString).FormatLocal();
		}

		public E<LocalStr> ChangeName(string name)
		{
			var result = tsFullClient.ChangeName(name);
			if (result.Ok)
				return R.Ok;

			if (result.Error.Id == Ts3ErrorCode.parameter_invalid_size)
				return new LocalStr(strings.error_ts_invalid_name);
			else
				return result.Error.FormatLocal();
		}

		public R<ClientList, LocalStr> GetCachedClientById(ushort id) => ClientBufferRequest(client => client.ClientId == id);

		public R<ClientList, LocalStr> GetFallbackedClientById(ushort id)
		{
			var result = ClientBufferRequest(client => client.ClientId == id);
			if (result.Ok)
				return result;
			Log.Warn("Slow double request due to missing or wrong permission configuration!");
			var result2 = tsFullClient.Send<ClientList>("clientinfo", new CommandParameter("clid", id)).WrapSingle();
			if (!result2.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			ClientList cd = result2.Value;
			cd.ClientId = id;
			clientbuffer.Add(cd);
			return cd;
		}

		public R<ClientList, LocalStr> GetClientByName(string name)
		{
			var refreshResult = RefreshClientBuffer(false);
			if (!refreshResult)
				return refreshResult.Error;
			var clients = Algorithm.Filter.DefaultFilter.Filter(
				clientbuffer.Select(cb => new KeyValuePair<string, ClientList>(cb.Name, cb)), name).ToArray();
			if (clients.Length <= 0)
				return new LocalStr(strings.error_ts_no_client_found);
			return clients[0].Value;
		}

		private R<ClientList, LocalStr> ClientBufferRequest(Predicate<ClientList> pred)
		{
			var refreshResult = RefreshClientBuffer(false);
			if (!refreshResult)
				return refreshResult.Error;
			var clientData = clientbuffer.Find(pred);
			if (clientData is null)
				return new LocalStr(strings.error_ts_no_client_found);
			return clientData;
		}

		public E<LocalStr> RefreshClientBuffer(bool force)
		{
			if (clientbufferOutdated || force)
			{
				var result = tsFullClient.ClientList(ClientListOptions.uid);
				if (!result)
				{
					Log.Debug("Clientlist failed ({0})", result.Error.ErrorFormat());
					return result.Error.FormatLocal();
				}
				clientbuffer = result.Value.ToList();
				clientbufferOutdated = false;
			}
			return R.Ok;
		}

		public R<ulong[], LocalStr> GetClientServerGroups(ulong dbId)
		{
			var result = tsFullClient.ServerGroupsByClientDbId(dbId);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			return result.Value.Select(csg => csg.ServerGroupId).ToArray();
		}

		public R<ClientDbInfo, LocalStr> GetDbClientByDbId(ulong clientDbId)
		{
			if (clientDbNames.TryGetValue(clientDbId, out var clientData))
				return clientData;

			var result = tsFullClient.ClientDbInfo(clientDbId);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			clientData = result.Value;
			clientDbNames.Set(clientDbId, clientData);
			return clientData;
		}

		public R<ClientInfo, LocalStr> GetClientInfoById(ushort id) => tsFullClient.ClientInfo(id).FormatLocal(() => strings.error_ts_no_client_found);

		public R<ulong, LocalStr> GetClientDbIdByUid(string uid)
		{
			if (dbIdCache.TryGetValue(uid, out var dbid))
				return dbid;

			var result = tsFullClient.GetClientDbIdFromUid(uid);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);

			dbIdCache.Set(result.Value.ClientUid, result.Value.ClientDbId);
			return result.Value.ClientDbId;
		}

		internal bool SetupRights(string key)
		{
			var dbResult = tsFullClient.GetClientDbIdFromUid(identity.ClientUid);
			if (!dbResult.Ok)
			{
				Log.Error("Getting own dbid failed ({0})", dbResult.Error.ErrorFormat());
				return false;
			}
			var myDbId = dbResult.Value.ClientDbId;

			// Check all own server groups
			var getGroupResult = GetClientServerGroups(myDbId);
			var groups = getGroupResult.Ok ? getGroupResult.Value : Array.Empty<ulong>();

			// Add self to master group (via token)
			if (!string.IsNullOrEmpty(key))
			{
				var privKeyUseResult = tsFullClient.PrivilegeKeyUse(key);
				if (!privKeyUseResult.Ok)
				{
					Log.Error("Using privilege key failed ({0})", privKeyUseResult.Error.ErrorFormat());
					return false;
				}
			}

			// Remember new group (or check if in new group at all)
			var groupDiff = Array.Empty<ulong>();
			if (getGroupResult.Ok)
			{
				getGroupResult = GetClientServerGroups(myDbId);
				var groupsNew = getGroupResult.Ok ? getGroupResult.Value : Array.Empty<ulong>();
				groupDiff = groupsNew.Except(groups).ToArray();
			}

			if (config.BotGroupId == 0)
			{
				// Create new Bot group
				var botGroup = tsFullClient.ServerGroupAdd("ServerBot");
				if (botGroup.Ok)
				{
					config.BotGroupId.Value = botGroup.Value.ServerGroupId;

					// Add self to new group
					var grpresult = tsFullClient.ServerGroupAddClient(botGroup.Value.ServerGroupId, myDbId);
					if (!grpresult.Ok)
						Log.Error("Adding group failed ({0})", grpresult.Error.ErrorFormat());
				}
			}

			const int max = 75;
			const int ava = 500000; // max size in bytes for the avatar

			// Add various rights to the bot group
			var permresult = tsFullClient.ServerGroupAddPerm(config.BotGroupId.Value,
				new[] {
					Ts3Permission.i_client_whisper_power, // + Required for whisper channel playing
					Ts3Permission.i_client_private_textmessage_power, // + Communication
					Ts3Permission.b_client_server_textmessage_send, // + Communication
					Ts3Permission.b_client_channel_textmessage_send, // + Communication

					Ts3Permission.b_client_modify_dbproperties, // ? Dont know but seems also required for the next one
					Ts3Permission.b_client_modify_description, // + Used to change the description of our bot
					Ts3Permission.b_client_info_view, // (+) only used as fallback usually
					Ts3Permission.b_virtualserver_client_list, // ? Dont know but seems also required for the next one

					Ts3Permission.i_channel_subscribe_power, // + Required to find user to communicate
					Ts3Permission.b_virtualserver_client_dbinfo, // + Required to get basic user information for history, api, etc...
					Ts3Permission.i_client_talk_power, // + Required for normal channel playing
					Ts3Permission.b_client_modify_own_description, // ? not sure if this makes b_client_modify_description superfluous

					Ts3Permission.b_group_is_permanent, // + Group should stay even if bot disconnects
					Ts3Permission.i_client_kick_from_channel_power, // + Optional for kicking
					Ts3Permission.i_client_kick_from_server_power, // + Optional for kicking
					Ts3Permission.i_client_max_clones_uid, // + In case that bot times out and tries to join again

					Ts3Permission.b_client_ignore_antiflood, // + The bot should be resistent to forced spam attacks
					Ts3Permission.b_channel_join_ignore_password, // + The noble bot will not abuse this power
					Ts3Permission.b_channel_join_permanent, // + Allow joining to all channel even on strict servers
					Ts3Permission.b_channel_join_semi_permanent, // + Allow joining to all channel even on strict servers

					Ts3Permission.b_channel_join_temporary, // + Allow joining to all channel even on strict servers
					Ts3Permission.b_channel_join_ignore_maxclients, // + Allow joining full channels
					Ts3Permission.i_channel_join_power, // + Allow joining to all channel even on strict servers
					Ts3Permission.b_client_permissionoverview_view, // + Scanning through given perms for rights system

					Ts3Permission.i_client_max_avatar_filesize, // + Uploading thumbnails as avatar
					Ts3Permission.b_client_use_channel_commander, // + Enable channel commander
					Ts3Permission.b_client_ignore_bans, // + The bot should be resistent to bans
					Ts3Permission.b_client_ignore_sticky, // + Should skip weird movement restrictions
				},
				new[] {
					max, max,   1,   1,
					  1,   1,   1,   1,
					max,   1, max,   1,
					  1, max, max,   4,
					  1,   1,   1,   1,
					  1,   1, max,   1,
					ava,   1,   1,   1,
				},
				new[] {
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
				},
				new[] {
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
				});

			if (!permresult)
				Log.Error("Adding permissions failed ({0})", permresult.Error.ErrorFormat());

			// Leave master group again
			if (groupDiff.Length > 0)
			{
				foreach (var grp in groupDiff)
				{
					var grpresult = tsFullClient.ServerGroupDelClient(grp, myDbId);
					if (!grpresult.Ok)
						Log.Error("Removing group failed ({0})", grpresult.Error.ErrorFormat());
				}
			}

			return true;
		}

		public E<LocalStr> UploadAvatar(System.IO.Stream stream) => tsFullClient.UploadAvatar(stream).FormatLocal();

		public E<LocalStr> DeleteAvatar() => tsFullClient.DeleteAvatar().FormatLocal();

		public E<LocalStr> MoveTo(ulong channelId, string password = null)
			=> tsFullClient.ClientMove(tsFullClient.ClientId, channelId, password).FormatLocal(() => strings.error_ts_cannot_move);

		public E<LocalStr> SetChannelCommander(bool isCommander)
			=> tsFullClient.ChangeIsChannelCommander(isCommander).FormatLocal(() => strings.error_ts_cannot_set_commander);

		public R<bool, LocalStr> IsChannelCommander()
		{
			var getInfoResult = GetClientInfoById(tsFullClient.ClientId);
			if (!getInfoResult.Ok)
				return getInfoResult.Error;
			return getInfoResult.Value.IsChannelCommander;
		}

		public R<ClientInfo, LocalStr> GetSelf() => tsFullClient.ClientInfo(tsFullClient.ClientId).FormatLocal();

		public void InvalidateClientBuffer() => clientbufferOutdated = true;

		private void ClearAllCaches()
		{
			InvalidateClientBuffer();
			dbIdCache.Clear();
			clientDbNames.Clear();
		}

		#endregion

		#region Events

		private void TsFullClient_OnErrorEvent(object sender, CommandError error)
		{
			switch (error.Id)
			{
			case Ts3ErrorCode.whisper_no_targets:
				stallCheckPipe.SetStall();
				break;

			default:
				Log.Debug("Got ts3 error event: {0}", error.ErrorFormat());
				break;
			}
		}

		private void TsFullClient_OnDisconnected(object sender, DisconnectEventArgs e)
		{
			if (e.Error != null)
			{
				var error = e.Error;
				switch (error.Id)
				{
				case Ts3ErrorCode.client_could_not_validate_identity:
					if (config.Connect.Identity.Level.Value == -1)
					{
						int targetSecLevel = int.Parse(error.ExtraMessage);
						UpdateIndentityToSecurityLevel(targetSecLevel);
						ConnectClient();
						return; // skip triggering event, we want to reconnect
					}
					else
					{
						Log.Warn("The server reported that the security level you set is not high enough." +
							"Increase the value to '{0}' or set it to '-1' to generate it on demand when connecting.", error.ExtraMessage);
					}
					break;

				case Ts3ErrorCode.client_too_many_clones_connected:
					Log.Warn("Seems like another client with the same identity is already connected.");
					if (TryReconnect(ReconnectType.Error))
						return;
					break;

				case Ts3ErrorCode.connect_failed_banned:
					Log.Warn("This bot is banned.");
					if (TryReconnect(ReconnectType.Ban))
						return;
					break;

				default:
					Log.Warn("Could not connect: {0}", error.ErrorFormat());
					if (TryReconnect(ReconnectType.Error))
						return;
					break;
				}
			}
			else
			{
				Log.Debug("Bot disconnected. Reason: {0}", e.ExitReason);

				if (TryReconnect( // TODO c# 8.0 switch expression
						e.ExitReason == Reason.Timeout ? ReconnectType.Timeout :
						e.ExitReason == Reason.KickedFromServer ? ReconnectType.Kick :
						e.ExitReason == Reason.ServerShutdown || e.ExitReason == Reason.ServerStopped ? ReconnectType.ServerShutdown :
						e.ExitReason == Reason.Banned ? ReconnectType.Ban :
						ReconnectType.None))
					return;
			}

			OnBotDisconnect?.Invoke(this, e);
		}

		private bool TryReconnect(ReconnectType type)
		{
			if (closed)
				return false;

			if (lastReconnect != type)
				reconnectCounter = 0;
			lastReconnect = type;

			TimeSpan? delay;
			switch (type)
			{
			case ReconnectType.Timeout: delay = config.Reconnect.OnTimeout.GetValueAsTime(reconnectCounter); break;
			case ReconnectType.Kick: delay = config.Reconnect.OnKick.GetValueAsTime(reconnectCounter); break;
			case ReconnectType.Ban: delay = config.Reconnect.OnBan.GetValueAsTime(reconnectCounter); break;
			case ReconnectType.ServerShutdown: delay = config.Reconnect.OnShutdown.GetValueAsTime(reconnectCounter); break;
			case ReconnectType.Error: delay = config.Reconnect.OnError.GetValueAsTime(reconnectCounter); break;
			case ReconnectType.None:
				return false;
			default: throw Util.UnhandledDefault(type);
			}
			reconnectCounter++;

			if (delay == null)
			{
				Log.Info("Reconnect strategy for '{0}' has reached the end. Closing instance.", type);
				return false;
			}

			Log.Info("Trying to reconnect because of {0}. Delaying reconnect for {1:0} seconds", type, delay.Value.TotalSeconds);
			reconnectTick = TickPool.RegisterTickOnce(() => ConnectClient(), delay);
			return true;
		}

		private void TsFullClient_OnConnected(object sender, EventArgs e)
		{
			StopReconnectTickWorker();
			reconnectCounter = 0;
			lastReconnect = null;
			OnBotConnected?.Invoke(this, EventArgs.Empty);
		}

		private void ExtendedTextMessage(object sender, TextMessage textMessage)
		{
			// Prevent loopback of own textmessages
			if (textMessage.InvokerId == tsFullClient.ClientId)
				return;
			OnMessageReceived?.Invoke(sender, textMessage);
		}

		#endregion

		#region IPlayerConnection

		public event EventHandler OnSongEnd
		{
			add => ffmpegProducer.OnSongEnd += value;
			remove => ffmpegProducer.OnSongEnd -= value;
		}

		public event EventHandler<SongInfoChanged> OnSongUpdated
		{
			add => ffmpegProducer.OnSongUpdated += value;
			remove => ffmpegProducer.OnSongUpdated -= value;
		}

		public E<string> AudioStart(PlayResource res)
		{
			E<string> result;
			if (res is MediaPlayResource mres && mres.IsIcyStream)
				result = ffmpegProducer.AudioStartIcy(res.PlayUri);
			else
				result = ffmpegProducer.AudioStart(res.PlayUri);
			if (result)
				timePipe.Paused = false;
			return result;
		}

		public E<string> AudioStop()
		{
			// TODO clean up all mixins
			timePipe.Paused = true;
			ffmpegProducer.AudioStop();
			return R.Ok;
		}

		public TimeSpan Length => ffmpegProducer.Length;

		public TimeSpan Position
		{
			get => ffmpegProducer.Position;
			set => ffmpegProducer.Position = value;
		}

		public float Volume
		{
			get => AudioValues.FactorToHumanVolume(volumePipe.Volume);
			set => volumePipe.Volume = AudioValues.HumanVolumeToFactor(value);
		}

		public bool Paused
		{
			get => timePipe.Paused;
			set => timePipe.Paused = value;
		}

		public bool Playing => !timePipe.Paused;

		#endregion

		public void Dispose()
		{
			closed = true;
			StopReconnectTickWorker();
			timePipe?.Dispose();
			ffmpegProducer?.Dispose();
			encoderPipe?.Dispose();
			tsFullClient.Dispose();
		}

		enum ReconnectType
		{
			None,
			Timeout,
			Kick,
			Ban,
			ServerShutdown,
			Error
		}
	}

	namespace RExtensions
	{
		internal static class RExtentions
		{
			public static R<T, LocalStr> FormatLocal<T>(this R<T, CommandError> cmdErr, Func<string> prefix = null)
			{
				if (cmdErr.Ok)
					return cmdErr.Value;
				return cmdErr.Error.FormatLocal(prefix);
			}

			public static E<LocalStr> FormatLocal(this E<CommandError> cmdErr, Func<string> prefix = null)
			{
				if (cmdErr.Ok)
					return R.Ok;
				return cmdErr.Error.FormatLocal(prefix);
			}

			public static LocalStr FormatLocal(this CommandError err, Func<string> prefix = null)
			{
				var str = LocalizationManager.GetString("error_ts_code_" + (uint)err.Id)
					?? $"{strings.error_ts_unknown_error} ({err.Message})";

				if (prefix != null)
					str = $"{prefix()} ({str})";
				return new LocalStr(str);
			}
		}
	}
}
