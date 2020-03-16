// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Net;
using TSLib;
using TSLib.Messages;

namespace TS3AudioBot.Rights
{
	internal class ExecuteContext
	{
		public string Host { get; set; }
		public ServerGroupId[] ServerGroups { get; set; } = Array.Empty<ServerGroupId>();
		public ChannelGroupId? ChannelGroupId { get; set; }
		public Uid ClientUid { get; set; }
		public bool IsApi { get; set; }
		public IPAddress ApiCallerIp { get; set; }
		public string ApiToken { get; set; }
		public string Bot { get; set; }
		public TextMessageTargetMode? Visibiliy { get; set; }
		public PermOverview[] Permissions { get; set; }

		public List<RightsRule> MatchingRules { get; } = new List<RightsRule>();

		public HashSet<string> DeclAdd { get; } = new HashSet<string>();
	}
}
