// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Net;

namespace TS3AudioBot.Rights.Matchers
{
	internal class MatchApiCallerIp : Matcher
	{
		private readonly HashSet<IPAddress> requestIps;

		public MatchApiCallerIp(IEnumerable<IPAddress> requestIps) => this.requestIps = new HashSet<IPAddress>(requestIps);

		public override bool Matches(ExecuteContext ctx) => ctx.ApiCallerIp != null && requestIps.Contains(ctx.ApiCallerIp);
	}
}
