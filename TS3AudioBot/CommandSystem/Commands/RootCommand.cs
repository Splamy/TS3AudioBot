// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.Commands
{
	using CommandResults;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A special group command that also accepts commands as first parameter and executes them on the left over parameters.
	/// </summary>
	public class RootCommand : CommandGroup
	{
		public override ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			if (arguments.Count == 0)
				return base.Execute(info, arguments, returnTypes);

			var result = arguments[0].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnCommandOrString);
			if (result.ResultType == CommandResultType.String)
			{
				// Use cached result so we don't execute the first argument twice
				var passArgs = new ICommand[arguments.Count];
				passArgs[0] = new StringCommand(((StringCommandResult)result).Content);
				arguments.CopyTo(1, passArgs, 1);
				return base.Execute(info, passArgs, returnTypes);
			}
			return ((CommandCommandResult)result).Command.Execute(info, arguments.TrySegment(1), returnTypes);
		}

		public override string ToString() => "<root>";
	}
}
