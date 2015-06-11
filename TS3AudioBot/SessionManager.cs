using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TeamSpeak3QueryApi.Net.Specialized.Notifications;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot
{
	class SessionManager
	{
		public BotSession defaultSession { get; set; }
		private List<PrivateSession> openSessions;

		public SessionManager()
		{
			openSessions = new List<PrivateSession>();
		}

		public async Task<BotSession> CreateSession(QueryConnection queryConnection, TextMessage textMessage)
		{
			if (ExistsSession(textMessage))
				return GetSession(textMessage);
			GetClientsInfo client = await queryConnection.GetClientById(textMessage.InvokerId);
			var newSession = new PrivateSession(queryConnection, client, false);
			openSessions.Add(newSession);
			return newSession;
		}

		public bool ExistsSession(TextMessage textMessage)
		{
			return openSessions.Any((bs) => bs.client.Id == textMessage.InvokerId);
		}

		public BotSession GetSession(TextMessage textMessage)
		{
			return openSessions.FirstOrDefault((bs) => bs.client.Id == textMessage.InvokerId) ?? defaultSession;
		}
	}
}
