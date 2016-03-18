namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using TS3Query;
	using TS3Query.Messages;

	public class SessionManager : MarshalByRefObject
	{
		public BotSession DefaultSession { get; internal set; }
		private readonly List<PrivateSession> openSessions;

		public SessionManager()
		{
			openSessions = new List<PrivateSession>();
		}

		public BotSession CreateSession(MainBot bot, ushort invokerId)
		{
			if (bot == null)
				throw new ArgumentNullException(nameof(bot));

			if (ExistsSession(invokerId))
				return GetSession(MessageTarget.Private, invokerId);
			ClientData client = bot.QueryConnection.GetClientById(invokerId);
			if (client == null)
				throw new SessionManagerException("Could not find the requested client.");
			var newSession = new PrivateSession(bot, client);
			openSessions.Add(newSession);
			return newSession;
		}

		public bool ExistsSession(ushort invokerId)
		{
			return openSessions.Any((ps) => ps.Client.ClientId == invokerId);
		}

		public BotSession GetSession(MessageTarget target, ushort invokerId)
		{
			if (target == MessageTarget.Server)
				return DefaultSession;
			return openSessions.FirstOrDefault((bs) => bs.Client.ClientId == invokerId) ?? DefaultSession;
		}

		public void RemoveSession(ushort invokerId)
		{
			openSessions.RemoveAll((ps) => ps.Client.ClientId == invokerId);
		}
	}


	[Serializable]
	public class SessionManagerException : Exception
	{
		public SessionManagerException() { }
		public SessionManagerException(string message) : base(message) { }
		public SessionManagerException(string message, Exception inner) : base(message, inner) { }
		protected SessionManagerException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{ }
	}
}
