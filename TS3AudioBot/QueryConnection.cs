using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot
{
	class QueryConnection
	{
		private QueryConnectionData connectionData;
		private int PingEverySeconds = 60;

		public TeamSpeakClient TSClient { get; protected set; }

		public Action<TextMessage> Callback { get; set; }

		private bool connected = false;
		private IReadOnlyList<GetClientsInfo> clientbuffer;
		private bool clientbufferoutdated = true;

		private Task keepAliveTask;

		public QueryConnection(QueryConnectionData qcd)
		{
			connectionData = qcd;
			TSClient = new TeamSpeakClient(connectionData.host);
		}

		public async void Connect()
		{
			if (!connected)
			{
				await TSClient.Connect();
				await TSClient.Login(connectionData.user, connectionData.passwd);
				await TSClient.UseServer(1);

				await TSClient.RegisterServerNotification();
				await TSClient.RegisterTextPrivateNotification();
				await TSClient.RegisterTextServerNotification();

				TSClient.Subscribe<TextMessage>(data => data.ForEach(ProcessMessage));
				TSClient.Subscribe<ClientEnterView>(data => clientbufferoutdated = true);
				TSClient.Subscribe<ClientLeftView>(data => clientbufferoutdated = true);

				connected = true;

				keepAliveTask = Task.Run(KeepAlivePoke);
			}
		}

		private void KeepAlivePoke()
		{
			while (connected)
			{
				Console.WriteLine("Piinnngggg.....");
				TSClient.WhoAmI();
				for (int i = 0; i < PingEverySeconds && connected; i++)
					Task.Delay(1000).Wait();
			}
		}

		private void ProcessMessage(TextMessage tm)
		{
			if (Callback != null)
				Callback(tm);
		}

		public void Close()
		{
			Console.WriteLine("Closing Queryconnection...");
			connected = false;
			keepAliveTask.Wait();
		}

		public async Task<GetClientsInfo> GetClientById(int uid)
		{
			if (clientbufferoutdated)
			{
				clientbuffer = await TSClient.GetClients();
				clientbufferoutdated = false;
			}
			return clientbuffer.FirstOrDefault(client => client.Id == uid);
		}
	}

	public struct QueryConnectionData
	{
		public string host;
		public string user;
		public string passwd;
	}

	internal static class ReadOnlyCollectionExtensions
	{
		public static void ForEach<T>(this IReadOnlyCollection<T> collection, Action<T> action)
		{
			if (action == null)
				throw new ArgumentNullException("action");
			foreach (var i in collection)
				action(i);
		}
	}
}
