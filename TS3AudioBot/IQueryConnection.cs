using System;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot
{
	public interface IQueryConnection : IDisposable
	{
		event MessageReceivedDelegate OnMessageReceived;
		event ClientEnterDelegate OnClientConnect;
		event ClientQuitDelegate OnClientDisconnect;

		TeamSpeakClient TSClient { get; }

		Task Connect();
		void SendMessage(string message, GetClientsInfo client);
		void SendGlobalMessage(string message);

		GetClientsInfo GetClientById(int id);
		GetClientsInfo GetClientByName(string name);
		void RefreshClientBuffer(bool force);
		int[] GetClientServerGroups(GetClientsInfo client);
	}

	public delegate void MessageReceivedDelegate(object sender, TextMessage e);
	public delegate void ClientEnterDelegate(object sender, ClientEnterView e);
	public delegate void ClientQuitDelegate(object sender, ClientLeftView e);
}
