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
	using System.Linq;
	using System.Collections.Generic;

	public class AppliedCommand : ICommand
	{
		private readonly ICommand internCommand;
		private readonly IEnumerable<ICommand> internArguments;

		public AppliedCommand(ICommand command, IEnumerable<ICommand> arguments)
		{
			internCommand = command;
			internArguments = arguments;
		}

		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return internCommand.Execute(info, internArguments.Concat(arguments), returnTypes);
		}
	}
}
