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

	internal class UserToken
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
}
