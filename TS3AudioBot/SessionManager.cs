using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot
{
	class SessionManager
	{
		public BotSession DefaultSession { get; set; }
		private readonly List<PrivateSession> openSessions;

		public SessionManager()
		{
			openSessions = new List<PrivateSession>();
		}

		public async Task<BotSession> CreateSession(MainBot bot, int invokerId)
		{
			if (ExistsSession(invokerId))
				return GetSession(MessageTarget.Private, invokerId);
			GetClientsInfo client = await bot.QueryConnection.GetClientById(invokerId);
			var newSession = new PrivateSession(bot, client);
			openSessions.Add(newSession);
			return newSession;
		}

		public bool ExistsSession(int invokerId)
		{
			return openSessions.Any((ps) => ps.Client.Id == invokerId);
		}

		public BotSession GetSession(MessageTarget target, int invokerId)
		{
			if (target == MessageTarget.Server)
				return DefaultSession;
			return openSessions.FirstOrDefault((bs) => bs.Client.Id == invokerId) ?? DefaultSession;
		}

		public void RemoveSession(int invokerId)
		{
			openSessions.RemoveAll((ps) => ps.Client.Id == invokerId);
		}
	}
}
