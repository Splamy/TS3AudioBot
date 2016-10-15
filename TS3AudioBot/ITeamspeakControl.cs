namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Helper;
	using TS3Client;
	using TS3Client.Messages;

	public interface ITeamspeakControl : IDisposable
	{
		event EventHandler<TextMessage> OnMessageReceived;
		event EventHandler<ClientEnterView> OnClientConnect;
		event EventHandler<ClientLeftView> OnClientDisconnect;

		void Connect();
		void EnterEventLoop();

		void SendMessage(string message, ushort clientId);
		void SendGlobalMessage(string message);
		void KickClientFromServer(ushort clientId);
		void KickClientFromChannel(ushort clientId);
		void ChangeDescription(string description);

		ClientData GetClientById(ushort id);
		ClientData GetClientByName(string name);
		ClientData GetSelf();

		void RefreshClientBuffer(bool force);
		ulong[] GetClientServerGroups(ClientData client);
		string GetNameByDbId(ulong clientDbId);
	}
}
