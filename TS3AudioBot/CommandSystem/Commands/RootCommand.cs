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
using TS3AudioBot.Dependency;

namespace TS3AudioBot.CommandSystem.Commands
{
	/// <summary>
	/// A special group command that also accepts commands as first parameter and executes them on the left over parameters.
	///
	/// This command is needed to enable easy use of higher order functions.
	/// E.g. `!(!if 1 > 2 (!vol) (!print)) 10`
	/// </summary>
	public class RootCommand : ICommand
	{
		private readonly IReadOnlyList<ICommand> internArguments;

		public RootCommand(IReadOnlyList<ICommand> arguments)
		{
			internArguments = arguments;
		}

		public virtual object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			var merged = new ICommand[internArguments.Count + arguments.Count];
			internArguments.CopyTo(0, merged, 0);
			arguments.CopyTo(0, merged, internArguments.Count);
			if (!info.TryGet<CommandManager>(out var cmdSys))
				throw new CommandException("Could not find local commandsystem tree", CommandExceptionReason.MissingContext);
			return cmdSys.RootGroup.Execute(info, merged, returnTypes);
		}

		public override string ToString() => $"RootCmd({string.Join(", ", internArguments)})";
	}
}
