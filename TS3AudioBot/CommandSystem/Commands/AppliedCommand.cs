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

namespace TS3AudioBot.CommandSystem.Commands
{
	public class AppliedCommand : ICommand
	{
		private readonly ICommand internCommand;
		private readonly IReadOnlyList<ICommand> internArguments;

		public AppliedCommand(ICommand command, IReadOnlyList<ICommand> arguments)
		{
			internCommand = command;
			internArguments = arguments;
		}

		public virtual object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			var merged = new ICommand[internArguments.Count + arguments.Count];
			internArguments.CopyTo(0, merged, 0);
			arguments.CopyTo(0, merged, internArguments.Count);
			return internCommand.Execute(info, merged, returnTypes);
		}

		public override string ToString() => $"F\"{internCommand}\"({string.Join(", ", internArguments)})";
	}
}
