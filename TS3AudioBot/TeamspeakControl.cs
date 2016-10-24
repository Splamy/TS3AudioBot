namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3Client;
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
			if (OnClientDisconnect == null) return;
			foreach (var evData in eventArgs)
			{
				clientbufferOutdated = true;
				OnClientDisconnect?.Invoke(sender, evData);
			}
		}

		protected bool SuppressLoopback { get; set; }

		private IEnumerable<ClientData> clientbuffer;
		private bool clientbufferOutdated = true;
		private IDictionary<ulong, string> clientDbNames;

		protected TS3BaseClient tsBaseClient;
		protected ClientData me;

		public TeamspeakControl(ClientType connectionType)
		{
			clientDbNames = new Dictionary<ulong, string>();

			if (connectionType == ClientType.Full)
				tsBaseClient = new TS3FullClient(EventDispatchType.DoubleThread);
			else if (connectionType == ClientType.Query)
				tsBaseClient = new TS3QueryClient(EventDispatchType.DoubleThread);

			tsBaseClient.OnClientLeftView += ExtendedClientLeftView;
			tsBaseClient.OnClientEnterView += ExtendedClientEnterView;
			tsBaseClient.OnTextMessageReceived += ExtendedTextMessage;
			tsBaseClient.OnConnected += OnConnected;
		}

		public abstract void Connect();
		protected abstract void OnConnected(object sender, EventArgs e);

		public void EnterEventLoop() => tsBaseClient.EnterEventLoop();

		public void SendMessage(string message, ushort clientId) => tsBaseClient.SendMessage(MessageTarget.Private, clientId, message);
		public void SendGlobalMessage(string message) => tsBaseClient.SendMessage(MessageTarget.Server, 1, message);
		public void KickClientFromServer(ushort clientId) => tsBaseClient.KickClientFromServer(new[] { clientId });
		public void KickClientFromChannel(ushort clientId) => tsBaseClient.KickClientFromChannel(new[] { clientId });
		public void ChangeDescription(string description) => tsBaseClient.ChangeDescription(description, me);

		public ClientData GetClientById(ushort id)
		{
			var cd = ClientBufferRequest(client => client.ClientId == id);
			if (cd != null) return cd;
			Log.Write(Log.Level.Warning, "Slow double request, due to missing or wrong permission confinguration!");
			cd = tsBaseClient.Send<ClientData>("clientinfo", new CommandParameter("clid", id)).FirstOrDefault();
			if (cd != null)
			{
				cd.ClientId = id;
				return cd;
			}
			throw new InvalidOperationException();
		}

		public ClientData GetClientByName(string name)
		{
			RefreshClientBuffer(false);
			return CommandSystem.XCommandSystem.FilterList(
				clientbuffer.Select(cb => new KeyValuePair<string, ClientData>(cb.NickName, cb)), name)
				.FirstOrDefault().Value;
		}

		private ClientData ClientBufferRequest(Func<ClientData, bool> pred)
		{
			RefreshClientBuffer(false);
			return clientbuffer.FirstOrDefault(pred);
		}

		public ClientData GetSelf()
		{
			var cd = Generator.ActivateResponse<ClientData>();
			var data = tsBaseClient.WhoAmI();
			cd.ChannelId = data.ChannelId;
			cd.DatabaseId = data.DatabaseId;
			cd.ClientId = data.ClientId;
			cd.NickName = data.NickName;
			cd.ClientType = tsBaseClient.ClientType;
			return cd;
		}

		public void RefreshClientBuffer(bool force)
		{
			if (clientbufferOutdated || force)
			{
				clientbuffer = tsBaseClient.ClientList();
				clientbufferOutdated = false;
			}
		}

		public ulong[] GetClientServerGroups(ClientData client)
		{
			if (client == null)
				throw new ArgumentNullException(nameof(client));

			Log.Write(Log.Level.Debug, "QC GetClientServerGroups called");
			var response = tsBaseClient.ServerGroupsOfClientDbId(client);
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
			catch (TS3CommandException) { return null; }
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
