using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net;
using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;
using TeamSpeak3QueryApi.Net.Specialized.Responses;
using TS3AudioBot.Helper;

namespace TS3AudioBot
{
	class QueryConnection : IQueryConnection
	{
		public event MessageReceivedDelegate OnMessageReceived;
		public event ClientEnterDelegate OnClientConnect;
		public event ClientQuitDelegate OnClientDisconnect;

		private bool connected = false;
		private IReadOnlyList<GetClientsInfo> clientbuffer;
		private bool clientbufferOutdated = true;
		private IDictionary<ulong, string> clientDbNames;

		private QueryConnectionData connectionData;
		private const int PingEverySeconds = 60;
		private Task keepAliveTask;
		private CancellationTokenSource keepAliveTokenSource;
		private CancellationToken keepAliveToken;

		public TeamSpeakClient TSClient { get; private set; }

		public QueryConnection(QueryConnectionData qcd)
		{
			clientDbNames = new Dictionary<ulong, string>();

			connectionData = qcd;
			TSClient = new TeamSpeakClient(connectionData.host);
		}

		public async Task Connect()
		{
			if (!connected)
			{
				await TSClient.Connect();
				await TSClient.Login(connectionData.user, connectionData.passwd);
				await TSClient.UseServer(1);
				if (!(await ChangeName("TS3AudioBot")))
					Log.Write(Log.Level.Warning, "TS3AudioBot name already in use!");

				await TSClient.RegisterServerNotification();
				await TSClient.RegisterTextPrivateNotification();
				await TSClient.RegisterTextServerNotification();

				TSClient.Subscribe<TextMessage>(data =>
					{
						Log.Write(Log.Level.Debug, "QC TextMessage event raised");
						if (OnMessageReceived != null)
						{
							foreach (var textMessage in data)
								OnMessageReceived(this, textMessage);
						}
					});
				TSClient.Subscribe<ClientEnterView>(data =>
					{
						Log.Write(Log.Level.Debug, "QC ClientEnterView event raised");
						clientbufferOutdated = true;
						if (OnClientConnect != null)
						{
							foreach (var clientdata in data)
								OnClientConnect(this, clientdata);
						}
					});
				TSClient.Subscribe<ClientLeftView>(data =>
				{
					Log.Write(Log.Level.Debug, "QC ClientQuitView event raised");
					clientbufferOutdated = true;
					if (OnClientDisconnect != null)
					{
						foreach (var clientdata in data)
							OnClientDisconnect(this, clientdata);
					}
				});

				connected = true;

				keepAliveTokenSource = new CancellationTokenSource();
				keepAliveToken = keepAliveTokenSource.Token;
				keepAliveTask = Task.Run((Action)KeepAlivePoke);
			}
		}

		private async void KeepAlivePoke()
		{
			try
			{
				while (!keepAliveToken.IsCancellationRequested)
				{
					await TSClient.WhoAmI();
					await Task.Delay(TimeSpan.FromSeconds(PingEverySeconds), keepAliveToken);
				}
			}
			catch (TaskCanceledException) { }
			catch (AggregateException) { }
		}

		private async Task<bool> ChangeName(string newName)
		{
			try
			{
				await TSClient.Client.Send("clientupdate", new Parameter("client_nickname", newName));
				return true;
			}
			catch (QueryException) { return false; }
		}

		private void Diconnect()
		{
			if (TSClient.Client.IsConnected)
				TSClient.Client.Send("quit");
		}

		public void SendMessage(string message, GetClientsInfo client)
		{
			Sync.Run(TSClient.SendMessage(message, client));
		}

		public void SendGlobalMessage(string message)
		{
			Sync.Run(TSClient.SendGlobalMessage(message));
		}

		public GetClientsInfo GetClientById(int id)
		{
			RefreshClientBuffer(false);
			return clientbuffer.FirstOrDefault(client => client.Id == id);
		}

		public GetClientsInfo GetClientByName(string name)
		{
			RefreshClientBuffer(false);
			return clientbuffer.FirstOrDefault(user => user.NickName == name);
		}

		public void RefreshClientBuffer(bool force)
		{
			if (clientbufferOutdated || force)
			{
				clientbuffer = Sync.Run<IReadOnlyList<GetClientsInfo>>(TSClient.GetClients());
				clientbufferOutdated = false;
			}
		}

		public int[] GetClientServerGroups(GetClientsInfo client)
		{
			Log.Write(Log.Level.Debug, "QC GetClientServerGroups called");
			QueryResponseDictionary[] response = Sync.Run(TSClient.Client.Send("servergroupsbyclientid", new Parameter("cldbid", client.DatabaseId)));
			if (!response.Any() || !response.First().ContainsKey("sgid"))
				return new int[0];
			return response.Select<QueryResponseDictionary, int>(dict => (int)dict["sgid"]).ToArray();
		}

		public async Task<string> GetNameByDbId(ulong clientDbId)
		{
			string name;
			if (!clientDbNames.TryGetValue(clientDbId, out name))
			{
				QueryResponseDictionary[] response = await TSClient.Client.Send("clientdbinfo", new Parameter("cldbid", (ParameterValueEx)clientDbId));
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
			if (connected)
			{
				connected = false;
				Log.Write(Log.Level.Debug, "QC disconnecting...");
				Diconnect();
				Log.Write(Log.Level.Debug, "QC disconnected");
				if (keepAliveToken.CanBeCanceled)
				{
					keepAliveTokenSource.Cancel();
					Log.Write(Log.Level.Debug, "QC kAT cancel raised");
				}
				if (!keepAliveTask.IsCompleted)
					keepAliveTask.Wait();
				Log.Write(Log.Level.Debug, "QC kAT ended");
				if (keepAliveTokenSource != null)
				{
					keepAliveTokenSource.Dispose();
					keepAliveTokenSource = null;
				}
			}
		}

		class ParameterValueEx : ParameterValue
		{
			public ParameterValueEx(ulong value)
			{
				Value = value.ToString();
			}

			public static implicit operator ParameterValueEx(ulong fromParameter)
			{
				return new ParameterValueEx(fromParameter);
			}
		}
	}

	public struct QueryConnectionData
	{
		[InfoAttribute("the address of the TeamSpeak3 Query")]
		public string host;
		[InfoAttribute("the user for the TeamSpeak3 Query")]
		public string user;
		[InfoAttribute("the password for the TeamSpeak3 Query")]
		public string passwd;
	}
}
