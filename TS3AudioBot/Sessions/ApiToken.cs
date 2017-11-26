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
	using System;
	using System.Collections.Generic;

	internal class ApiToken
	{
		public const int TokenLen = 32;
		public const int NonceLen = 32;
		public const int MaxNonceCount = 100;
		public static readonly TimeSpan DefaultTokenTimeout = TimeSpan.MaxValue;
		public static readonly TimeSpan DefaultNonceTimeout = TimeSpan.FromHours(1);

		public string Value { get; set; }
		public DateTime Timeout { get; set; }
		public bool ApiTokenActive => Value != null && Timeout > Util.GetNow();
		private readonly Dictionary<string, ApiNonce> nonceList;

		public ApiToken()
		{
			Value = null;
			Timeout = DateTime.MinValue;
			Util.Init(out nonceList);
		}

		public ApiNonce UseNonce(string nonce)
		{
			lock (nonceList)
			{
				if (!nonceList.Remove(nonce))
					return null;

				return CreateNonceInternal();
			}
		}

		private ApiNonce CreateNonceInternal()
		{
			DateTime now = Util.GetNow();

			// Clean up old
			var oldestNonce = new ApiNonce(string.Empty, DateTime.MaxValue);

			var vals = nonceList.Values;
			foreach (var val in vals)
			{
				if (val.Timeout < now)
					nonceList.Remove(val.Value);
				else if (oldestNonce.Timeout < val.Timeout)
					oldestNonce = val;
			}
			if (nonceList.Count >= MaxNonceCount && oldestNonce.Value != string.Empty)
				nonceList.Remove(oldestNonce.Value);

			// Create new
			string nextNonce;
			do { nextNonce = TextUtil.GenToken(NonceLen); } while (nonceList.ContainsKey(nextNonce));
			var newNonce = new ApiNonce(nextNonce, now + DefaultNonceTimeout);
			nonceList.Add(newNonce.Value, newNonce);

			return newNonce;
		}

		public ApiNonce CreateNonce()
		{
			lock (nonceList)
			{
				return CreateNonceInternal();
			}
		}
	}
}
