// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using LiteDB;
using System;
using System.Collections.Generic;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TSLib.Helper;

namespace TS3AudioBot.Sessions
{
	public class TokenManager
	{
		private const string TokenFormat = "{0}:{1}";

		private const string ApiTokenTable = "apiToken";
		private readonly LiteCollection<DbApiToken> dbTokenList;
		private readonly Dictionary<string, ApiToken> liveTokenList = new Dictionary<string, ApiToken>();

		public TokenManager(DbStore database)
		{
			dbTokenList = database.GetCollection<DbApiToken>(ApiTokenTable);
			dbTokenList.EnsureIndex(x => x.UserUid, true);
			dbTokenList.EnsureIndex(x => x.Token, true);

			database.GetMetaData(ApiTokenTable);
		}

		public string GenerateToken(string authId, TimeSpan? timeout = null)
		{
			if (string.IsNullOrEmpty(authId))
				throw new ArgumentNullException(nameof(authId));

			if (!liveTokenList.TryGetValue(authId, out var token))
			{
				token = new ApiToken();
				liveTokenList.Add(authId, token);
			}

			token.Value = TextUtil.GenToken(ApiToken.TokenLen);
			if (timeout.HasValue)
				token.Timeout = timeout.Value == TimeSpan.MaxValue
					? DateTime.MaxValue
					: AddTimeSpanSafe(Tools.Now, timeout.Value);
			else
				token.Timeout = AddTimeSpanSafe(Tools.Now, ApiToken.DefaultTokenTimeout);

			dbTokenList.Upsert(new DbApiToken
			{
				UserUid = authId,
				Token = token.Value,
				ValidUntil = token.Timeout
			});

			return string.Format(TokenFormat, authId, token.Value);
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

		internal R<ApiToken, LocalStr> GetToken(string authId)
		{
			if (liveTokenList.TryGetValue(authId, out var token)
				&& token.ApiTokenActive)
				return token;

			var dbToken = dbTokenList.FindById(authId);
			if (dbToken is null)
				return new LocalStr(strings.error_no_active_token);

			if (dbToken.ValidUntil < Tools.Now)
			{
				dbTokenList.Delete(authId);
				return new LocalStr(strings.error_no_active_token);
			}

			token = new ApiToken { Value = dbToken.Token };
			liveTokenList[authId] = token;
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
