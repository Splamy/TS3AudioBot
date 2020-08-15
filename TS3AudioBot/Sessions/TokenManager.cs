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
using TSLib.Helper;

namespace TS3AudioBot.Sessions
{
	public class TokenManager
	{
		private const string TokenFormat = "{0}:{1}";

		private const string ApiTokenTable = "apiToken";
		private readonly LiteCollection<DbApiToken> dbTokenList;
		private readonly Dictionary<string, ApiToken> dbTokenCache = new Dictionary<string, ApiToken>();

		public TokenManager(DbStore database)
		{
			dbTokenList = database.GetCollection<DbApiToken>(ApiTokenTable);
			dbTokenList.EnsureIndex(x => x.UserUid, true);
			dbTokenList.EnsureIndex(x => x.Token, true);
		}

		public string GenerateToken(string authId, TimeSpan? timeout = null)
		{
			if (string.IsNullOrEmpty(authId))
				throw new ArgumentNullException(nameof(authId));

			var token = new ApiToken(
				TextUtil.GenToken(ApiToken.TokenLen),
				AddTimeSpanSafe(Tools.Now, timeout ?? ApiToken.DefaultTokenTimeout));

			dbTokenCache[authId] = token;

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
			if (dateTime == DateTime.MaxValue)
				return DateTime.MaxValue;

			try
			{
				return dateTime + addSpan;
			}
			catch (ArgumentOutOfRangeException)
			{
				return addSpan >= TimeSpan.Zero ? DateTime.MaxValue : DateTime.MinValue;
			}
		}

		internal ApiToken? GetToken(string authId)
		{
			if (dbTokenCache.TryGetValue(authId, out var token)
				&& token.ApiTokenActive)
				return token;

			var dbToken = dbTokenList.FindById(authId);
			if (dbToken is null || dbToken.Token is null)
				return null;

			if (dbToken.ValidUntil < Tools.Now)
			{
				dbTokenList.Delete(authId);
				dbTokenCache.Remove(authId);
				return null;
			}

			token = new ApiToken(dbToken.Token, dbToken.ValidUntil);
			dbTokenCache[authId] = token;
			return token;
		}

		private class DbApiToken
		{
			[BsonId]
			public string? UserUid { get; set; }
			public string? Token { get; set; }
			public DateTime ValidUntil { get; set; }
		}
	}
}
