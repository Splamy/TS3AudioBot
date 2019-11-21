// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TS3AudioBot.Algorithm;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TSLib;
using TSLib.Commands;
using TSLib.Full;
using TSLib.Helper;
using TSLib.Messages;

namespace TS3AudioBot
{
	public sealed class Ts3Client : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly Id id;

		public event EventHandler OnBotConnected;
		public event EventHandler<DisconnectEventArgs> OnBotDisconnect;
		public event EventHandler<TextMessage> OnMessageReceived;
		public event EventHandler<AloneChanged> OnAloneChanged;
		public event EventHandler OnWhisperNoTarget;

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
		private readonly TsFullClient ts3FullClient;
		private IdentityData identity;
		private List<ClientList> clientbuffer = new List<ClientList>();
		private bool clientbufferOutdated = true;
		private readonly TimedCache<ClientDbId, ClientDbInfo> clientDbNames = new TimedCache<ClientDbId, ClientDbInfo>();
		private readonly LruCache<Uid, ClientDbId> dbIdCache = new LruCache<Uid, ClientDbId>(1024);
		private bool alone = true;
		private ClientId[] ownChannelClients = Array.Empty<ClientId>();

		public bool Connected => ts3FullClient.Connected;
		public ConnectionData ConnectionData => ts3FullClient.ConnectionData;

		public Ts3Client(ConfBot config, TsFullClient ts3FullClient, Id id)
		{
			this.id = id;

			this.ts3FullClient = ts3FullClient;
			ts3FullClient.OnEachTextMessage += ExtendedTextMessage;
			ts3FullClient.OnErrorEvent += TsFullClient_OnErrorEvent;
			ts3FullClient.OnConnected += TsFullClient_OnConnected;
			ts3FullClient.OnDisconnected += TsFullClient_OnDisconnected;
			ts3FullClient.OnEachClientMoved += (s, e) =>
			{
				if (AloneRecheckRequired(e.ClientId, e.TargetChannelId)) IsAloneRecheck();
			};
			ts3FullClient.OnEachClientEnterView += (s, e) =>
			{
				if (AloneRecheckRequired(e.ClientId, e.TargetChannelId)) IsAloneRecheck();
				else if (AloneRecheckRequired(e.ClientId, e.TargetChannelId)) IsAloneRecheck();
			};
			ts3FullClient.OnEachClientLeftView += (s, e) =>
			{
				if (AloneRecheckRequired(e.ClientId, e.TargetChannelId)) IsAloneRecheck();
				else if (AloneRecheckRequired(e.ClientId, e.TargetChannelId)) IsAloneRecheck();
			};

			this.config = config;
			identity = null;
		}

		public E<string> Connect()
		{
			// get or compute identity
			var identityConf = config.Connect.Identity;
			if (string.IsNullOrEmpty(identityConf.PrivateKey))
			{
				identity = TsCrypt.GenerateNewIdentity();
				identityConf.PrivateKey.Value = identity.PrivateKeyString;
				identityConf.Offset.Value = identity.ValidKeyOffset;
			}
			else
			{
				var identityResult = TsCrypt.LoadIdentityDynamic(identityConf.PrivateKey.Value, identityConf.Offset.Value);
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
			ts3FullClient.QuitMessage = Tools.PickRandom(QuitMessages);
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
			else if (Tools.IsLinux)
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

				ts3FullClient.Connect(connectionConfig);
				return R.Ok;
			}
			catch (TsException qcex)
			{
				Log.Error(qcex, "There is either a problem with your connection configuration, or the bot has not all permissions it needs.");
				return "Connect error";
			}
		}

