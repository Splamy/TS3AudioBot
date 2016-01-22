namespace TS3AudioBot
{
	using System;
	using TS3Query.Messages;

	public interface IQueryConnection : IDisposable
	{
		event EventHandler<TextMessage> OnMessageReceived;
		event EventHandler<ClientEnterView> OnClientConnect;
		event EventHandler<ClientLeftView> OnClientDisconnect;

		void Connect();

		void SendMessage(string message, ClientData client);
		void SendGlobalMessage(string message);
		void KickClientFromServer(ushort clientId);
		void KickClientFromChannel(ushort clientId);

		ClientData GetClientById(ushort id);
		ClientData GetClientByName(string name);
		void RefreshClientBuffer(bool force);
		int[] GetClientServerGroups(ClientData client);
	}
}
