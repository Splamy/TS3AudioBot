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

	internal class ApiToken
	{
		public const int TokenLen = 32;
		public const int NonceLen = 32;
		public static readonly TimeSpan DefaultTokenTimeout = TimeSpan.FromDays(1);
		public static readonly TimeSpan DefaultNonceTimeout = TimeSpan.FromHours(1);

		public string Value { get; set; }
		public DateTime Timeout { get; set; }
		public bool ApiTokenActive => Value != null && Timeout > Util.GetNow();
		private readonly Dictionary<string, ApiNonce> nonceList;

		public ApiToken()
		{
			Value = null;
			Timeout = DateTime.MinValue;
			Util.Init(ref nonceList);
		}

		public ApiNonce UseNonce(string token)
		{
			lock (nonceList)
			{
				ApiNonce nonce;
				if (!nonceList.TryGetValue(token, out nonce))
					return null;

				nonceList.Remove(token);

				return CreateNonceInternal();
			}
		}

		private ApiNonce CreateNonceInternal()
		{
			// Clean up old
			var vals = nonceList.Values;
			foreach (var val in vals)
			{
				if (val.Timeout < Util.GetNow())
				{
					nonceList.Remove(val.Value);
				}
			}

			// Create new
			string nextNonce;
			do { nextNonce = TextUtil.GenToken(NonceLen); } while (nonceList.ContainsKey(nextNonce));
			var newNonce = new ApiNonce(nextNonce, Util.GetNow() + DefaultNonceTimeout);
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
