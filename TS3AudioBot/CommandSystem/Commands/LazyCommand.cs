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
	public class LazyCommand : ICommand
	{
		private readonly ICommand innerCommand;
		private bool executed = false;
		/// <summary>
		/// The cached result, if available.
		/// </summary>
		private object? result;

		public LazyCommand(ICommand innerCommandArg)
		{
			innerCommand = innerCommandArg;
		}

		public virtual async ValueTask<object?> Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			if (!executed)
			{
				result = await innerCommand.Execute(info, arguments);
				executed = true;
				return result;
			}
			return result;
		}

		public override string ToString() => $"L({innerCommand})";
	}
}
