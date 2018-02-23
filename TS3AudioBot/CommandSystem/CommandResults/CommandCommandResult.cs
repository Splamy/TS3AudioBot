// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.CommandResults
{
	using Commands;

	public class CommandCommandResult : ICommandResult
	{
		public CommandResultType ResultType => CommandResultType.Command;

		public virtual ICommand Command { get; }

		public CommandCommandResult(ICommand commandArg)
		{
			Command = commandArg;
		}

		public override string ToString() => "CommandCommandResult can't be converted into a string";
	}
}
