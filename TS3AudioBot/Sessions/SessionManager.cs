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
	using LiteDB;
	using System;
	using System.Collections.Generic;
	using TS3Client.Messages;

	public class SessionManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const string TokenFormat = "{0}:" + Web.WebManager.WebRealm + ":{1}";

		public DbStore Database { get; set; }

		// Map: Id => UserSession
		private readonly Dictionary<ushort, UserSession> openSessions;

		// Map: Uid => InvokerData
		private const string ApiTokenTable = "apiToken";
		private LiteCollection<DbApiToken> dbTokenList;
		private readonly Dictionary<string, ApiToken> liveTokenList;

		public SessionManager()
		{
			Util.Init(out openSessions);
			Util.Init(out liveTokenList);
		}

		public void Initialize()
		{
			dbTokenList = Database.GetCollection<DbApiToken>(ApiTokenTable);
			dbTokenList.EnsureIndex(x => x.UserUid, true);
			dbTokenList.EnsureIndex(x => x.Token, true);

			Database.GetMetaData(ApiTokenTable);
		}

		public UserSession CreateSession(Bot bot, ClientData client)
		{
			if (bot == null)
				throw new ArgumentNullException(nameof(bot));

			lock (openSessions)
			{
				if (openSessions.TryGetValue(client.ClientId, out var session))
					return session;

				Log.Debug("User {0} created session with the bot", client.NickName);
				session = new UserSession(bot, client);
				openSessions.Add(client.ClientId, session);
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

		public R<string> GenerateToken(string uid, TimeSpan? timeout = null)
		{
			if (string.IsNullOrEmpty(uid))
				throw new ArgumentNullException(nameof(uid));

			if (!liveTokenList.TryGetValue(uid, out var token))
			{
				token = new ApiToken();
				liveTokenList.Add(uid, token);
			}

			token.Value = TextUtil.GenToken(ApiToken.TokenLen);
			if (timeout.HasValue)
				token.Timeout = timeout.Value == TimeSpan.MaxValue
					? DateTime.MaxValue
					: AddTimeSpanSafe(Util.GetNow(), timeout.Value);
			else
				token.Timeout = AddTimeSpanSafe(Util.GetNow(), ApiToken.DefaultTokenTimeout);

			dbTokenList.Upsert(new DbApiToken
			{
				UserUid = uid,
				Token = token.Value,
				ValidUntil = token.Timeout
			});

			return R<string>.OkR(string.Format(TokenFormat, uid, token.Value));
		}

		private static DateTime AddTimeSpanSafe(DateTime dateTime, TimeSpan addSpan)
		{
			if (addSpan == TimeSpan.MaxValue)
				return DateTime.MaxValue;
			if (addSpan == TimeSpan.MinValue)
				return DateTime.MinValue;
			try
			{
				return dateTime + addSpan;
			}
			catch (ArgumentOutOfRangeException)
			{
				return addSpan >= TimeSpan.Zero ? DateTime.MaxValue : DateTime.MinValue;
			}
		}

		internal R<ApiToken> GetToken(string uid)
		{
			if (liveTokenList.TryGetValue(uid, out var token)
				&& token.ApiTokenActive)
				return token;

			var dbToken = dbTokenList.FindById(uid);
			if (dbToken == null)
				return "No active Token";

			if (dbToken.ValidUntil < Util.GetNow())
			{
				dbTokenList.Delete(uid);
				return "No active Token";
			}

			token = new ApiToken { Value = dbToken.Token };
			liveTokenList[uid] = token;
			return token;
		}

		private class DbApiToken
		{
			[BsonId]
			public string UserUid { get; set; }
			public string Token { get; set; }
			public DateTime ValidUntil { get; set; }
		}
	}
}
