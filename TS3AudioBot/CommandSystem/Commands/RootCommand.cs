// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TS3AudioBot.Dependency;

namespace TS3AudioBot.CommandSystem.Commands
{
	/// <summary>
	/// A special group founction that extracts the root group from the current execution context
	/// </summary>
	public class RootCommand : ICommand
	{
		private readonly IReadOnlyList<ICommand> internArguments;

		public RootCommand(IReadOnlyList<ICommand> arguments)
		{
			internArguments = arguments;
		}

		public virtual async ValueTask<object?> Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			if (!info.TryGet<CommandManager>(out var cmdSys))
				throw new CommandException("Could not find local commandsystem tree", CommandExceptionReason.MissingContext);

			IReadOnlyList<ICommand> merged;
			if (arguments.Count == 0)
				merged = internArguments;
			else if (internArguments.Count == 0)
				merged = arguments;
			else
				merged = internArguments.Concat(arguments).ToArray();
			return await cmdSys.RootGroup.Execute(info, merged);
		}

		public override string ToString() => $"RootCmd({string.Join(", ", internArguments)})";
	}
}
