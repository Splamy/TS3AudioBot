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
using System.Linq;
using TS3AudioBot.CommandSystem;

namespace TS3AudioBot.Plugins
{
	internal class PluginCommandBag : ICommandBag
	{
		public IReadOnlyCollection<BotCommand> BagCommands { get; }
		public IReadOnlyCollection<string> AdditionalRights => Array.Empty<string>();

		public PluginCommandBag(object obj, Type t)
		{
			BagCommands = CommandManager.GetBotCommands(obj, t).ToArray();
		}
	}
}
