// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	public class CallerInfo
	{
		/// <summary>Whether this call was initiated from the api.</summary>
		public bool ApiCall { get; }
		/// <summary>Skips all permission checks when set to true.</summary>
		public bool SkipRightsChecks { get; set; } = false;
		/// <summary>Counts execution token for a single call to prevent endless loops.</summary>
		public int CommandComplexityCurrent { get; set; } = 0;
		/// <summary>The maximum execution token count for a single call.</summary>
		public int CommandComplexityMax { get; set; } = 0;
		/// <summary>Whether the caller wants a colored output.</summary>
		public bool IsColor { get; set; }

		public CallerInfo(bool isApi)
		{
			ApiCall = isApi;
		}
	}
}
