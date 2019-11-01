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
using TS3AudioBot.CommandSystem.CommandResults;

namespace TS3AudioBot.CommandSystem.Commands
{
	/// <summary>
	/// A special group command that also accepts commands as first parameter and executes them on the left over parameters.
	///
	/// This command is needed to enable easy use of higher order functions.
	/// E.g. `!(!if 1 > 2 (!vol) (!print)) 10`
	/// </summary>
	public class RootCommand : CommandGroup
	{
		public override object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			if (arguments.Count == 0)
				return base.Execute(info, arguments, returnTypes);

			var result = arguments[0].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnCommandOrString);
			if (result is IPrimitiveResult<string>)
			{
				// Use cached result so we don't execute the first argument twice
				var passArgs = new ICommand[arguments.Count];
				passArgs[0] = new ResultCommand(result);
				arguments.CopyTo(1, passArgs, 1);
				return base.Execute(info, passArgs, returnTypes);
			}
			return ((ICommand)result).Execute(info, arguments.TrySegment(1), returnTypes);
		}

		public override string ToString() => "<root>";
	}
}
