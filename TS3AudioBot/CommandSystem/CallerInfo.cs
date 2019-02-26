// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem
{
	public class CallerInfo
	{
		/// <summary>The original unmodified string which was received by the client.</summary>
		public string TextMessage { get; }
		/// <summary>Whether this call was initiated from the api.</summary>
		public bool ApiCall { get; }
		/// <summary>Skips all permission checks when set to true.</summary>
		public bool SkipRightsChecks { get; set; } = false;
		/// <summary>Counts execution token for a single call to prevent endless loops.</summary>
		public int CommandComplexityCurrent { get; set; } = 0;
		/// <summary>The maximum execution token count for a single call.</summary>
		public int CommandComplexityMax { get; set; } = 0;

		public CallerInfo(string textMessage, bool isApi)
		{
			TextMessage = textMessage;
			ApiCall = isApi;
		}
	}
}
