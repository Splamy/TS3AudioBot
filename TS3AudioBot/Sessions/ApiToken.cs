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
		public static readonly TimeSpan DefaultTokenTimeout = TimeSpan.MaxValue;

		public string Value { get; set; }
		public DateTime Timeout { get; set; }
		public bool ApiTokenActive => Value != null && Timeout > Util.GetNow();

		public ApiToken()
		{
			Value = null;
			Timeout = DateTime.MinValue;
		}
	}
}
