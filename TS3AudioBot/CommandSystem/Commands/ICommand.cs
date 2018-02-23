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

	public interface ICommand
	{
		/// <summary>Execute this command.</summary>
		/// <param name="info">All global informations for this execution.</param>
		/// <param name="arguments">
		/// The arguments for this command.
		/// They are evaluated lazy which means they will only be evaluated if needed.
		/// </param>
		/// <param name="returnTypes">
		/// The possible return types that should be returned by this execution.
		/// They are ordered by priority so, if possible, the first return type should be picked, then the second and so on.
		/// </param>
		ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes);
	}
}
