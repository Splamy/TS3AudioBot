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
	using Helper;
	using Localization;
	using RExtensions;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3Client;
	using TS3Client.Commands;
	using TS3Client.Full;
	using TS3Client.Helper;
	using TS3Client.Messages;
	using TS3Client.Query;

	public abstract class TeamspeakControl : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public event EventHandler<TextMessage> OnMessageReceived;
		private void ExtendedTextMessage(object sender, IEnumerable<TextMessage> eventArgs)
		{
			if (OnMessageReceived == null) return;
			var me = GetSelf();
			foreach (var evData in eventArgs)
			{
				if (me.Ok && evData.InvokerId == me.Value.ClientId)
					continue;
				OnMessageReceived?.Invoke(sender, evData);
			}
		}

		public event EventHandler<ClientEnterView> OnClientConnect;
		private void ExtendedClientEnterView(object sender, IEnumerable<ClientEnterView> eventArgs)
		{
			clientbufferOutdated = true;
			if (OnClientConnect == null) return;
			foreach (var evData in eventArgs)
			{
				clientbufferOutdated = true;
				OnClientConnect?.Invoke(sender, evData);
			}
		}

		public event EventHandler<ClientLeftView> OnClientDisconnect;
		private void ExtendedClientLeftView(object sender, IEnumerable<ClientLeftView> eventArgs)
		{
			clientbufferOutdated = true;
			if (OnClientDisconnect == null) return;
			foreach (var evData in eventArgs)
			{
				clientbufferOutdated = true;
				OnClientDisconnect?.Invoke(sender, evData);
			}
		}

		public abstract event EventHandler<EventArgs> OnBotConnected;
		public abstract event EventHandler<DisconnectEventArgs> OnBotDisconnect;

		private List<ClientData> clientbuffer;
		private bool clientbufferOutdated = true;
		private readonly Cache<ulong, ClientDbData> clientDbNames;

		protected Ts3BaseFunctions tsBaseClient;

		protected TeamspeakControl(ClientType connectionType)
		{
			Util.Init(out clientDbNames);
			Util.Init(out clientbuffer);

			if (connectionType == ClientType.Full)
				tsBaseClient = new Ts3FullClient(EventDispatchType.DoubleThread);
			else if (connectionType == ClientType.Query)
				tsBaseClient = new Ts3QueryClient(EventDispatchType.DoubleThread);

			tsBaseClient.OnClientLeftView += ExtendedClientLeftView;
			tsBaseClient.OnClientEnterView += ExtendedClientEnterView;
			tsBaseClient.OnTextMessageReceived += ExtendedTextMessage;
		}

		public virtual T GetLowLibrary<T>() where T : class
		{
			if (typeof(T) == typeof(Ts3BaseFunctions) && tsBaseClient != null)
				return tsBaseClient as T;
			return null;
		}

		public abstract E<string> Connect();

		public E<LocalStr> SendMessage(string message, ushort clientId)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return tsBaseClient.SendPrivateMessage(message, clientId).FormatLocal();
		}

		public E<LocalStr> SendChannelMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return tsBaseClient.SendChannelMessage(message).FormatLocal();
		}

		public E<LocalStr> SendServerMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return new LocalStr(strings.error_ts_msg_too_long);
			return tsBaseClient.SendServerMessage(message, 1).FormatLocal();
		}

		public E<LocalStr> KickClientFromServer(ushort clientId) => tsBaseClient.KickClientFromServer(new[] { clientId }).FormatLocal();
		public E<LocalStr> KickClientFromChannel(ushort clientId) => tsBaseClient.KickClientFromChannel(new[] { clientId }).FormatLocal();

		public E<LocalStr> ChangeDescription(string description)
		{
			var me = GetSelf();
			if (!me.Ok)
				return LocalStr.Empty;
			return tsBaseClient.ChangeDescription(description, me.Value.ClientId).FormatLocal();
		}

		public E<LocalStr> ChangeBadges(string badgesString)
		{
			if (!badgesString.StartsWith("overwolf=") && !badgesString.StartsWith("badges="))
				badgesString = "overwolf=0:badges=" + badgesString;
			return tsBaseClient.ChangeBadges(badgesString).FormatLocal();
		}

		public E<LocalStr> ChangeName(string name)
		{
			var result = tsBaseClient.ChangeName(name);
			if (result.Ok)
				return R.Ok;

			if (result.Error.Id == Ts3ErrorCode.parameter_invalid_size)
				return new LocalStr(strings.error_ts_invalid_name);
			else
				return result.Error.FormatLocal();
		}

		public R<ClientData, LocalStr> GetClientById(ushort id)
		{
			var result = ClientBufferRequest(client => client.ClientId == id);
			if (result.Ok) return result;
			Log.Warn("Slow double request due to missing or wrong permission configuration!");
			var result2 = tsBaseClient.Send<ClientData>("clientinfo", new CommandParameter("clid", id)).WrapSingle();
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

		private R<ClientData, LocalStr> ClientBufferRequest(Func<ClientData, bool> pred)
		{
			var refreshResult = RefreshClientBuffer(false);
			if (!refreshResult)
				return refreshResult.Error;
			var clientData = clientbuffer.FirstOrDefault(pred);
			if (clientData == null)
				return new LocalStr(strings.error_ts_no_client_found);
			return clientData;
		}

		public abstract R<ClientData> GetSelf();

		public E<LocalStr> RefreshClientBuffer(bool force)
		{
			if (clientbufferOutdated || force)
			{
				var result = tsBaseClient.ClientList(ClientListOptions.uid);
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
			var result = tsBaseClient.ServerGroupsByClientDbId(dbId);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			return result.Value.Select(csg => csg.ServerGroupId).ToArray();
		}

		public R<ClientDbData, LocalStr> GetDbClientByDbId(ulong clientDbId)
		{
			if (clientDbNames.TryGetValue(clientDbId, out var clientData))
				return clientData;

			var result = tsBaseClient.ClientDbInfo(clientDbId);
			if (!result.Ok)
				return new LocalStr(strings.error_ts_no_client_found);
			clientData = result.Value;
			clientDbNames.Store(clientDbId, clientData);
			return clientData;
		}

		public R<ClientInfo, LocalStr> GetClientInfoById(ushort id) => tsBaseClient.ClientInfo(id).FormatLocal(() => strings.error_ts_no_client_found);

		internal bool SetupRights(string key, Config.ConfBot confBot)
		{
			// TODO get own dbid !!!
			var me = GetSelf().Unwrap();

			// Check all own server groups
			var getGroupResult = GetClientServerGroups(me.DatabaseId);
			var groups = getGroupResult.Ok ? getGroupResult.Value : Array.Empty<ulong>();

			// Add self to master group (via token)
			if (!string.IsNullOrEmpty(key))
			{
				var privKeyUseResult = tsBaseClient.PrivilegeKeyUse(key);
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
				getGroupResult = GetClientServerGroups(me.DatabaseId);
				var groupsNew = getGroupResult.Ok ? getGroupResult.Value : Array.Empty<ulong>();
				groupDiff = groupsNew.Except(groups).ToArray();
			}

			if (confBot.BotGroupId == 0)
			{
				// Create new Bot group
				var botGroup = tsBaseClient.ServerGroupAdd("ServerBot");
				if (botGroup.Ok)
				{
					confBot.BotGroupId.Value = (long)botGroup.Value.ServerGroupId;

					// Add self to new group
					var grpresult = tsBaseClient.ServerGroupAddClient(botGroup.Value.ServerGroupId, me.DatabaseId);
					if (!grpresult.Ok)
						Log.Error("Adding group failed ({0})", grpresult.Error.ErrorFormat());
				}
			}

			const int max = 75;
			const int ava = 500000; // max size in bytes for the avatar

			// Add various rights to the bot group
			var permresult = tsBaseClient.ServerGroupAddPerm((ulong)confBot.BotGroupId.Value,
				new[] {
					PermissionId.i_client_whisper_power, // + Required for whisper channel playing
					PermissionId.i_client_private_textmessage_power, // + Communication
					PermissionId.b_client_server_textmessage_send, // + Communication
					PermissionId.b_client_channel_textmessage_send, // + Communication, could be used but not yet

					PermissionId.b_client_modify_dbproperties, // ? Dont know but seems also required for the next one
					PermissionId.b_client_modify_description, // + Used to change the description of our bot
					PermissionId.b_client_info_view, // (+) only used as fallback usually
					PermissionId.b_virtualserver_client_list, // ? Dont know but seems also required for the next one

					PermissionId.i_channel_subscribe_power, // + Required to find user to communicate
					PermissionId.b_virtualserver_client_dbinfo, // + Required to get basic user information for history, api, etc...
					PermissionId.i_client_talk_power, // + Required for normal channel playing
					PermissionId.b_client_modify_own_description, // ? not sure if this makes b_client_modify_description superfluous

					PermissionId.b_group_is_permanent, // + Group should stay even if bot disconnects
					PermissionId.i_client_kick_from_channel_power, // + Optional for kicking
					PermissionId.i_client_kick_from_server_power, // + Optional for kicking
					PermissionId.i_client_max_clones_uid, // + In case that bot times out and tries to join again

					PermissionId.b_client_ignore_antiflood, // + The bot should be resistent to forced spam attacks
					PermissionId.b_channel_join_ignore_password, // + The noble bot will not abuse this power
					PermissionId.b_channel_join_permanent, // + Allow joining to all channel even on strict servers
					PermissionId.b_channel_join_semi_permanent, // + Allow joining to all channel even on strict servers

					PermissionId.b_channel_join_temporary, // + Allow joining to all channel even on strict servers
					PermissionId.b_channel_join_ignore_maxclients, // + Allow joining full channels
					PermissionId.i_channel_join_power, // + Allow joining to all channel even on strict servers
					PermissionId.b_client_permissionoverview_view, // + Scanning through given perms for rights system

					PermissionId.i_client_max_avatar_filesize, // + Uploading thumbnails as avatar
					PermissionId.b_client_use_channel_commander, // + Enable channel commander
				},
				new[] {
					max, max,   1,   1,
					  1,   1,   1,   1,
					max,   1, max,   1,
					  1, max, max,   4,
					  1,   1,   1,   1,
					  1,   1, max,   1,
					ava,   1,
				},
				new[] {
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false,
				},
				new[] {
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false, false, false,
					false, false,
				});

			if (!permresult)
				Log.Error("Adding permissions failed ({0})", permresult.Error.ErrorFormat());

			// Leave master group again
			if (groupDiff.Length > 0)
			{
				foreach (var grp in groupDiff)
				{
					var grpresult = tsBaseClient.ServerGroupDelClient(grp, me.DatabaseId);
					if (!grpresult.Ok)
						Log.Error("Removing group failed ({0})", grpresult.Error.ErrorFormat());
				}
			}

			return true;
		}

		public E<LocalStr> UploadAvatar(System.IO.Stream stream) => tsBaseClient.UploadAvatar(stream).FormatLocal();

		public E<LocalStr> MoveTo(ulong channelId, string password = null)
		{
			var me = GetSelf();
			if (!me.Ok)
				return LocalStr.Empty;
			return tsBaseClient.ClientMove(me.Value.ClientId, channelId, password).FormatLocal(() => strings.error_ts_cannot_move);
		}

		public E<LocalStr> SetChannelCommander(bool isCommander)
		{
			if (!(tsBaseClient is Ts3FullClient tsFullClient))
				return new LocalStr(strings.error_feature_unavailable);
			return tsFullClient.ChangeIsChannelCommander(isCommander).FormatLocal(() => strings.error_ts_cannot_set_commander);
		}
		public R<bool, LocalStr> IsChannelCommander()
		{
			var me = GetSelf();
			if (!me.Ok)
				return LocalStr.Empty;
			var getInfoResult = GetClientInfoById(me.Value.ClientId);
			if (!getInfoResult.Ok)
				return getInfoResult.Error;
			return getInfoResult.Value.IsChannelCommander;
		}

		public virtual void Dispose()
		{
			if (tsBaseClient != null)
			{
				tsBaseClient.Dispose();
				tsBaseClient = null;
			}
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
