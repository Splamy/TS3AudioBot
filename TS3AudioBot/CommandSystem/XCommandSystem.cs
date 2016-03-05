namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public class XCommandSystem
	{
		public static readonly CommandResultType[] AllTypes = Enum.GetValues(typeof(CommandResultType)).OfType<CommandResultType>().ToArray();

		ICommand rootCommand;

		public XCommandSystem(ICommand rootCommandArg)
		{
			rootCommand = rootCommandArg;
		}

		public ICommand RootCommand => rootCommand;

		public static IEnumerable<string> FilterList(IEnumerable<string> list, string filter)
		{
			// Convert result to list because it can be enumerated multiple times
			var possibilities = list.Select(t => new Tuple<string, int>(t, 0)).ToList();
			// Filter matching commands
			foreach (var c in filter)
			{
				var newPossibilities = (from p in possibilities
										let pos = p.Item1.IndexOf(c, p.Item2)
										where pos != -1
										select new Tuple<string, int>(p.Item1, pos + 1)).ToList();
				if (newPossibilities.Any())
					possibilities = newPossibilities;
			}
			// Take command with lowest index
			int minIndex = possibilities.Min(t => t.Item2);
			var cmds = possibilities.Where(t => t.Item2 == minIndex).Select(t => t.Item1).ToList();
			// Take the smallest command
			int minLength = cmds.Min(c => c.Length);

			return cmds.Where(c => c.Length == minLength);
		}

		internal ICommand AstToCommandResult(ASTNode node)
		{
			switch (node.Type)
			{
			case ASTType.Error:
				throw new CommandException("Found an unconvertable ASTNode of type Error");
			case ASTType.Command:
				var cmd = (ASTCommand)node;
				var arguments = new List<ICommand>();
				arguments.AddRange(cmd.Parameter.Select(n => AstToCommandResult(n)));
				return new AppliedCommand(rootCommand, arguments);
			case ASTType.Value:
				return new StringCommand(((ASTValue)node).Value);
			}
			throw new NotSupportedException("Seems like there's a new NodeType, this code should not be reached");
		}

		public ICommandResult Execute(ExecutionInformation info, string command)
		{
			return Execute(info, command, new[] { CommandResultType.String, CommandResultType.Empty });
		}

		public ICommandResult Execute(ExecutionInformation info, string command, IEnumerable<CommandResultType> returnTypes)
		{
			var ast = CommandParser.ParseCommandRequest(command);
			var cmd = AstToCommandResult(ast);
			return cmd.Execute(info, new ICommand[] { }, returnTypes);
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments)
		{
			return Execute(info, arguments, new[] { CommandResultType.String, CommandResultType.Empty });
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return rootCommand.Execute(info, arguments, returnTypes);
		}

		public string ExecuteCommand(ExecutionInformation info, string command)
		{
			ICommandResult result = Execute(info, command);
			if (result.ResultType == CommandResultType.String)
				return result.ToString();
			if (result.ResultType == CommandResultType.Empty)
				return null;
			throw new CommandException("Expected a string or nothing as result");
		}
	}
}
