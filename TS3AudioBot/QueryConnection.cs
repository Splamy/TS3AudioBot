using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TS3Query;
using TS3Query.Messages;
using TS3AudioBot.Helper;

namespace TS3AudioBot
{
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
		private const int PingEverySeconds = 60;
		private Task keepAliveTask;
		private CancellationTokenSource keepAliveTokenSource;
		private CancellationToken keepAliveToken;

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

				TickPool.RegisterTick(() => tsClient.WhoAmI(), (int)TimeSpan.FromSeconds(PingEverySeconds).TotalMilliseconds, true);
			}
		}

		private void Diconnect()
		{
			if (tsClient.IsConnected)
				tsClient.Quit();
		}

		public void SendMessage(string message, ClientData client) => tsClient.SendMessage(message, client);
		public void SendGlobalMessage(string message) => tsClient.SendGlobalMessage(message);
		public void KickClientFromServer(int clientId) => tsClient.KickClientFromServer(new[] { clientId });
		public void KickClientFromChannel(int clientId) => tsClient.KickClientFromChannel(new[] { clientId });

		public ClientData GetClientById(int id)
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
			Log.Write(Log.Level.Debug, "QC GetClientServerGroups called");
			var response = tsClient.Send("servergroupsbyclientid", new Parameter("cldbid", client.DatabaseId));
			if (!response.Any() || !response.First().ContainsKey("sgid"))
				return new int[0];
			// TODO check/redo
			return response.Select(dict => int.Parse(dict["sgid"])).ToArray();
		}

		public string GetNameByDbId(ulong clientDbId)
		{
			string name;
			if (!clientDbNames.TryGetValue(clientDbId, out name))
			{
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
				if (tsClient.IsConnected)
				{
					Diconnect();
					Log.Write(Log.Level.Debug, "QC disconnected");
					if (keepAliveToken.CanBeCanceled)
					{
						keepAliveTokenSource.Cancel();
						Log.Write(Log.Level.Debug, "QC kAT cancel raised");
					}
					if (!keepAliveTask.IsCompleted)
						keepAliveTask.Wait(500);
					Log.Write(Log.Level.Debug, "QC kAT ended");
					if (keepAliveTokenSource != null)
					{
						keepAliveTokenSource.Dispose();
						keepAliveTokenSource = null;
					}
				}

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
