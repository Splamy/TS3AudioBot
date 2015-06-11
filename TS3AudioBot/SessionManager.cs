using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TeamSpeak3QueryApi.Net.Specialized;
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

		public async Task<BotSession> CreateSession(QueryConnection queryConnection, int invokerId)
		{
			if (ExistsSession(invokerId))
				return GetSession(MessageTarget.Private, invokerId);
			GetClientsInfo client = await queryConnection.GetClientById(invokerId);
			var newSession = new PrivateSession(queryConnection, client);
			openSessions.Add(newSession);
			return newSession;
		}

		public bool ExistsSession(int invokerId)
		{
			return openSessions.Any((ps) => ps.client.Id == invokerId);
		}

		public BotSession GetSession(MessageTarget target, int invokerId)
		{
			if (target == MessageTarget.Server)
				return defaultSession;
			return openSessions.FirstOrDefault((bs) => bs.client.Id == invokerId) ?? defaultSession;
		}

		public void RemoveSession(int invokerId)
		{
			openSessions.RemoveAll((ps) => ps.client.Id == invokerId);
		}
	}
}
