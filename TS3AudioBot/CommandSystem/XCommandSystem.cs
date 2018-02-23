// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem
{
	using Ast;
	using CommandResults;
	using Commands;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public class XCommandSystem
	{
		public static readonly CommandResultType[] ReturnJson = { CommandResultType.Json };
		public static readonly CommandResultType[] ReturnJsonOrNothing = { CommandResultType.Json, CommandResultType.Empty };
		public static readonly CommandResultType[] ReturnString = { CommandResultType.String };
		public static readonly CommandResultType[] ReturnStringOrNothing = { CommandResultType.String, CommandResultType.Empty };
		public static readonly CommandResultType[] ReturnCommandOrString = { CommandResultType.Command, CommandResultType.String };

		/// <summary>
		/// The order of types, the first item has the highest priority, items not in the list have lower priority.
		/// </summary>
		public static readonly Type[] TypeOrder = {
			typeof(bool),
			typeof(sbyte), typeof(byte),
			typeof(short), typeof(ushort),
			typeof(int), typeof(uint),
			typeof(long), typeof(ulong),
			typeof(float), typeof(double),
			typeof(TimeSpan), typeof(DateTime),
			typeof(string) };
		public static readonly HashSet<Type> BasicTypes = new HashSet<Type>(TypeOrder);

		public RootCommand RootCommand { get; }

		public XCommandSystem()
		{
			RootCommand = new RootCommand();
		}

		public static IEnumerable<KeyValuePair<string, T>> FilterList<T>(IEnumerable<KeyValuePair<string, T>> list, string filter)
		{
			// Convert result to list because it can be enumerated multiple times
			var possibilities = list.Select(t => new FilterItem<T>(t.Key, t.Value, 0)).ToList();
			// Filter matching commands
			foreach (var c in filter.ToLowerInvariant())
			{
				var newPossibilities = (from p in possibilities
										let pos = p.Name.ToLowerInvariant().IndexOf(c, p.Index)
										where pos != -1
										select new FilterItem<T>(p.Name, p.Value, pos + 1)).ToList();
				if (newPossibilities.Count > 0)
					possibilities = newPossibilities;
			}
			// Take command with lowest index
			int minIndex = possibilities.Min(t => t.Index);
			var cmds = possibilities.Where(t => t.Index == minIndex).ToArray();
			// Take the smallest command
			int minLength = cmds.Min(c => c.Name.Length);

			return cmds.Where(c => c.Name.Length == minLength).Select(fi => new KeyValuePair<string, T>(fi.Name, fi.Value));
		}

		private sealed class FilterItem<T>
		{
			public readonly string Name;
			public readonly T Value;
			public readonly int Index;

			public FilterItem(string n, T v, int i)
			{
				Name = n;
				Value = v;
				Index = i;
			}
		}

		internal ICommand AstToCommandResult(AstNode node)
		{
			switch (node.Type)
			{
			case AstType.Error:
				throw new CommandException("Found an unconvertable ASTNode of type Error", CommandExceptionReason.InternalError);
			case AstType.Command:
				var cmd = (AstCommand)node;
				var arguments = new ICommand[cmd.Parameter.Count];
				for (int i = 0; i < cmd.Parameter.Count; i++)
					arguments[i] = AstToCommandResult(cmd.Parameter[i]);
				return new AppliedCommand(RootCommand, arguments);
			case AstType.Value:
				return new StringCommand(((AstValue)node).Value);
			default:
				throw new NotSupportedException("Seems like there's a new NodeType, this code should not be reached");
			}
		}

		public ICommandResult Execute(ExecutionInformation info, string command)
		{
			return Execute(info, command, ReturnStringOrNothing);
		}

		public ICommandResult Execute(ExecutionInformation info, string command, IReadOnlyList<CommandResultType> returnTypes)
		{
			var ast = CommandParser.ParseCommandRequest(command);
			var cmd = AstToCommandResult(ast);
			return cmd.Execute(info, Array.Empty<ICommand>(), returnTypes);
		}

		public ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			return Execute(info, arguments, ReturnStringOrNothing);
		}

		public ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			return RootCommand.Execute(info, arguments, returnTypes);
		}

		public string ExecuteCommand(ExecutionInformation info, string command)
		{
			var result = Execute(info, command);
			if (result.ResultType == CommandResultType.String)
				return result.ToString();
			if (result.ResultType == CommandResultType.Empty)
				return null;
			throw new CommandException("Expected a string or nothing as result", CommandExceptionReason.NoReturnMatch);
		}
	}
}
