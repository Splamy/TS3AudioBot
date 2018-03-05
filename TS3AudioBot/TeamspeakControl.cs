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
	using RExtensions;
	using Helper;
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
			foreach (var evData in eventArgs)
			{
				var me = GetSelf();
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

		public event EventHandler<EventArgs> OnBotConnected;
		public abstract event EventHandler OnBotDisconnect;

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
			tsBaseClient.OnConnected += OnBotConnected;
		}

		public virtual T GetLowLibrary<T>() where T : class
		{
			if (typeof(T) == typeof(Ts3BaseFunctions) && tsBaseClient != null)
				return tsBaseClient as T;
			return null;
		}

		public abstract void Connect();

		public R SendMessage(string message, ushort clientId)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return "The message to send is longer than the maximum of " + Ts3Const.MaxSizeTextMessage + " characters";
			return tsBaseClient.SendPrivateMessage(message, clientId).ToR(Extensions.ErrorFormat);
		}

		public R SendChannelMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return "The message to send is longer than the maximum of " + Ts3Const.MaxSizeTextMessage + " characters";
			return tsBaseClient.SendChannelMessage(message).ToR(Extensions.ErrorFormat);
		}

		public R SendServerMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3Const.MaxSizeTextMessage)
				return "The message to send is longer than the maximum of " + Ts3Const.MaxSizeTextMessage + " characters";
			return tsBaseClient.SendServerMessage(message, 1).ToR(Extensions.ErrorFormat);
		}

		public R KickClientFromServer(ushort clientId) => tsBaseClient.KickClientFromServer(new[] { clientId }).ToR(Extensions.ErrorFormat);
		public R KickClientFromChannel(ushort clientId) => tsBaseClient.KickClientFromChannel(new[] { clientId }).ToR(Extensions.ErrorFormat);

		public R ChangeDescription(string description)
		{
			var me = GetSelf();
			if (!me.Ok)
				return "Internal error (me==null)";

			return tsBaseClient.ChangeDescription(description, me.Value.ClientId).ToR(Extensions.ErrorFormat);
		}

		public R ChangeBadges(string badgesString) => tsBaseClient.ChangeBadges(badgesString).ToR(Extensions.ErrorFormat);

		public R ChangeName(string name)
		{
			var result = tsBaseClient.ChangeName(name);
			if (result.Ok)
				return R.OkR;

			if (result.Error.Id == Ts3ErrorCode.parameter_invalid_size)
				return "The new name is too long or invalid";
			else
				return result.Error.ErrorFormat();
		}

		public R<ClientData> GetClientById(ushort id)
		{
			var result = ClientBufferRequest(client => client.ClientId == id);
			if (result.Ok) return result;
			Log.Debug("Slow double request due to missing or wrong permission configuration!");
			var result2 = tsBaseClient.Send<ClientData>("clientinfo", new CommandParameter("clid", id)).WrapSingle();
			if (!result2.Ok)
				return "No client found";
			ClientData cd = result2.Value;
			cd.ClientId = id;
			clientbuffer.Add(cd);
			return cd;
		}

		public R<ClientData> GetClientByName(string name)
		{
			var refreshResult = RefreshClientBuffer(false);
			if (!refreshResult)
				return refreshResult.Error;
			var clients = CommandSystem.XCommandSystem.FilterList(
				clientbuffer.Select(cb => new KeyValuePair<string, ClientData>(cb.Name, cb)), name).ToArray();
			if (clients.Length <= 0)
				return "No client found";
			return clients[0].Value;
		}

		private R<ClientData> ClientBufferRequest(Func<ClientData, bool> pred)
		{
			var refreshResult = RefreshClientBuffer(false);
			if (!refreshResult)
				return refreshResult.Error;
			var clientData = clientbuffer.FirstOrDefault(pred);
			if (clientData == null)
				return "No client found";
			return clientData;
		}

		public abstract R<ClientData> GetSelf();

		public R RefreshClientBuffer(bool force)
		{
			if (clientbufferOutdated || force)
			{
				var result = tsBaseClient.ClientList(ClientListOptions.uid);
				if (!result)
					return $"Clientlist failed ({result.Error.ErrorFormat()})";
				clientbuffer = result.Value.ToList();
				clientbufferOutdated = false;
			}
			return R.OkR;
		}

		public R<ulong[]> GetClientServerGroups(ulong dbId)
		{
			var result = tsBaseClient.ServerGroupsByClientDbId(dbId);
			if (!result.Ok)
				return "No client found.";
			return result.Value.Select(csg => csg.ServerGroupId).ToArray();
		}

		public R<ClientDbData> GetDbClientByDbId(ulong clientDbId)
		{
			if (clientDbNames.TryGetValue(clientDbId, out var clientData))
				return clientData;

			var result = tsBaseClient.ClientDbInfo(clientDbId);
			if (!result.Ok)
				return "No client found.";
			clientData = result.Value;
			clientDbNames.Store(clientDbId, clientData);
			return clientData;
		}

		public R<ClientInfo> GetClientInfoById(ushort id) => tsBaseClient.ClientInfo(id).ToR("No client found.");

		internal R SetupRights(string key, MainBotData mainBotData)
		{
			var me = GetSelf();
			if (!me.Ok)
				return me.Error;

			// Check all own server groups
			var result = GetClientServerGroups(me.Value.DatabaseId);
			var groups = result.Ok ? result.Value : Array.Empty<ulong>();

			// Add self to master group (via token)
			if (!string.IsNullOrEmpty(key))
				tsBaseClient.PrivilegeKeyUse(key);

			// Remember new group (or check if in new group at all)
			if (result.Ok)
				result = GetClientServerGroups(me.Value.DatabaseId);
			var groupsNew = result.Ok ? result.Value : Array.Empty<ulong>();
			var groupDiff = groupsNew.Except(groups).ToArray();

			if (mainBotData.BotGroupId == 0)
			{
				// Create new Bot group
				var botGroup = tsBaseClient.ServerGroupAdd("ServerBot");
				if (botGroup.Ok)
				{
					mainBotData.BotGroupId = botGroup.Value.ServerGroupId;

					// Add self to new group
					tsBaseClient.ServerGroupAddClient(botGroup.Value.ServerGroupId, me.Value.DatabaseId);
				}
			}

			const int max = 75;
			const int ava = 500000; // max size in bytes for the avatar

			// Add various rights to the bot group
			var permresult = tsBaseClient.ServerGroupAddPerm(mainBotData.BotGroupId,
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

			// Leave master group again
			if (groupDiff.Length > 0)
			{
				foreach (var grp in groupDiff)
					tsBaseClient.ServerGroupDelClient(grp, me.Value.DatabaseId);
			}

			if (!result)
			{
				Log.Warn(permresult.Error.ErrorFormat());
				return "Auto setup failed! (See logs for more details)";
			}

			return R.OkR;
		}

		public R UploadAvatar(System.IO.Stream stream) => tsBaseClient.UploadAvatar(stream).ToR(Extensions.ErrorFormat);

		public R MoveTo(ulong channelId, string password = null)
		{
			var me = GetSelf();
			if (!me.Ok)
				return me.Error;
			return tsBaseClient.ClientMove(me.Value.ClientId, channelId, password).ToR("Cannot move there.");
		}

		public R SetChannelCommander(bool isCommander)
		{
			if (!(tsBaseClient is Ts3FullClient tsFullClient))
				return "Commander mode not available";
			return tsFullClient.ChangeIsChannelCommander(isCommander).ToR("Cannot set commander mode");
		}
		public R<bool> IsChannelCommander()
		{
			var me = GetSelf();
			if (!me.Ok)
				return me.Error;
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
		static class RExtentions
		{
			public static R ToR<TE>(this E<TE> result, string message)
			{
				if (!result.Ok)
					return R.Err(message);
				else
					return R.OkR;
			}

			public static R ToR<TE>(this E<TE> result, Func<TE, string> fromError)
			{
				if (!result.Ok)
					return R.Err(fromError(result.Error));
				else
					return R.OkR;
			}

			public static R<T> ToR<T, TE>(this R<T, TE> result, string message)
			{
				if (!result.Ok)
					return R<T>.Err(message);
				else
					return R<T>.OkR(result.Value);
			}

			public static R<T> ToR<T, TE>(this R<T, TE> result, Func<TE, string> fromError)
			{
				if (!result.Ok)
					return R<T>.Err(fromError(result.Error));
				else
					return R<T>.OkR(result.Value);
			}
		}
	}
}
