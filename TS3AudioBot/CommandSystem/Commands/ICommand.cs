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
	using System;
	using System.Collections.Generic;

	public abstract class ICommand : MarshalByRefObject
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
		public abstract ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes);
	}
}
