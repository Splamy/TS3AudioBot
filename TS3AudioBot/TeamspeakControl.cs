// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3Client;
	using TS3Client.Commands;
	using TS3Client.Full;
	using TS3Client.Messages;
	using TS3Client.Query;

	public abstract class TeamspeakControl : IDisposable
	{
		public event EventHandler<TextMessage> OnMessageReceived;
		private void ExtendedTextMessage(object sender, IEnumerable<TextMessage> eventArgs)
		{
			if (OnMessageReceived == null) return;
			foreach (var evData in eventArgs)
			{
				if (evData.InvokerId == me.ClientId)
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

		private List<ClientData> clientbuffer;
		private bool clientbufferOutdated = true;
		private IDictionary<ulong, string> clientDbNames;

		protected Ts3BaseFunctions tsBaseClient;
		protected ClientData me;

		protected TeamspeakControl(ClientType connectionType)
		{
			clientDbNames = new Dictionary<ulong, string>();

			if (connectionType == ClientType.Full)
				tsBaseClient = new Ts3FullClient(EventDispatchType.ExtraDispatchThread);
			else if (connectionType == ClientType.Query)
				tsBaseClient = new Ts3QueryClient(EventDispatchType.ExtraDispatchThread);

			tsBaseClient.OnClientLeftView += ExtendedClientLeftView;
			tsBaseClient.OnClientEnterView += ExtendedClientEnterView;
			tsBaseClient.OnTextMessageReceived += ExtendedTextMessage;
			tsBaseClient.OnConnected += OnConnected;
			tsBaseClient.OnDisconnected += OnDisconnected;
		}

		public abstract void Connect();
		protected virtual void OnConnected(object sender, EventArgs e)
		{
			me = GetSelf();
		}

		private void OnDisconnected(object sender, DisconnectEventArgs e)
		{
			Log.Write(Log.Level.Debug, "Bot disconnected. Reason: {0}", e.ExitReason);
		}

		public R SendMessage(string message, ushort clientId)
		{
			if (Ts3String.TokenLength(message) > Ts3String.MaxMsgLength)
				return "The message to send is longer than the maximum of " + Ts3String.MaxMsgLength + " characters";
			try { tsBaseClient.SendMessage(TextMessageTargetMode.Private, clientId, message); return R.OkR; }
			catch (Ts3CommandException ex) { return ex.ErrorStatus.ErrorFormat(); }
		}

		public R SendGlobalMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3String.MaxMsgLength)
				return "The message to send is longer than the maximum of " + Ts3String.MaxMsgLength + " characters";
			try { tsBaseClient.SendMessage(TextMessageTargetMode.Server, 1, message); return R.OkR; }
			catch (Ts3CommandException ex) { return ex.ErrorStatus.ErrorFormat(); }
		}

		public void KickClientFromServer(ushort clientId) => tsBaseClient.KickClientFromServer(new[] { clientId });
		public void KickClientFromChannel(ushort clientId) => tsBaseClient.KickClientFromChannel(new[] { clientId });

		public R ChangeDescription(string description)
		{
			if (me == null)
				return "Internal error (me==null)";

			try { tsBaseClient.ChangeDescription(description, me); return R.OkR; }
			catch (Ts3CommandException ex) { return ex.ErrorStatus.ErrorFormat(); }
		}

		public R ChangeName(string name)
		{
			try
			{
				tsBaseClient.ChangeName(name);
				return R.OkR;
			}
			catch (Ts3CommandException ex)
			{
				if (ex.ErrorStatus.Id == Ts3ErrorCode.parameter_invalid_size)
					return "The new name is too long or invalid";
				else
					return ex.ErrorStatus.ErrorFormat();
			}
		}

		public R<ClientData> GetClientById(ushort id)
		{
			var result = ClientBufferRequest(client => client.ClientId == id);
			if (result.Ok) return result;
			Log.Write(Log.Level.Debug, "Slow double request, due to missing or wrong permission confinguration!");
			ClientData cd;
			try { cd = tsBaseClient.Send<ClientData>("clientinfo", new CommandParameter("clid", id)).FirstOrDefault(); }
			catch (Ts3CommandException) { cd = null; }
			if (cd != null)
			{
				cd.ClientId = id;
				clientbuffer.Add(cd);
				return R<ClientData>.OkR(cd);
			}
			return "No client found";
		}

		public R<ClientData> GetClientByName(string name)
		{
			var refreshResult = RefreshClientBuffer(false);
			if (!refreshResult)
				return refreshResult.Message;
			var clients = CommandSystem.XCommandSystem.FilterList(
				clientbuffer.Select(cb => new KeyValuePair<string, ClientData>(cb.NickName, cb)), name).ToArray();
			if (clients.Length > 0)
				return R<ClientData>.OkR(clients[0].Value);
			else
				return "No Client found";
		}

		private R<ClientData> ClientBufferRequest(Func<ClientData, bool> pred)
		{
			var refreshResult = RefreshClientBuffer(false);
			if (!refreshResult)
				return refreshResult.Message;
			var clientData = clientbuffer.FirstOrDefault(pred);
			if (clientData == null)
				return "No client found";
			return clientData;
		}

		protected abstract ClientData GetSelf();

		public R RefreshClientBuffer(bool force)
		{
			if (clientbufferOutdated || force)
			{
				try { clientbuffer = tsBaseClient.ClientList(ClientListOptions.uid).ToList(); }
				catch (Ts3CommandException ex) { return "Clientlist failed: " + ex.Message; }
				clientbufferOutdated = false;
			}
			return R.OkR;
		}

		public ulong[] GetClientServerGroups(ulong dbId)
		{
			Log.Write(Log.Level.Debug, "QC GetClientServerGroups called");
			return tsBaseClient.ServerGroupsByClientDbId(dbId).Select(csg => csg.ServerGroupId).ToArray();
		}

		public string GetNameByDbId(ulong clientDbId)
		{
			string name;
			if (clientDbNames.TryGetValue(clientDbId, out name))
				return name;

			try
			{
				var response = tsBaseClient.ClientDbInfo(clientDbId);
				name = response?.NickName ?? string.Empty;
				clientDbNames.Add(clientDbId, name);
				return name;
			}
			catch (Ts3CommandException) { return null; }
		}

		public ClientInfo GetClientInfoById(ushort id) => tsBaseClient.ClientInfo(id);

		internal R SetupRights(string key, MainBotData mainBotData)
		{
			try
			{
				// Check all own server groups
				var groups = GetClientServerGroups(me.DatabaseId);

				// Add self to master group (via token)
				if (!string.IsNullOrEmpty(key))
					tsBaseClient.PrivilegeKeyUse(key);

				// Remember new group (or check if in new group at all)
				var groupsNew = GetClientServerGroups(me.DatabaseId);
				var groupDiff = groupsNew.Except(groups).ToArray();

				if (mainBotData.BotGroupId == 0)
				{
					// Create new Bot group
					var botGroup = tsBaseClient.ServerGroupAdd("ServerBot");
					mainBotData.BotGroupId = botGroup.ServerGroupId;

					// Add self to new group
					tsBaseClient.ServerGroupAddClient(botGroup.ServerGroupId, me.DatabaseId);
				}

				const int max = 75;
				// Add various rights to the bot group
				tsBaseClient.ServerGroupAddPerm(mainBotData.BotGroupId,
					new[] {
						PermissionId.i_client_whisper_power, // + Required for whisper channel playing
						PermissionId.i_client_private_textmessage_power, // + Communication
						PermissionId.b_client_server_textmessage_send, // + Communication
						PermissionId.b_client_channel_textmessage_send, // (+) Communication, could be used but not yet

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
					},
					new[] {
						max, max,   1,   1,
						  1,   1,   1,   1,
						max,   1, max,   1,
						  1, max, max,   4,
						  1,   1,   1,   1,
						  1,   1, max,
					},
					new[] {
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
						false, false, false,
					});

				// Leave master group again
				if (groupDiff.Length > 0)
				{
					foreach (var grp in groupDiff)
						tsBaseClient.ServerGroupDelClient(grp, me.DatabaseId);
				}

				return R.OkR;
			}
			catch (Ts3CommandException cex)
			{
				Log.Write(Log.Level.Warning, cex.ErrorStatus.ErrorFormat());
				return "Auto setup failed! (See logs for more details)";
			}
		}

		public void Dispose()
		{
			Log.Write(Log.Level.Info, "Closing QueryConnection...");
			if (tsBaseClient != null)
			{
				tsBaseClient.Dispose();
				tsBaseClient = null;
			}
		}
	}
}
