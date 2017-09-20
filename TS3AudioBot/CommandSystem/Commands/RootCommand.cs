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
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// A special group command that also accepts commands as first parameter and executes them on the left over parameters.
	/// </summary>
	public class RootCommand : CommandGroup
	{
		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (!arguments.Any())
				return base.Execute(info, arguments, returnTypes);

			var result = arguments.First().Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.Command, CommandResultType.String });
			if (result.ResultType == CommandResultType.String)
				// Use cached result so we don't execute the first argument twice
				return base.Execute(info, new ICommand[] { new StringCommand(((StringCommandResult)result).Content) }
				                    .Concat(arguments.Skip(1)), returnTypes);

			return ((CommandCommandResult)result).Command.Execute(info, arguments.Skip(1), returnTypes);
		}
	}
}
