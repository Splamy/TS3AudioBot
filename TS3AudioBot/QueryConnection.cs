namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3AudioBot.Helper;
	using TS3Query;
	using TS3Query.Messages;

	public class QueryConnection : MarshalByRefObject, IDisposable
	{
		public event EventHandler<TextMessage> OnMessageReceived;
		private void ExtendedTextMessage(object sender, TextMessage eventArgs)
		{
			if (connectionData.suppressLoopback && eventArgs.InvokerId == me.ClientId)
				return;
			OnMessageReceived?.Invoke(sender, eventArgs);
		}

		public event EventHandler<ClientEnterView> OnClientConnect;
		private void ExtendedClientEnterView(object sender, ClientEnterView eventArgs)
		{
			clientbufferOutdated = true;
			OnClientConnect?.Invoke(sender, eventArgs);
		}

		public event EventHandler<ClientLeftView> OnClientDisconnect;
		private void ExtendedClientLeftView(object sender, ClientLeftView eventArgs)
		{
			clientbufferOutdated = true;
			OnClientDisconnect?.Invoke(sender, eventArgs);
		}

		private IEnumerable<ClientData> clientbuffer;
		private bool clientbufferOutdated = true;
		private IDictionary<ulong, string> clientDbNames;

		private QueryConnectionData connectionData;
		private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(60);

		internal TS3QueryClient tsClient { get; private set; }
		private ClientData me;

		public QueryConnection(QueryConnectionData qcd)
		{
			clientDbNames = new Dictionary<ulong, string>();

			connectionData = qcd;
			tsClient = new TS3QueryClient(EventDispatchType.CurrentThread);
			tsClient.OnClientLeftView += ExtendedClientLeftView;
			tsClient.OnClientEnterView += ExtendedClientEnterView;
			tsClient.OnTextMessageReceived += ExtendedTextMessage;
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

				me = GetSelf();

				tsClient.RegisterNotification(MessageTarget.Server, -1);
				tsClient.RegisterNotification(MessageTarget.Private, -1);
				tsClient.RegisterNotification(RequestTarget.Server, -1);

				TickPool.RegisterTick(() => tsClient.WhoAmI(), PingInterval, true);
			}
		}

		private void Diconnect()
		{
			if (tsClient.IsConnected)
				tsClient.Quit();
		}

		public void SendMessage(string message, ClientData client) => tsClient.SendMessage(message, client);
		public void SendGlobalMessage(string message) => tsClient.SendMessage(MessageTarget.Server, 1, message);
		public void KickClientFromServer(ushort clientId) => tsClient.KickClientFromServer(new[] { clientId });
		public void KickClientFromChannel(ushort clientId) => tsClient.KickClientFromChannel(new[] { clientId });
		public void ChangeDescription(string description) => tsClient.ChangeDescription(description, me);

		public ClientData GetClientById(ushort id)
		{
			var cd = ClientBufferRequest(client => client.ClientId == id);
			if (cd != null) return cd;
			Log.Write(Log.Level.Warning, "Slow double request, due to missing or wrong permission confinguration!");
			cd = tsClient.Send<ClientData>("clientinfo", new Parameter("clid", id)).FirstOrDefault();
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
			var data = tsClient.WhoAmI();
			cd.ChannelId = data.ChannelId;
			cd.DatabaseId = data.DatabaseId;
			cd.ClientId = data.ClientId;
			cd.NickName = data.NickName;
			cd.ClientType = ClientType.Query;
			return cd;
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
			var response = tsClient.ServerGroupsOfClientDbId(client);
			if (!response.Any())
				return new int[0];
			return response.Select(csg => csg.ServerGroupId).ToArray();
		}

		public string GetNameByDbId(ulong clientDbId)
		{
			string name;
			if (clientDbNames.TryGetValue(clientDbId, out name))
				return name;

			try
			{
				var response = tsClient.ClientDbInfo(clientDbId);
				name = response?.NickName ?? string.Empty;
				clientDbNames.Add(clientDbId, name);
				return name;
			}
			catch (QueryCommandException) { return null; }
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
		[Info("wether or not to show own received messages in the log", "true")]
		public bool suppressLoopback;
	}
}
