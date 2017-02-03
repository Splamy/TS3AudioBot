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

		// Map: Id => UserToken
		private readonly Dictionary<ushort, UserSession> openSessions;
		// Map: Uid => UserToken
		private readonly Dictionary<string, UserSession> userTokens;

		public SessionManager()
		{
			Util.Init(ref openSessions);
			Util.Init(ref userTokens);
		}

		public R<UserSession> CreateSession(MainBot bot, ushort invokerId)
		{
			if (bot == null)
				throw new ArgumentNullException(nameof(bot));

			lock (openSessions)
			{
				UserSession session;
				if (openSessions.TryGetValue(invokerId, out session))
					return session;

				ClientData client = bot.QueryConnection.GetClientById(invokerId);
				if (client == null)
					return "Could not find the requested client.";

				Log.Write(Log.Level.Debug, "SM User {0} created session with the bot", client.NickName);
				session = new UserSession(bot, client);
				openSessions.Add(invokerId, session);
				return session;
			}
		}

		public bool ExistsSession(ushort invokerId)
		{
			lock (openSessions)
				return openSessions.ContainsKey(invokerId);
		}

		public R<UserSession> GetSession(ushort invokerId)
		{
			lock (openSessions)
			{
				UserSession session;
				if (openSessions.TryGetValue(invokerId, out session))
					return session;
				else
					return "Session not found";
			}
		}

		public void RemoveSession(ushort invokerId)
		{
			lock (openSessions)
			{
				UserSession session;
				if (openSessions.TryGetValue(invokerId, out session))
				{
					if (session.Token == null || !session.Token.ApiTokenActive)
						openSessions.Remove(invokerId);
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

				UserSession getSession;
				if (userTokens.TryGetValue(clientInfo.Uid, out getSession))
					session.Token = getSession.Token;
				else
				{
					session.Token = new UserToken() { UserUid = clientInfo.Uid };
					userTokens.Add(clientInfo.Uid, session);
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
			if (userTokens.TryGetValue(uid, out session) && (session.Token?.ApiTokenActive ?? false))
				return session;
			else
				return "No session found";

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
