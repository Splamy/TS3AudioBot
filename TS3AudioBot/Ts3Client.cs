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
	using TS3Client;
	using TS3Client.Audio;
	using TS3Client.Commands;
	using TS3Client.Full;
	using TS3Client.Helper;
	using TS3Client.Messages;

	public sealed class Ts3Client : IPlayerConnection, IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const Codec SendCodec = Codec.OpusMusic;

		public event EventHandler<EventArgs> OnBotConnected;
		public event EventHandler<DisconnectEventArgs> OnBotDisconnect;
		public event EventHandler<TextMessage> OnMessageReceived;
		public event EventHandler<ClientEnterView> OnClientConnect;
		public event EventHandler<ClientLeftView> OnClientDisconnect;

		private static readonly string[] QuitMessages = {
			"I'm outta here", "You're boring", "Have a nice day", "Bye", "Good night",
			"Nothing to do here", "Taking a break", "Lorem ipsum dolor sit amet…",
			"Nothing can hold me back", "It's getting quiet", "Drop the bazzzzzz",
			"Never gonna give you up", "Never gonna let you down", "Keep rockin' it",
			"?", "c(ꙩ_Ꙩ)ꜿ", "I'll be back", "Your advertisement could be here",
			"connection lost", "disconnected", "Requested by API.",
			"Robert'); DROP TABLE students;--", "It works!! No, wait...",
			"Notice me, senpai", ":wq"
		};

		private bool closed = false;
		private TickWorker reconnectTick = null;
		public static readonly TimeSpan TooManyClonesReconnectDelay = TimeSpan.FromSeconds(30);
		private int reconnectCounter;
		private static readonly TimeSpan[] LostConnectionReconnectDelay = new[] {
			TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10),
			TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5) };
		private static int MaxReconnects { get; } = LostConnectionReconnectDelay.Length;

		private readonly ConfBot config;
		internal Ts3FullClient TsFullClient { get; }
		private IdentityData identity;
		private List<ClientData> clientbuffer;
		private bool clientbufferOutdated = true;
		private readonly Cache<ulong, ClientDbData> clientDbNames;

		private readonly StallCheckPipe stallCheckPipe;
		private readonly VolumePipe volumePipe;
		private readonly FfmpegProducer ffmpegProducer;
		private readonly PreciseTimedPipe timePipe;
		private readonly PassiveMergePipe mergePipe;
		private readonly EncoderPipe encoderPipe;
		internal CustomTargetPipe TargetPipe { get; }

		public Ts3Client(ConfBot config)
		{
			Util.Init(out clientDbNames);
			Util.Init(out clientbuffer);

			TsFullClient = new Ts3FullClient(EventDispatchType.DoubleThread);
			TsFullClient.OnClientLeftView += ExtendedClientLeftView;
			TsFullClient.OnClientEnterView += ExtendedClientEnterView;
			TsFullClient.OnTextMessage += ExtendedTextMessage;
			TsFullClient.OnErrorEvent += TsFullClient_OnErrorEvent;
			TsFullClient.OnConnected += TsFullClient_OnConnected;
			TsFullClient.OnDisconnected += TsFullClient_OnDisconnected;

			int ScaleBitrate(int value) => Math.Min(Math.Max(1, value), 255) * 1000;

			this.config = config;
			this.config.Audio.Bitrate.Changed += (s, e) => encoderPipe.Bitrate = ScaleBitrate(e.NewValue);

			ffmpegProducer = new FfmpegProducer(config.GetParent().Tools.Ffmpeg);
			stallCheckPipe = new StallCheckPipe();
			volumePipe = new VolumePipe();
			Volume = config.Audio.Volume.Default;
			encoderPipe = new EncoderPipe(SendCodec) { Bitrate = ScaleBitrate(config.Audio.Bitrate) };
			timePipe = new PreciseTimedPipe { ReadBufferSize = encoderPipe.PacketSize };
			timePipe.Initialize(encoderPipe);
			TargetPipe = new CustomTargetPipe(TsFullClient);
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

			TsFullClient.QuitMessage = QuitMessages[Util.Random.Next(0, QuitMessages.Length)];
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
				versionSign = VersionSign.VER_LIN_3_2_2;
			}
			else
			{
				versionSign = VersionSign.VER_WIN_3_2_2;
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
				};
				config.SaveWhenExists();

				TsFullClient.Connect(connectionConfig);
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
			return TsFullClient.SendPrivateMessage(message, clientId).FormatLocal();
		}

		public E<LocalStr> SendChannelMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return TsFullClient.SendChannelMessage(message).FormatLocal();
		}

		public E<LocalStr> SendServerMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return TsFullClient.SendServerMessage(message, 1).FormatLocal();
		}

		public E<LocalStr> KickClientFromServer(ushort clientId) => TsFullClient.KickClientFromServer(new[] { clientId }).FormatLocal();
		public E<LocalStr> KickClientFromChannel(ushort clientId) => TsFullClient.KickClientFromChannel(new[] { clientId }).FormatLocal();

		public E<LocalStr> ChangeDescription(string description)
			=> TsFullClient.ChangeDescription(description, TsFullClient.ClientId).FormatLocal();

		public E<LocalStr> ChangeBadges(string badgesString)
		{
			if (!badgesString.StartsWith("overwolf=") && !badgesString.StartsWith("badges="))
				badgesString = "overwolf=0:badges=" + badgesString;
			return TsFullClient.ChangeBadges(badgesString).FormatLocal();
		}

		public E<LocalStr> ChangeName(string name)
		{
			var result = TsFullClient.ChangeName(name);
			if (result.Ok)
				return R.Ok;

			if (result.Error.Id == Ts3ErrorCode.parameter_invalid_size)
				return new LocalStr(strings.error_ts_invalid_name);
			else
				return result.Error.FormatLocal();
		}

		public R<ClientData, LocalStr> GetCachedClientById(ushort id) => ClientBufferRequest(client => client.ClientId == id);

		public R<ClientData, LocalStr> GetFallbackedClientById(ushort id)
		{
			var result = ClientBufferRequest(client => client.ClientId == id);
			if (result.Ok)
				return result;
			Log.Warn("Slow double request due to missing or wrong permission configuration!");
			var result2 = TsFullClient.Send<ClientData>("clientinfo", new CommandParameter("clid", id)).WrapSingle();
			if (!result2.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			ClientData cd = result2.Value;
			cd.ClientId = id;
			clientbuffer.Add(cd);
			return cd;
		}

		public R<ClientData, LocalStr> GetClientByName(string name)
		{
			var refreshResult = RefreshClientBuffer(false);
			if (!refreshResult)
				return refreshResult.Error;
			var clients = Algorithm.Filter.DefaultAlgorithm.Filter(
				clientbuffer.Select(cb => new KeyValuePair<string, ClientData>(cb.Name, cb)), name).ToArray();
			if (clients.Length <= 0)
				return new LocalStr(strings.error_ts_no_client_found);
			return clients[0].Value;
		}

		private R<ClientData, LocalStr> ClientBufferRequest(Predicate<ClientData> pred)
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
				var result = TsFullClient.ClientList(ClientListOptions.uid);
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
			var result = TsFullClient.ServerGroupsByClientDbId(dbId);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			return result.Value.Select(csg => csg.ServerGroupId).ToArray();
		}

		public R<ClientDbData, LocalStr> GetDbClientByDbId(ulong clientDbId)
		{
			if (clientDbNames.TryGetValue(clientDbId, out var clientData))
				return clientData;

			var result = TsFullClient.ClientDbInfo(clientDbId);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			clientData = result.Value;
			clientDbNames.Store(clientDbId, clientData);
			return clientData;
		}

		public R<ClientInfo, LocalStr> GetClientInfoById(ushort id) => TsFullClient.ClientInfo(id).FormatLocal(() => strings.error_ts_no_client_found);

		internal bool SetupRights(string key)
		{
			// TODO get own dbid !!!
			var dbResult = TsFullClient.ClientGetDbIdFromUid(identity.ClientUid);
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
				var privKeyUseResult = TsFullClient.PrivilegeKeyUse(key);
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
				var botGroup = TsFullClient.ServerGroupAdd("ServerBot");
				if (botGroup.Ok)
				{
					config.BotGroupId.Value = botGroup.Value.ServerGroupId;

					// Add self to new group
					var grpresult = TsFullClient.ServerGroupAddClient(botGroup.Value.ServerGroupId, myDbId);
					if (!grpresult.Ok)
						Log.Error("Adding group failed ({0})", grpresult.Error.ErrorFormat());
				}
			}

			const int max = 75;
			const int ava = 500000; // max size in bytes for the avatar

			// Add various rights to the bot group
			var permresult = TsFullClient.ServerGroupAddPerm(config.BotGroupId.Value,
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
				},
				new[] {
					max, max,   1,   1,
					  1,   1,   1,   1,
					max,   1, max,   1,
					  1, max, max,   4,
					  1,   1,   1,   1,
					  1,   1, max,   1,
					ava,   1,   1,
				},
				new[] {
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false,
				},
				new[] {
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false,
				});

			if (!permresult)
				Log.Error("Adding permissions failed ({0})", permresult.Error.ErrorFormat());

			// Leave master group again
			if (groupDiff.Length > 0)
			{
				foreach (var grp in groupDiff)
				{
					var grpresult = TsFullClient.ServerGroupDelClient(grp, myDbId);
					if (!grpresult.Ok)
						Log.Error("Removing group failed ({0})", grpresult.Error.ErrorFormat());
				}
			}

			return true;
		}

		public E<LocalStr> UploadAvatar(System.IO.Stream stream) => TsFullClient.UploadAvatar(stream).FormatLocal();

		public E<LocalStr> DeleteAvatar() => TsFullClient.DeleteAvatar().FormatLocal();

		public E<LocalStr> MoveTo(ulong channelId, string password = null)
			=> TsFullClient.ClientMove(TsFullClient.ClientId, channelId, password).FormatLocal(() => strings.error_ts_cannot_move);

		public E<LocalStr> SetChannelCommander(bool isCommander)
			=> TsFullClient.ChangeIsChannelCommander(isCommander).FormatLocal(() => strings.error_ts_cannot_set_commander);

		public R<bool, LocalStr> IsChannelCommander()
		{
			var getInfoResult = GetClientInfoById(TsFullClient.ClientId);
			if (!getInfoResult.Ok)
				return getInfoResult.Error;
			return getInfoResult.Value.IsChannelCommander;
		}

		public R<ClientInfo, LocalStr> GetSelf() => TsFullClient.ClientInfo(TsFullClient.ClientId).FormatLocal();

		public void InvalidateClientBuffer() => clientbufferOutdated = true;

		#endregion

		#region Event helper

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
					if (reconnectCounter++ < MaxReconnects)
					{
						Log.Warn("Seems like another client with the same identity is already connected. Waiting {0:0} seconds to reconnect.",
							TooManyClonesReconnectDelay.TotalSeconds);
						reconnectTick = TickPool.RegisterTickOnce(() => ConnectClient(), TooManyClonesReconnectDelay);
						return; // skip triggering event, we want to reconnect
					}
					break;

				default:
					Log.Warn("Could not connect: {0}", error.ErrorFormat());
					break;
				}
			}
			else
			{
				Log.Debug("Bot disconnected. Reason: {0}", e.ExitReason);

				if (reconnectCounter < LostConnectionReconnectDelay.Length && !closed)
				{
					var delay = LostConnectionReconnectDelay[reconnectCounter++];
					Log.Info("Trying to reconnect. Delaying reconnect for {0:0} seconds", delay.TotalSeconds);
					reconnectTick = TickPool.RegisterTickOnce(() => ConnectClient(), delay);
					return;
				}
			}

			if (reconnectCounter >= LostConnectionReconnectDelay.Length)
			{
				Log.Warn("Could not (re)connect after {0} tries. Giving up.", reconnectCounter);
			}
			OnBotDisconnect?.Invoke(this, e);
		}

		private void TsFullClient_OnConnected(object sender, EventArgs e)
		{
			StopReconnectTickWorker();
			reconnectCounter = 0;
			OnBotConnected?.Invoke(this, EventArgs.Empty);
		}

		private void ExtendedTextMessage(object sender, IEnumerable<TextMessage> eventArgs)
		{
			if (OnMessageReceived is null) return;
			foreach (var evData in eventArgs)
			{
				// Prevent loopback of own textmessages
				if (evData.InvokerId == TsFullClient.ClientId)
					continue;
				OnMessageReceived?.Invoke(sender, evData);
			}
		}

		private void ExtendedClientEnterView(object sender, IEnumerable<ClientEnterView> eventArgs)
		{
			clientbufferOutdated = true;
			if (OnClientConnect is null) return;
			foreach (var evData in eventArgs)
			{
				clientbufferOutdated = true;
				OnClientConnect?.Invoke(sender, evData);
			}
		}

		private void ExtendedClientLeftView(object sender, IEnumerable<ClientLeftView> eventArgs)
		{
			clientbufferOutdated = true;
			if (OnClientDisconnect is null) return;
			foreach (var evData in eventArgs)
			{
				clientbufferOutdated = true;
				OnClientDisconnect?.Invoke(sender, evData);
			}
		}

		#endregion

		#region IPlayerConnection

		public event EventHandler OnSongEnd
		{
			add => ffmpegProducer.OnSongEnd += value;
			remove => ffmpegProducer.OnSongEnd -= value;
		}

		public E<string> AudioStart(string url)
		{
			var result = ffmpegProducer.AudioStart(url);
			if (result)
				timePipe.Paused = false;
			return result;
		}

		public E<string> AudioStop()
		{
			// TODO clean up all mixins
			timePipe.Paused = true;
			return ffmpegProducer.AudioStop();
		}

		public TimeSpan Length => ffmpegProducer.Length;

		public TimeSpan Position
		{
			get => ffmpegProducer.Position;
			set => ffmpegProducer.Position = value;
		}

		public float Volume
		{
			get => volumePipe.Volume * AudioValues.MaxVolume;
			set
			{
				if (value < 0)
					volumePipe.Volume = 0;
				else if (value > AudioValues.MaxVolume)
					volumePipe.Volume = AudioValues.MaxVolume;
				else
					volumePipe.Volume = value / AudioValues.MaxVolume;
			}
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
			TsFullClient.Dispose();
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
