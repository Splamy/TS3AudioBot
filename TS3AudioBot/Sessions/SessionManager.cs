// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Threading;
using TSLib;

namespace TS3AudioBot.Sessions;

/// <summary>Management for clients talking with the bot.</summary>
public class SessionManager
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

	// Map: Id => UserSession
	private readonly Lock sessionLock = new();
	private readonly Dictionary<ClientId, UserSession> openSessions = [];

	public UserSession GetOrCreateSession(ClientId clientId)
	{
		lock (sessionLock)
		{
			if (openSessions.TryGetValue(clientId, out var session))
				return session;

			Log.Debug("ClientId {0} created session with the bot", clientId);
			session = new UserSession();
			openSessions.Add(clientId, session);
			return session;
		}
	}

	public UserSession? GetSession(ClientId id)
	{
		lock (sessionLock)
		{
			return openSessions.GetValueOrDefault(id);
		}
	}

	public void RemoveSession(ClientId id)
	{
		lock (sessionLock)
		{
			openSessions.Remove(id);
		}
	}
}
