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
	public class LazyCommand : ICommand
	{
		private readonly ICommand innerCommand;
		private bool executed = false;
		/// <summary>
		/// The cached result, if available.
		/// </summary>
		private object result;

		public LazyCommand(ICommand innerCommandArg)
		{
			innerCommand = innerCommandArg;
		}

		public virtual object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			if (!executed)
			{
				result = innerCommand.Execute(info, arguments, returnTypes);
				executed = true;
				return result;
			}
			if (!ResultHelper.IsValidResult(result, returnTypes))
				throw new CommandException("The cached result can't be returned", CommandExceptionReason.NoReturnMatch);
			return result;
		}

		public override string ToString() => $"L({innerCommand})";
	}
}
