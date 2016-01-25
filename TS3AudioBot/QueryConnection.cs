namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3AudioBot.Helper;
	using TS3Query;
	using TS3Query.Messages;

	class QueryConnection : IQueryConnection
	{
		public event EventHandler<TextMessage> OnMessageReceived
		{ add { tsClient.OnTextMessageReceived += value; } remove { tsClient.OnTextMessageReceived -= value; } }

		public event EventHandler<ClientEnterView> OnClientConnect;
		private void ExtendedClientEnterView(object sender, ClientEnterView eventArgs)
		{ clientbufferOutdated = true; OnClientConnect?.Invoke(sender, eventArgs); }

		public event EventHandler<ClientLeftView> OnClientDisconnect;
		private void ExtendedClientLeftView(object sender, ClientLeftView eventArgs)
		{ clientbufferOutdated = true; OnClientDisconnect?.Invoke(sender, eventArgs); }

		private IEnumerable<ClientData> clientbuffer;
		private bool clientbufferOutdated = true;
		private IDictionary<ulong, string> clientDbNames;

		private QueryConnectionData connectionData;
		private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(60);

		public TS3QueryClient tsClient { get; private set; }

		public QueryConnection(QueryConnectionData qcd)
		{
			clientDbNames = new Dictionary<ulong, string>();

			connectionData = qcd;
			tsClient = new TS3QueryClient(EventDispatchType.Manual);
		}

		public void Connect()
		{
			if (!tsClient.IsConnected)
			{
				tsClient.Connect(connectionData.host);
				tsClient.Login(connectionData.user, connectionData.passwd);
				tsClient.UseServer(1);
				try { tsClient.ChangeName("TS3AudioBot"); }
				catch (QueryCommandException) { Log.Write(Log.Level.Warning, "TS3AudioBot name already in use!"); }

				tsClient.RegisterNotification(MessageTarget.Server, -1);
				tsClient.RegisterNotification(MessageTarget.Private, -1);
				tsClient.RegisterNotification(RequestTarget.Server, -1);

				tsClient.OnClientLeftView += ExtendedClientLeftView;
				tsClient.OnClientEnterView += ExtendedClientEnterView;

				TickPool.RegisterTick(() => tsClient.WhoAmI(), PingInterval, true);
			}
		}

		private void Diconnect()
		{
			if (tsClient.IsConnected)
				tsClient.Quit();
		}

		public void SendMessage(string message, ClientData client) => tsClient.SendMessage(message, client);
		public void SendGlobalMessage(string message) => tsClient.SendGlobalMessage(message);
		public void KickClientFromServer(ushort clientId) => tsClient.KickClientFromServer(new[] { clientId });
		public void KickClientFromChannel(ushort clientId) => tsClient.KickClientFromChannel(new[] { clientId });

		public ClientData GetClientById(ushort id)
		{
			RefreshClientBuffer(false);
			return clientbuffer.FirstOrDefault(client => client.Id == id);
		}

		public ClientData GetClientByName(string name)
		{
			RefreshClientBuffer(false);
			return clientbuffer.FirstOrDefault(user => user.NickName == name);
		}

		public void RefreshClientBuffer(bool force)
		{
			if (clientbufferOutdated || force)
			{
				clientbuffer = tsClient.ClientList();
				clientbufferOutdated = false;
			}
		}

		public int[] GetClientServerGroups(ClientData client)
		{
			if (client == null)
				throw new ArgumentNullException(nameof(client));

			Log.Write(Log.Level.Debug, "QC GetClientServerGroups called");
			var response = tsClient.Send("servergroupsbyclientid", new Parameter("cldbid", client.DatabaseId));
			if (!response.Any() || !response.First().ContainsKey("sgid"))
				return new int[0];
			return response.Select(dict => int.Parse(dict["sgid"])).ToArray();
		}

		public string GetNameByDbId(ulong clientDbId)
		{
			string name;
			if (!clientDbNames.TryGetValue(clientDbId, out name))
			{
				// TODO move to TS3Query
				var response = tsClient.Send("clientdbinfo", new Parameter("cldbid", clientDbId));
				if (!response.Any() || !response.First().ContainsKey("client_nickname"))
				{
					name = response.First()["client_nickname"] as string;
					if (name != null)
						clientDbNames.Add(clientDbId, name);
				}
				else
				{
					name = string.Empty;
				}
			}
			return name;
		}

		public void Dispose()
		{
			Log.Write(Log.Level.Info, "Closing QueryConnection...");
			if (tsClient != null)
			{
				tsClient.Dispose();
				tsClient = null;
			}
		}
	}

	public struct QueryConnectionData
	{
		[Info("the address of the TeamSpeak3 Query")]
		public string host;
		[Info("the user for the TeamSpeak3 Query")]
		public string user;
		[Info("the password for the TeamSpeak3 Query")]
		public string passwd;
	}
}
