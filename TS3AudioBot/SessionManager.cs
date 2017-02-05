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
	using System.Security.Cryptography;

	public class SessionManager
	{
		private static readonly TimeSpan DefaultApiTimeout = TimeSpan.FromDays(1);
		const string apiRealm = "TS3ABAPI";
		const string tokenFormat = "{0}:" + apiRealm + ":{1}";
		private static readonly MD5 Md5Hash = MD5.Create();

		// Map: Uid => UserToken
		private readonly Dictionary<string, UserSession> openSessions;

		public SessionManager()
		{
			Util.Init(ref openSessions);
		}

		public R<UserSession> CreateSession(MainBot bot, ClientData client)
		{
			if (bot == null)
				throw new ArgumentNullException(nameof(bot));

			lock (openSessions)
			{
				UserSession session;
				if (openSessions.TryGetValue(client.Uid, out session))
					return session;

				Log.Write(Log.Level.Debug, "SM User {0} created session with the bot", client.NickName);
				session = new UserSession(bot, client);
				openSessions.Add(client.Uid, session);
				return session;
			}
		}

		public bool ExistsSession(string uid)
		{
			lock (openSessions)
				return openSessions.ContainsKey(uid);
		}

		public R<UserSession> GetSession(string uid)
		{
			lock (openSessions)
			{
				UserSession session;
				if (openSessions.TryGetValue(uid, out session))
					return session;
				else
					return "Session not found";
			}
		}

		public void RemoveSession(string uid)
		{
			lock (openSessions)
			{
				UserSession session;
				if (openSessions.TryGetValue(uid, out session))
				{
					if (session.Token == null || !session.Token.ApiTokenActive)
						openSessions.Remove(uid);
				}
			}
		}

		public R<string> GetToken(UserSession session) => GetToken(session, DefaultApiTimeout);
		public R<string> GetToken(UserSession session, TimeSpan timeout)
		{
			// Check if this is another user with the same unique Id
			if (session.Token == null)
			{
				var clientInfo = session.Bot.QueryConnection.GetClientInfoById(session.Client.ClientId);

				lock (openSessions)
				{
					UserSession getSession;
					if (openSessions.TryGetValue(clientInfo.Uid, out getSession))
						session.Token = getSession.Token;
					else
					{
						session.Token = new UserToken() { UserUid = clientInfo.Uid };
						openSessions.Add(clientInfo.Uid, session);
					}
				}
			}

			session.Token.ApiToken = GenToken();
			var newTimeout = Util.GetNow();
			if (newTimeout > session.Token.ApiTokenTimeout)
				session.Token.ApiTokenTimeout = newTimeout;

			return R<string>.OkR(string.Format(tokenFormat, session.Token.ApiTokenId, session.Token.ApiToken));
		}

		public R<UserSession> GetSessionByUid(string uid)
		{
			UserSession session;
			lock (openSessions)
			{
				if (openSessions.TryGetValue(uid, out session) && (session.Token?.ApiTokenActive ?? false))
					return session;
				else
					return "No session found";
			}
		}

		private static string GenToken()
		{
			const int TokenLen = 32;
			const string alph = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

			var arr = new char[TokenLen];
			for (int i = 0; i < arr.Length; i++)
				arr[i] = alph[Util.RngInstance.Next(0, alph.Length)];
			return new string(arr);
		}
	}

	class UserToken
	{
		public string ApiToken { get; set; }
		public uint ApiTokenId { get; set; }
		public DateTime ApiTokenTimeout { get; set; }
		public bool ApiTokenActive => ApiToken != null && ApiTokenTimeout > Util.GetNow();
		public readonly Dictionary<string, TokenNonce> NonceList;
		public string UserUid { get; set; }

		public UserToken()
		{
			ApiToken = null;
			ApiTokenTimeout = DateTime.MinValue;
			Util.Init(ref NonceList);
		}
	}

	class TokenNonce
	{
		public string Nonce { get; }
		public DateTime UseTime { get; }

		public TokenNonce(string nonce, DateTime useTime)
		{
			Nonce = nonce;
			UseTime = useTime;
		}

		public override bool Equals(object obj) => Nonce.Equals((obj as TokenNonce)?.Nonce);
		public override int GetHashCode() => Nonce.GetHashCode();
	}
}
