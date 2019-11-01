// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;

namespace TS3AudioBot.Rights.Matchers
{
	internal class MatchToken : Matcher
	{
		private readonly HashSet<string> tokens;

		public MatchToken(IEnumerable<string> tokens) => this.tokens = new HashSet<string>(tokens);

		public override bool Matches(ExecuteContext ctx) => ctx.ApiToken != null && tokens.Contains(ctx.ApiToken);
	}
}
