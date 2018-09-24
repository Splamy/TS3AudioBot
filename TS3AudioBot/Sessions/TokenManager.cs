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
	using Localization;
	using System;
	using System.Collections.Generic;

	public class TokenManager
	{
		private const string TokenFormat = "{0}:" + Web.WebServer.WebRealm + ":{1}";

		private const string ApiTokenTable = "apiToken";
		private LiteCollection<DbApiToken> dbTokenList;
		// Map: Uid => ApiToken
		private readonly Dictionary<string, ApiToken> liveTokenList;

		public DbStore Database { get; set; }

		public TokenManager()
		{
			Util.Init(out liveTokenList);
		}

		public void Initialize()
		{
			dbTokenList = Database.GetCollection<DbApiToken>(ApiTokenTable);
			dbTokenList.EnsureIndex(x => x.UserUid, true);
			dbTokenList.EnsureIndex(x => x.Token, true);

			Database.GetMetaData(ApiTokenTable);
		}

		public string GenerateToken(string uid, TimeSpan? timeout = null)
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

			return string.Format(TokenFormat, uid, token.Value);
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

		internal R<ApiToken, LocalStr> GetToken(string uid)
		{
			if (liveTokenList.TryGetValue(uid, out var token)
				&& token.ApiTokenActive)
				return token;

			var dbToken = dbTokenList.FindById(uid);
			if (dbToken is null)
				return new LocalStr(strings.error_no_active_token);

			if (dbToken.ValidUntil < Util.GetNow())
			{
				dbTokenList.Delete(uid);
				return new LocalStr(strings.error_no_active_token);
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
