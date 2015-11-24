using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net;
using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;
using TeamSpeak3QueryApi.Net.Specialized.Responses;
using System.Threading;

namespace TS3AudioBot
{
	class QueryConnection : IDisposable
	{
		public delegate void MessageReceivedDelegate(object sender, TextMessage e);
		public event MessageReceivedDelegate OnMessageReceived;
		public delegate void ClientEnterDelegate(object sender, ClientEnterView e);
		public event ClientEnterDelegate OnClientConnect;
		public delegate void ClientQuitDelegate(object sender, ClientLeftView e);
		public event ClientQuitDelegate OnClientDisconnect;

		private bool connected = false;
		private IReadOnlyList<GetClientsInfo> clientbuffer;
		private bool clientbufferoutdated = true;

		private QueryConnectionData connectionData;
		private const int PingEverySeconds = 60;
		private Task keepAliveTask;
		private CancellationTokenSource keepAliveTokenSource;
		private CancellationToken keepAliveToken;

		private Task queueProcessor;
		private Queue<Func<Task>> workQueue;
		private bool queueDone = false;

		public TeamSpeakClient TSClient { get; private set; }

		public QueryConnection(QueryConnectionData qcd)
		{
			queueProcessor = null;
			workQueue = new Queue<Func<Task>>();

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
						clientbufferoutdated = true;
						if (OnClientConnect != null)
						{
							foreach (var clientdata in data)
								OnClientConnect(this, clientdata);
						}
					});
				TSClient.Subscribe<ClientLeftView>(data =>
				{
					Log.Write(Log.Level.Debug, "QC ClientQuitView event raised");
					clientbufferoutdated = true;
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

		public async Task<bool> ChangeName(string newName)
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
			QueueTask(() => TSClient.SendMessage(message, client));
		}

		public void SendGlobalMessage(string message)
		{
			QueueTask(() => TSClient.SendGlobalMessage(message));
		}

		// SMART QUEUE ////////////////////

		private async void DoQueueWork()
		{
			while (true)
			{
				Func<Task> workTask;
				lock (workQueue)
				{
					if (workQueue.Count == 0)
					{
						queueDone = true;
						return;
					}
					else
					{
						workTask = workQueue.Dequeue();
					}
				}
				await workTask.Invoke();
			}
		}

		private void QueueTask(Func<Task> work)
		{
			if (queueProcessor == null)
				EnqueInternal(work);
			else
			{
				lock (workQueue)
				{
					if (queueDone)
						EnqueInternal(work);
					else
						workQueue.Enqueue(work);
				}
			}
		}

		/// <summary>Do NOT call this method directly.
		/// Use QueueTask(Func<Task>) instead.</summary>
		private void EnqueInternal(Func<Task> work)
		{
			workQueue.Enqueue(work);
			queueDone = false;
			queueProcessor = Task.Run((Action)DoQueueWork);
		}

		///////////////////////////////////

		public async Task<GetClientsInfo> GetClientById(int id)
		{
			Log.Write(Log.Level.Debug, "QC GetClientById called");
			await RefreshClientBuffer(false);
			return clientbuffer.FirstOrDefault(client => client.Id == id);
		}

		public GetClientsInfo GetClientByIdBuffer(int id)
		{
			if (clientbufferoutdated)
				Log.Write(Log.Level.Warning, "QC clientbuffer was outdated");
			return clientbuffer.FirstOrDefault(client => client.Id == id);
		}

		public async Task<GetClientsInfo> GetClientByName(string name)
		{
			await RefreshClientBuffer(false);
			return clientbuffer.FirstOrDefault(user => user.NickName == name);
		}

		public async Task RefreshClientBuffer(bool force)
		{
			if (clientbufferoutdated || force)
			{
				clientbuffer = await TSClient.GetClients();
				clientbufferoutdated = false;
			}
		}

		public async Task<int[]> GetClientServerGroups(GetClientsInfo client)
		{
			Log.Write(Log.Level.Debug, "QC GetClientServerGroups called");
			QueryResponseDictionary[] response = await TSClient.Client.Send("servergroupsbyclientid", new Parameter("cldbid", client.DatabaseId));
			if (response.Length <= 0 || !response.First().ContainsKey("sgid"))
				return new int[0];
			return response.Select<QueryResponseDictionary, int>(dict => (int)dict["sgid"]).ToArray();
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