		private void UpdateIndentityToSecurityLevel(int targetLevel)
		{
			if (TsCrypt.GetSecurityLevel(identity) < targetLevel)
			{
				Log.Info("Calculating up to required security level: {0}", targetLevel);
				TsCrypt.ImproveSecurity(identity, targetLevel);
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

		#region TSLib functions wrapper

		public E<LocalStr> SendMessage(string message, ClientId clientId)
		{
			if (TsString.TokenLength(message) > TsConst.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return ts3FullClient.SendPrivateMessage(message, clientId).FormatLocal();
		}

		public E<LocalStr> SendChannelMessage(string message)
		{
			if (TsString.TokenLength(message) > TsConst.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return ts3FullClient.SendChannelMessage(message).FormatLocal();
		}

		public E<LocalStr> SendServerMessage(string message)
		{
			if (TsString.TokenLength(message) > TsConst.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return ts3FullClient.SendServerMessage(message, 1).FormatLocal();
		}

		public E<LocalStr> KickClientFromServer(params ClientId[] clientId) => ts3FullClient.KickClientFromServer(clientId).FormatLocal();
		public E<LocalStr> KickClientFromChannel(params ClientId[] clientId) => ts3FullClient.KickClientFromChannel(clientId).FormatLocal();

		public E<LocalStr> ChangeDescription(string description)
			=> ts3FullClient.ChangeDescription(description, ts3FullClient.ClientId).FormatLocal();

		public E<LocalStr> ChangeBadges(string badgesString)
		{
			if (!badgesString.StartsWith("overwolf=") && !badgesString.StartsWith("badges="))
				badgesString = "overwolf=0:badges=" + badgesString;
			return ts3FullClient.ChangeBadges(badgesString).FormatLocal();
		}

		public E<LocalStr> ChangeName(string name)
		{
			var result = ts3FullClient.ChangeName(name);
			if (result.Ok)
				return R.Ok;

			if (result.Error.Id == TsErrorCode.parameter_invalid_size)
				return new LocalStr(strings.error_ts_invalid_name);
			else
				return result.Error.FormatLocal();
		}

		public R<ClientList, LocalStr> GetCachedClientById(ClientId id) => ClientBufferRequest(client => client.ClientId == id);

		public R<ClientList, LocalStr> GetFallbackedClientById(ClientId id)
		{
			var result = ClientBufferRequest(client => client.ClientId == id);
			if (result.Ok)
				return result;
			Log.Warn("Slow double request due to missing or wrong permission configuration!");
			var result2 = ts3FullClient.Send<ClientList>("clientinfo", new CommandParameter("clid", id)).WrapSingle();
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
			var clients = Filter.DefaultFilter.Filter(
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
				var result = ts3FullClient.ClientList(ClientListOptions.uid);
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

		public R<ServerGroupId[], LocalStr> GetClientServerGroups(ClientDbId dbId)
		{
			var result = ts3FullClient.ServerGroupsByClientDbId(dbId);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			return result.Value.Select(csg => csg.ServerGroupId).ToArray();
		}

		public R<ClientDbInfo, LocalStr> GetDbClientByDbId(ClientDbId clientDbId)
		{
			if (clientDbNames.TryGetValue(clientDbId, out var clientData))
				return clientData;

			var result = ts3FullClient.ClientDbInfo(clientDbId);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			clientData = result.Value;
			clientDbNames.Set(clientDbId, clientData);
			return clientData;
		}

		public R<ClientInfo, LocalStr> GetClientInfoById(ClientId id) => ts3FullClient.ClientInfo(id).FormatLocal(_ => (strings.error_ts_no_client_found, true));

		public R<ClientDbId, LocalStr> GetClientDbIdByUid(Uid uid)
		{
			if (dbIdCache.TryGetValue(uid, out var dbid))
				return dbid;

			var result = ts3FullClient.GetClientDbIdFromUid(uid);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);

			dbIdCache.Set(result.Value.ClientUid, result.Value.ClientDbId);
			return result.Value.ClientDbId;
		}

		internal bool SetupRights(string key)
		{
			var dbResult = ts3FullClient.GetClientDbIdFromUid(identity.ClientUid);
			if (!dbResult.Ok)
			{
				Log.Error("Getting own dbid failed ({0})", dbResult.Error.ErrorFormat());
				return false;
			}
			var myDbId = dbResult.Value.ClientDbId;

			// Check all own server groups
			var getGroupResult = GetClientServerGroups(myDbId);
			var groups = getGroupResult.Ok ? getGroupResult.Value : Array.Empty<ServerGroupId>();

			// Add self to master group (via token)
			if (!string.IsNullOrEmpty(key))
			{
				var privKeyUseResult = ts3FullClient.PrivilegeKeyUse(key);
				if (!privKeyUseResult.Ok)
				{
					Log.Error("Using privilege key failed ({0})", privKeyUseResult.Error.ErrorFormat());
					return false;
				}
			}

			// Remember new group (or check if in new group at all)
			var groupDiff = Array.Empty<ServerGroupId>();
			if (getGroupResult.Ok)
			{
				getGroupResult = GetClientServerGroups(myDbId);
				var groupsNew = getGroupResult.Ok ? getGroupResult.Value : Array.Empty<ServerGroupId>();
				groupDiff = groupsNew.Except(groups).ToArray();
			}

			if (config.BotGroupId == 0)
			{
				// Create new Bot group
				var botGroup = ts3FullClient.ServerGroupAdd("ServerBot");
				if (botGroup.Ok)
				{
					config.BotGroupId.Value = botGroup.Value.ServerGroupId.Value;

					// Add self to new group
					var grpresult = ts3FullClient.ServerGroupAddClient(botGroup.Value.ServerGroupId, myDbId);
					if (!grpresult.Ok)
						Log.Error("Adding group failed ({0})", grpresult.Error.ErrorFormat());
				}
			}

			const int max = 75;
			const int ava = 500000; // max size in bytes for the avatar

			// Add various rights to the bot group
			var permresult = ts3FullClient.ServerGroupAddPerm((ServerGroupId)config.BotGroupId.Value,
				new[] {
					TsPermission.i_client_whisper_power, // + Required for whisper channel playing
					TsPermission.i_client_private_textmessage_power, // + Communication
					TsPermission.b_client_server_textmessage_send, // + Communication
					TsPermission.b_client_channel_textmessage_send, // + Communication

					TsPermission.b_client_modify_dbproperties, // ? Dont know but seems also required for the next one
					TsPermission.b_client_modify_description, // + Used to change the description of our bot
					TsPermission.b_client_info_view, // (+) only used as fallback usually
					TsPermission.b_virtualserver_client_list, // ? Dont know but seems also required for the next one

					TsPermission.i_channel_subscribe_power, // + Required to find user to communicate
					TsPermission.b_virtualserver_client_dbinfo, // + Required to get basic user information for history, api, etc...
					TsPermission.i_client_talk_power, // + Required for normal channel playing
					TsPermission.b_client_modify_own_description, // ? not sure if this makes b_client_modify_description superfluous

					TsPermission.b_group_is_permanent, // + Group should stay even if bot disconnects
					TsPermission.i_client_kick_from_channel_power, // + Optional for kicking
					TsPermission.i_client_kick_from_server_power, // + Optional for kicking
					TsPermission.i_client_max_clones_uid, // + In case that bot times out and tries to join again

					TsPermission.b_client_ignore_antiflood, // + The bot should be resistent to forced spam attacks
					TsPermission.b_channel_join_ignore_password, // + The noble bot will not abuse this power
					TsPermission.b_channel_join_permanent, // + Allow joining to all channel even on strict servers
					TsPermission.b_channel_join_semi_permanent, // + Allow joining to all channel even on strict servers

					TsPermission.b_channel_join_temporary, // + Allow joining to all channel even on strict servers
					TsPermission.b_channel_join_ignore_maxclients, // + Allow joining full channels
					TsPermission.i_channel_join_power, // + Allow joining to all channel even on strict servers
					TsPermission.b_client_permissionoverview_view, // + Scanning through given perms for rights system

					TsPermission.i_client_max_avatar_filesize, // + Uploading thumbnails as avatar
					TsPermission.b_client_use_channel_commander, // + Enable channel commander
					TsPermission.b_client_ignore_bans, // + The bot should be resistent to bans
					TsPermission.b_client_ignore_sticky, // + Should skip weird movement restrictions

					TsPermission.i_client_max_channel_subscriptions, // + Required to find user to communicate
				},
				new[] {
					max, max,   1,   1,
					  1,   1,   1,   1,
					max,   1, max,   1,
					  1, max, max,   4,
					  1,   1,   1,   1,
					  1,   1, max,   1,
					ava,   1,   1,   1,
					 -1,
				},
				new[] {
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false,
				},
				new[] {
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false,
				});

			if (!permresult)
				Log.Error("Adding permissions failed ({0})", permresult.Error.ErrorFormat());

			// Leave master group again
			if (groupDiff.Length > 0)
			{
				foreach (var grp in groupDiff)
				{
					var grpresult = ts3FullClient.ServerGroupDelClient(grp, myDbId);
					if (!grpresult.Ok)
						Log.Error("Removing group failed ({0})", grpresult.Error.ErrorFormat());
				}
			}

			return true;
		}

		public E<LocalStr> UploadAvatar(System.IO.Stream stream) => ts3FullClient.UploadAvatar(stream).FormatLocal(e =>
			(e == TsErrorCode.permission_invalid_size ? strings.error_ts_file_too_big : null, false)
		); // TODO C# 8 switch expressions

		public E<LocalStr> DeleteAvatar() => ts3FullClient.DeleteAvatar().FormatLocal();

		public E<LocalStr> MoveTo(ChannelId channelId, string password = null)
			=> ts3FullClient.ClientMove(ts3FullClient.ClientId, channelId, password).FormatLocal(_ => (strings.error_ts_cannot_move, true));

		public E<LocalStr> SetChannelCommander(bool isCommander)
			=> ts3FullClient.ChangeIsChannelCommander(isCommander).FormatLocal(_ => (strings.error_ts_cannot_set_commander, true));

		public R<bool, LocalStr> IsChannelCommander()
		{
			var getInfoResult = GetClientInfoById(ts3FullClient.ClientId);
			if (!getInfoResult.Ok)
				return getInfoResult.Error;
			return getInfoResult.Value.IsChannelCommander;
		}

		public R<ClientInfo, LocalStr> GetSelf() => ts3FullClient.ClientInfo(ts3FullClient.ClientId).FormatLocal();

		public void InvalidateClientBuffer() => clientbufferOutdated = true;

		private void ClearAllCaches()
		{
			InvalidateClientBuffer();
			dbIdCache.Clear();
			clientDbNames.Clear();
			alone = true;
			ownChannelClients = Array.Empty<ClientId>();
		}

		#endregion

		#region Events

		private void TsFullClient_OnErrorEvent(object sender, CommandError error)
		{
			switch (error.Id)
			{
			case TsErrorCode.whisper_no_targets:
				OnWhisperNoTarget?.Invoke(this, EventArgs.Empty);
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
				case TsErrorCode.client_could_not_validate_identity:
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

				case TsErrorCode.client_too_many_clones_connected:
					Log.Warn("Seems like another client with the same identity is already connected.");
					if (TryReconnect(ReconnectType.Error))
						return;
					break;

				case TsErrorCode.connect_failed_banned:
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
			default: throw Tools.UnhandledDefault(type);
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
			if (textMessage.InvokerId == ts3FullClient.ClientId)
				return;
			OnMessageReceived?.Invoke(sender, textMessage);
		}

		private bool AloneRecheckRequired(ClientId clientId, ChannelId channelId)
			=> ownChannelClients.Contains(clientId) || channelId == ts3FullClient.Book.Self()?.Channel;

		private void IsAloneRecheck()
		{
			var self = ts3FullClient.Book.Self();
			if (self == null)
				return;
			var ownChannel = self.Channel;
			ownChannelClients = ts3FullClient.Book.Clients.Values.Where(c => c.Channel == ownChannel && c != self).Select(c => c.Id).ToArray();
			var newAlone = ownChannelClients.Length == 0;
			if (newAlone != alone)
			{
				alone = newAlone;
				OnAloneChanged?.Invoke(this, new AloneChanged(newAlone));
			}
		}

		#endregion

		public void Dispose()
		{
			closed = true;
			StopReconnectTickWorker();
			ts3FullClient.Dispose();
		}

		private enum ReconnectType
		{
			None,
			Timeout,
			Kick,
			Ban,
			ServerShutdown,
			Error
		}
	}

	public class AloneChanged : EventArgs
	{
		public bool Alone { get; }

		public AloneChanged(bool alone)
		{
			Alone = alone;
		}
	}

	internal static class CommandErrorExtentions
	{
		public static R<T, LocalStr> FormatLocal<T>(this R<T, CommandError> cmdErr, Func<TsErrorCode, (string loc, bool msg)> prefix = null)
		{
			if (cmdErr.Ok)
				return cmdErr.Value;
			return cmdErr.Error.FormatLocal(prefix);
		}

		public static E<LocalStr> FormatLocal(this E<CommandError> cmdErr, Func<TsErrorCode, (string loc, bool msg)> prefix = null)
		{
			if (cmdErr.Ok)
				return R.Ok;
			return cmdErr.Error.FormatLocal(prefix);
		}

		public static LocalStr FormatLocal(this CommandError err, Func<TsErrorCode, (string loc, bool msg)> prefix = null)
		{
			var strb = new StringBuilder();
			bool msg = true;

			if (prefix != null)
			{
				string prefixStr;
				(prefixStr, msg) = prefix(err.Id);
				if (prefixStr != null)
				{
					strb.Append(prefixStr);
				}
			}

			if (strb.Length == 0)
			{
				strb.Append(strings.error_ts_unknown_error);
			}

			if (msg)
			{
				if (strb.Length > 0)
					strb.Append(" (");
				var localStr = LocalizationManager.GetString("error_ts_code_" + (uint)err.Id);
				if (localStr != null)
					strb.Append(localStr);
				else
					strb.Append(err.Message);
				strb.Append(')');
			}

			if (err.MissingPermissionId != TsPermission.undefined)
				strb.Append(" (").Append(err.MissingPermissionId).Append(')');

			return new LocalStr(strb.ToString());
		}
	}
}
