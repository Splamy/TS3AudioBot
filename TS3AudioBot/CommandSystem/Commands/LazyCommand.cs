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
	using System.Collections.Generic;
	using System.Linq;

	public class LazyCommand : ICommand
	{
		private readonly ICommand innerCommand;
		/// <summary>
		/// The cached result, if available.
		/// </summary>
		private ICommandResult result;

		public LazyCommand(ICommand innerCommandArg)
		{
			innerCommand = innerCommandArg;
		}

		public virtual ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			if (result is null)
			{
				result = innerCommand.Execute(info, arguments, returnTypes);
				return result;
			}
			// Check if we can return that type
			if (!returnTypes.Contains(result.ResultType))
				throw new CommandException("The cached result can't be returned", CommandExceptionReason.NoReturnMatch);
			return result;
		}

		public override string ToString() => $"L({innerCommand})";
	}
}
