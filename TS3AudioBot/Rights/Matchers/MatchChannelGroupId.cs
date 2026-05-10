// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Frozen;
using System.Collections.Generic;
using TSLib;

namespace TS3AudioBot.Rights.Matchers;

internal class MatchChannelGroupId(IEnumerable<ChannelGroupId> channelGroupIds) : Matcher
{
	private readonly FrozenSet<ChannelGroupId> _channelGroupIds = [..channelGroupIds];

	public override bool Matches(ExecuteContext ctx) => ctx.ChannelGroupId is { } id && _channelGroupIds.Contains(id);

	public override void SetRequiredFeatures(ParseContext ctx) => ctx.NeedsAvailableChanGroups = true;
}
