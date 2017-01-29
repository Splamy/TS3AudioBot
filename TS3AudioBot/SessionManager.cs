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
	using TS3Client.Messages;

	public class SessionManager
	{
		private static readonly TimeSpan DefaultApiTimeout = TimeSpan.FromDays(1);
		const string tokenFormat = "{0}${1}";

		private readonly List<UserSession> openSessions = new List<UserSession>();

		public SessionManager() { }

		public R<UserSession> CreateSession(MainBot bot, ushort invokerId)
		{
			if (bot == null)
				throw new ArgumentNullException(nameof(bot));

			var result = GetSession(invokerId);
			if (result) return result.Value;

			ClientData client = bot.QueryConnection.GetClientById(invokerId);
			if (client == null)
				return "Could not find the requested client.";

			Log.Write(Log.Level.Debug, "SM User {0} created session with the bot", client.NickName);
			var newSession = new UserSession(bot, client);
			openSessions.Add(newSession);
			return newSession;
		}

		public bool ExistsSession(ushort invokerId)
		{
			return openSessions.Any((ps) => ps.Client.ClientId == invokerId);
		}

		public R<UserSession> GetSession(ushort invokerId)
		{
			var session = openSessions.FirstOrDefault((bs) => bs.Client.ClientId == invokerId);
			if (session == null) return "Session not found";
			else return session;
		}

		public void RemoveSession(ushort invokerId)
		{
			openSessions.RemoveAll((ps) => ps.Client.ClientId == invokerId && !ps.ApiTokenActive);
		}

		public R<string> GetToken(UserSession session) => GetToken(session, DefaultApiTimeout);
		public R<string> GetToken(UserSession session, TimeSpan timeout)
		{
			if (!openSessions.Contains(session))
				return R<string>.Err("This session is not associated to the current manager.");

			session.ApiToken = GenToken();
			var newTimeout = Util.GetNow();
			if (newTimeout > session.ApiTokenTimeout)
				session.ApiTokenTimeout = newTimeout;

			return R<string>.OkR(string.Format(tokenFormat, session.ApiTokenId, session.ApiToken));
		}

		public R<UserSession> GetSessionByTokenId(uint tokenId)
		{
			var session = openSessions.Where(s => s.ApiTokenId == tokenId).FirstOrDefault();
			if (session == null || session.ApiTokenTimeout < Util.GetNow())
				return "No session found";
			else
				return session;
		}

		private static string GenToken()
		{
			const int TokenLen = 64;
			const string alph = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

			var arr = new char[TokenLen];
			for (int i = 0; i < arr.Length; i++)
				arr[i] = alph[Util.RngInstance.Next(0, alph.Length)];
			return new string(arr);
		}
	}
}
