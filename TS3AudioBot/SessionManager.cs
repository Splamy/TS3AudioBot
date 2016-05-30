// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Helper;
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

		public R<BotSession> CreateSession(MainBot bot, ushort invokerId)
		{
			if (bot == null)
				throw new ArgumentNullException(nameof(bot));

			if (ExistsSession(invokerId))
				return GetSession(MessageTarget.Private, invokerId);
			ClientData client = bot.QueryConnection.GetClientById(invokerId);
			if (client == null)
				return "Could not find the requested client.";
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
}
