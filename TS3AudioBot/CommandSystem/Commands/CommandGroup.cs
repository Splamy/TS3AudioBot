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
	using System;
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
		public ICommand GetCommand(string name) => commands.TryGetValue(name, out var com) ? com : null;
		public bool IsEmpty => commands.Count == 0;
		public IEnumerable<KeyValuePair<string, ICommand>> Commands => commands;

		public virtual ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			string result;
			if (arguments.Count == 0)
			{
				if (returnTypes.Contains(CommandResultType.Command))
					return new CommandCommandResult(this);
				result = string.Empty;
			}
			else
			{
				var comResult = arguments[0].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString);
				result = ((StringCommandResult)comResult).Content;
			}

			var commandResults = XCommandSystem.FilterList(commands, result).ToArray();
			if (commandResults.Length > 1)
				throw new CommandException("Ambiguous call, possible subcommands: " + string.Join(", ", commandResults.Select(g => g.Key)), CommandExceptionReason.AmbiguousCall);
			if (commandResults.Length == 0)
				throw new CommandException("No matching command", CommandExceptionReason.AmbiguousCall);
			if (commandResults.Length == 1 && result == string.Empty && commandResults[0].Key != string.Empty)
				throw new CommandException("Ambiguous call, possible subcommands: " + string.Join(", ", commands.Keys.Take(4)) + ", ...", CommandExceptionReason.AmbiguousCall);


			var argSubList = arguments.TrySegment(1);
			return commandResults[0].Value.Execute(info, argSubList, returnTypes);
		}

		public override string ToString() => "<group>";
	}
}
