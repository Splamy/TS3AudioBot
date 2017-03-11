// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.CommandSystem
{
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

		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (result == null)
			{
				result = innerCommand.Execute(info, arguments, returnTypes);
				return result;
			}
			// Check if we can return that type
			if (!returnTypes.Contains(result.ResultType))
				throw new CommandException("The cached result can't be returned", CommandExceptionReason.NoReturnMatch);
			return result;
		}
	}
}
