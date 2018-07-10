// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Sessions
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using TS3Client.Messages;

	/// <summary>Management for clients talking with the bot.</summary>
	public class SessionManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		// Map: Id => UserSession
		private readonly Dictionary<ushort, UserSession> openSessions;

		public SessionManager()
		{
			Util.Init(out openSessions);
		}

		public UserSession GetOrCreateSession(ushort clientId)
		{
			lock (openSessions)
			{
				if (openSessions.TryGetValue(clientId, out var session))
					return session;

				Log.Debug("ClientId {0} created session with the bot", clientId);
				session = new UserSession();
				openSessions.Add(clientId, session);
				return session;
			}
		}

		public R<UserSession> GetSession(ushort id)
		{
			lock (openSessions)
			{
				if (openSessions.TryGetValue(id, out var session))
					return session;
				else
					return R.Err;
			}
		}

		public void RemoveSession(ushort id)
		{
			lock (openSessions)
			{
				openSessions.Remove(id);
			}
		}
	}
}
