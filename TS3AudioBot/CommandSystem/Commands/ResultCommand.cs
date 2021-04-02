// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace TS3AudioBot.CommandSystem.Commands
{
	/// <summary>
	/// A command that stores a result and returns it.
	/// </summary>
	public class ResultCommand : ICommand
	{
		public object? Content { get; }

		public ResultCommand(object? contentArg)
		{
			Content = contentArg;
		}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		public virtual async ValueTask<object?> Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			return Content;
		}

		public override string ToString() => "<result>";
	}
}
