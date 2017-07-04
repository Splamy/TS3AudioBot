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

namespace TS3AudioBot.Sessions
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using TS3Client.Messages;

	public class SessionManager
	{
		private const string TokenFormat = "{0}:" + Web.WebManager.WebRealm + ":{1}";

		// Map: Id => UserSession
		private readonly Dictionary<ushort, UserSession> openSessions;

		// Map: Uid => InvokerData
		// TODO move to database
		private readonly Dictionary<string, ApiToken> tokenList;

		public SessionManager()
		{
			Util.Init(ref openSessions);
			Util.Init(ref tokenList);
		}

		public UserSession CreateSession(MainBot bot, ClientData client)
		{
			if (bot == null)
				throw new ArgumentNullException(nameof(bot));

			lock (openSessions)
			{
				UserSession session;
				if (openSessions.TryGetValue(client.ClientId, out session))
					return session;

				Log.Write(Log.Level.Debug, "SM User {0} created session with the bot", client.NickName);
				session = new UserSession(bot, client);
				openSessions.Add(client.ClientId, session);
				return session;
			}
		}

		public R<UserSession> GetSession(ushort id)
		{
			lock (openSessions)
			{
				UserSession session;
				if (openSessions.TryGetValue(id, out session))
					return session;
				else
					return "Session not found";
			}
		}

		public void RemoveSession(ushort id)
		{
			lock (openSessions)
			{
				openSessions.Remove(id);
			}
		}

		public R<string> GenerateToken(string uid) => GenerateToken(uid, ApiToken.DefaultTokenTimeout);
		public R<string> GenerateToken(string uid, TimeSpan timeout)
		{
			if (string.IsNullOrEmpty(uid))
				throw new ArgumentNullException(nameof(uid));

			ApiToken token;
			if (!tokenList.TryGetValue(uid, out token))
				token = new ApiToken();

			token.Value = TextUtil.GenToken(ApiToken.TokenLen);
			var newTimeout = Util.GetNow() + timeout;
			if (newTimeout > token.Timeout)
				token.Timeout = newTimeout;

			return R<string>.OkR(string.Format(TokenFormat, uid, token.Value));
		}

		internal R<ApiToken> GetToken(string uid)
		{
			ApiToken token;
			if (tokenList.TryGetValue(uid, out token))
				return token;
			return "No active Token";
		}
	}
}
