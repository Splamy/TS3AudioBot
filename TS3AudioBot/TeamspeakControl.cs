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
				if (SuppressLoopback && evData.InvokerId == me.ClientId)
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

		protected bool SuppressLoopback { get; set; }

		private List<ClientData> clientbuffer;
		private bool clientbufferOutdated = true;
		private IDictionary<ulong, string> clientDbNames;

		protected Ts3BaseClient tsBaseClient;
		protected ClientData me;

		public TeamspeakControl(ClientType connectionType)
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
		}

		public abstract void Connect();
		protected virtual void OnConnected(object sender, EventArgs e)
		{
			me = GetSelf();
		}

		public R SendMessage(string message, ushort clientId)
		{
			if (Ts3String.TokenLength(message) > Ts3String.MaxMsgLength)
				return "The message to send is longer than the maximum of " + Ts3String.MaxMsgLength + " characters";
			try { tsBaseClient.SendMessage(MessageTarget.Private, clientId, message); return R.OkR; }
			catch (Ts3CommandException ex) { return ex.ErrorStatus.ErrorFormat(); }
		}

		public R SendGlobalMessage(string message)
		{
			if (Ts3String.TokenLength(message) > Ts3String.MaxMsgLength)
				return "The message to send is longer than the maximum of " + Ts3String.MaxMsgLength + " characters";
			try { tsBaseClient.SendMessage(MessageTarget.Server, 1, message); return R.OkR; }
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
				if (ex.ErrorStatus.Id == 1541)
					return "The new name is too long or invalid";
				else
					return ex.ErrorStatus.ErrorFormat();
			}
		}

		public R<ClientData> GetClientById(ushort id)
		{
			var cd = ClientBufferRequest(client => client.ClientId == id);
			if (cd != null) return R<ClientData>.OkR(cd);
			Log.Write(Log.Level.Debug, "Slow double request, due to missing or wrong permission confinguration!");
			cd = tsBaseClient.Send<ClientData>("clientinfo", new CommandParameter("clid", id)).FirstOrDefault();
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
			RefreshClientBuffer(false);
			var clients = CommandSystem.XCommandSystem.FilterList(
				clientbuffer.Select(cb => new KeyValuePair<string, ClientData>(cb.NickName, cb)), name).ToArray();
			if (clients.Length > 0)
				return R<ClientData>.OkR(clients[0].Value);
			else
				return "No Client found";
		}

		private ClientData ClientBufferRequest(Func<ClientData, bool> pred)
		{
			RefreshClientBuffer(false);
			return clientbuffer.FirstOrDefault(pred);
		}

		public abstract ClientData GetSelf();

		public void RefreshClientBuffer(bool force)
		{
			if (clientbufferOutdated || force)
			{
				clientbuffer = tsBaseClient.ClientList(ClientListOptions.uid).ToList();
				clientbufferOutdated = false;
			}
		}

		public ulong[] GetClientServerGroups(ulong dbId)
		{
			Log.Write(Log.Level.Debug, "QC GetClientServerGroups called");
			var response = tsBaseClient.ServerGroupsOfClientDbId(dbId);
			if (!response.Any())
				return new ulong[0];
			return response.Select(csg => csg.ServerGroupId).ToArray();
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
