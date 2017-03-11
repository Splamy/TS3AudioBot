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

	public class CommandGroup : ICommand
	{
		private readonly IDictionary<string, ICommand> commands = new Dictionary<string, ICommand>();

		public void AddCommand(string name, ICommand command) => commands.Add(name, command);
		public void RemoveCommand(string name) => commands.Remove(name);
		// TODO: test if command does not exist
		public void RemoveCommand(ICommand command) => commands.Remove(commands.FirstOrDefault(kvp => kvp.Value == command).Key);
		public bool ContainsCommand(string name) => commands.ContainsKey(name);
		public ICommand GetCommand(string name)
		{
			ICommand com;
			return commands.TryGetValue(name, out com) ? com : null;
		}
		public bool IsEmpty => !commands.Any();
		public IEnumerable<KeyValuePair<string, ICommand>> Commands => commands;

		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			string result;
			if (!arguments.Any())
			{
				if (returnTypes.Contains(CommandResultType.Command))
					return new CommandCommandResult(this);
				result = string.Empty;
			}
			else
			{
				var comResult = arguments.First().Execute(info, Enumerable.Empty<ICommand>(), new CommandResultType[] { CommandResultType.String });
				result = ((StringCommandResult)comResult).Content;
			}

			var commandResults = XCommandSystem.FilterList(commands, result);
			if (commandResults.Skip(1).Any())
				throw new CommandException("Ambiguous command, possible names: " + string.Join(", ", commandResults.Select(g => g.Key)), CommandExceptionReason.AmbiguousCall);

			var argSubList = arguments.Skip(1).ToArray();
			return commandResults.First().Value.Execute(info, argSubList, returnTypes);
		}
	}
}
