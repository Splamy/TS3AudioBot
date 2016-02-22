namespace TS3AudioBot.Algorithm
{
	using System;
	using System.Reflection;
	using System.Collections.Generic;
	using System.Linq;

	public class XCommandSystem
	{
		public static IEnumerable<CommandResultType> AllTypes
		{
			get
			{
				return new [] { CommandResultType.Command, CommandResultType.Enumerable, CommandResultType.String, CommandResultType.Empty };
			}
		}

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

			return possibilities.Where(t => t.Item2 == minIndex).Select(t => t.Item1);
		}

		ICommand rootCommand;

		public ICommand RootCommand { get { return rootCommand; } }

		public XCommandSystem(ICommand rootCommandArg)
		{
			rootCommand = rootCommandArg;
		}

		internal ICommand AstToCommandResult(ASTNode node)
		{
			switch (node.Type)
			{
			case NodeType.Error:
				throw new CommandException("Found an unconvertable ASTNode of type Error");
			case NodeType.Command:
				var cmd = (ASTCommand) node;
				var arguments = new List<ICommand>();
				arguments.Add(new StringCommand(cmd.Command));
				arguments.AddRange(cmd.Parameter.Select(n => AstToCommandResult(n)));
				return new AppliedCommand(rootCommand, new StaticEnumerableCommand(arguments));
			case NodeType.Value:
				return new StringCommand(((ASTValue) node).Value);
			}
			throw new NotSupportedException("Seems like there's a new NodeType, this code should not be reached");
		}

		public ICommandResult Execute(ExecutionInformation info, string command)
		{
			return Execute(info, command, new [] { CommandResultType.String, CommandResultType.Empty });
		}

		public ICommandResult Execute(ExecutionInformation info, string command, IEnumerable<CommandResultType> returnTypes)
		{
			var ast = CommandParser.ParseCommandRequest(command);
			var cmd = AstToCommandResult(ast);
			return cmd.Execute(info, new EmptyEnumerableCommand(), returnTypes);
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments)
		{
			return Execute(info, arguments, new [] { CommandResultType.String, CommandResultType.Empty });
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return rootCommand.Execute(info, arguments, returnTypes);
		}

		public string ExecuteCommand(ExecutionInformation info, string command)
		{
			ICommandResult result = Execute(info, command);
			if (result.ResultType == CommandResultType.String ||
				result.ResultType == CommandResultType.Empty)
				return result.ToString();
			throw new CommandException("Expected a string as result");
		}
	}

	#region Commands

	public class CommandGroup : ICommand
	{
		readonly IList<Tuple<string, ICommand>> commands = new List<Tuple<string, ICommand>>();
		// Cache all names
		readonly IList<string> commandNames = new List<string>();

		public void AddCommand(string name, ICommand command)
		{
			commands.Add(new Tuple<string, ICommand>(name, command));
			if (!commandNames.Contains(name))
				commandNames.Add(name);
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (arguments.Count < 1)
			{
				if (returnTypes.Contains(CommandResultType.Command))
					return new CommandCommandResult(this);
				throw new CommandException("Expected a string");
			}

			var result = arguments.Execute(0, info, new EmptyEnumerableCommand(), new CommandResultType[] { CommandResultType.String });

			var commandResults = XCommandSystem.FilterList(commandNames, ((StringCommandResult) result).Content).ToList();
			if (commandResults.Count != 1)
				throw new CommandException("Ambigous command, possible names: " + string.Join(", ", commandResults));

			return commands.First(c => c.Item1 == commandResults[0]).Item2.Execute(
				info, new EnumerableCommandRange(arguments, 1), returnTypes);
		}
	}

	public class FunctionCommand : ICommand
	{
		// Needed for non-static member methods
		readonly object callee;
		readonly MethodInfo internCommand;
		/// <summary>
		/// How many free arguments have to be applied to this function.
		/// This includes only user-supplied arguments, e.g. the ExecutionInformation is not included.
		/// </summary>
		public int RequiredParameters { get; set; }

		public FunctionCommand(MethodInfo command, object obj = null)
		{
			internCommand = command;
			callee = obj;
			// Require all parameters by default
			RequiredParameters = internCommand.GetParameters().Where(p => p.ParameterType != typeof(ExecutionInformation)).Count();
		}

		// Provide some constructors that take lambda expressions directly
		public FunctionCommand(Action command) : this(command.Method, command.Target) {}
		public FunctionCommand(Func<string> command) : this(command.Method, command.Target) {}
		public FunctionCommand(Action<string> command) : this(command.Method, command.Target) {}
		public FunctionCommand(Func<string, string> command) : this(command.Method, command.Target) {}
		public FunctionCommand(Action<ExecutionInformation> command) : this(command.Method, command.Target) {}
		public FunctionCommand(Func<ExecutionInformation, string> command) : this(command.Method, command.Target) {}
		public FunctionCommand(Action<ExecutionInformation, string> command) : this(command.Method, command.Target) {}
		public FunctionCommand(Func<ExecutionInformation, string, string> command) : this(command.Method, command.Target) {}

		public ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			object[] parameters = new object[internCommand.GetParameters().Length];
			// a: Iterate through arguments
			// p: Iterate through parameters
			int a = 0;
			for (int p = 0; p < parameters.Length; p++)
			{
				var arg = internCommand.GetParameters()[p].ParameterType;
				if (arg == typeof(ExecutionInformation))
					parameters[p] = info;
				// Only add arguments if we still have some
				else if (a < arguments.Count)
				{
					var argResult = ((StringCommandResult) arguments.Execute(a, info, new EmptyEnumerableCommand(), new []{ CommandResultType.String })).Content;
					if (arg == typeof(string))
						parameters[p] = argResult;
					else if (arg == typeof(int) || arg == typeof(int?))
					{
						int intArg;
						if (!int.TryParse(argResult, out intArg))
							throw new CommandException("Can't convert parameter to int");
						parameters[p] = intArg;
					}
					else if (arg == typeof(string[]))
					{
						// Use the remaining arguments for this parameter
						var args = new string[arguments.Count - a];
						for (int i = 0; i < args.Length; i++, a++)
							args[i] = ((StringCommandResult) arguments.Execute(a, info, new EmptyEnumerableCommand(), new []{ CommandResultType.String })).Content;
						parameters[p] = args;
						// Correct the argument index to the last used argument
						a--;
					}
					else
						throw new CommandException("Found inconvertable parameter type: " + arg.Name);
					a++;
				}
			}
			// Check if we were able to set enough arguments
			if (a < Math.Min(parameters.Length, RequiredParameters))
			{
				if (returnTypes.Contains(CommandResultType.Command))
				{
					if (arguments.Count == 0)
						return new CommandCommandResult(this);
					return new CommandCommandResult(new AppliedCommand(this, arguments));
				}
				throw new CommandException("Not enough arguments for function " + internCommand.Name);
			}

			object result = internCommand.Invoke(callee, parameters);

			// Return the appropriate result
			if (internCommand.ReturnType == typeof(void) || result == null || string.IsNullOrEmpty(result.ToString()))
				return new EmptyCommandResult();
			return new StringCommandResult(result.ToString());
		}

		/// <summary>
		/// A conveniance method to set the amount of required parameters and returns this object.
		/// This is useful for method chaining.
		/// </summary>
		public FunctionCommand SetRequiredParameters(int required)
		{
			RequiredParameters = required;
			return this;
		}
	}

	public interface ICommand
	{
		ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes);
	}

	public class StringCommand : ICommand
	{
		readonly string content;

		public StringCommand(string contentArg)
		{
			content = contentArg;
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return new StringCommandResult(content);
		}
	}

	public class AppliedCommand : ICommand
	{
		readonly ICommand internCommand;
		readonly IEnumerableCommand internArguments;

		public AppliedCommand(ICommand command, IEnumerableCommand arguments)
		{
			internCommand = command;
			internArguments = arguments;
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return internCommand.Execute(info, new EnumerableCommandMerge(new IEnumerableCommand[] { internArguments, arguments }), returnTypes);
		}
	}

	public struct ExecutionInformation
	{
		public BotSession session;
		public TS3Query.Messages.TextMessage textMessage;
	}

	#endregion

	#region EnumerableCommands

	public interface IEnumerableCommand
	{
		int Count { get; }

		ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes);
	}

	public class EmptyEnumerableCommand : IEnumerableCommand
	{
		public int Count { get { return 0; } }

		public ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			throw new CommandException("No arguments given");
		}
	}

	public class StaticEnumerableCommand : IEnumerableCommand
	{
		readonly IEnumerable<ICommand> internArguments;

		public int Count { get { return internArguments.Count(); } }

		public StaticEnumerableCommand(IEnumerable<ICommand> arguments)
		{
			internArguments = arguments;
		}

		public ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (index < 0 || index >= internArguments.Count())
				throw new CommandException("Not enough arguments");
			return internArguments.ElementAt(index).Execute(info, arguments, returnTypes);
		}
	}

	public class EnumerableCommandRange : IEnumerableCommand
	{
		readonly IEnumerableCommand internCommand;
		readonly int start;
		readonly int count;

		public int Count { get { return Math.Min(internCommand.Count - start, count); } }

		public EnumerableCommandRange(IEnumerableCommand command, int startArg, int countArg = int.MaxValue)
		{
			internCommand = command;
			start = startArg;
			count = countArg;
		}

		public ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (index < 0)
				throw new CommandException("Negative arguments?? (EnumerableCommandRange");
			return internCommand.Execute(index + start, info, arguments, returnTypes);
		}
	}

	public class EnumerableCommandMerge : IEnumerableCommand
	{
		readonly IEnumerable<IEnumerableCommand> internCommands;

		public int Count { get { return internCommands.Select(c => c.Count).Sum(); } }

		public EnumerableCommandMerge(IEnumerable<IEnumerableCommand> commands)
		{
			internCommands = commands;
		}

		public ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (index < 0)
				throw new CommandException("Negative arguments?? (EnumerableCommandMerge");
			foreach (var c in internCommands)
			{
				if (index < c.Count)
					return c.Execute(index, info, arguments, returnTypes);
				index -= c.Count;
			}
			throw new CommandException("Not enough arguments");
		}
	}

	#endregion

	#region CommandResults

	public enum CommandResultType
	{
		Empty,
		Command,
		Enumerable,
		String
	}

	public abstract class ICommandResult
	{
		public abstract CommandResultType ResultType { get; }

		public override string ToString()
		{
			if (ResultType == CommandResultType.String)
				return ((StringCommandResult) this).Content;
			if (ResultType == CommandResultType.Empty)
				return "";
			return "CommandResult can't be converted into a string";
		}
	}

	public class EmptyCommandResult : ICommandResult
	{
		public override CommandResultType ResultType => CommandResultType.Empty;
	}

	public class CommandCommandResult : ICommandResult
	{
		readonly ICommand command;

		public override CommandResultType ResultType => CommandResultType.Command;

		public virtual ICommand Command => command;

		public CommandCommandResult(ICommand commandArg)
		{
			command = commandArg;
		}
	}

	public abstract class EnumerableCommandResult : ICommandResult
	{
		public override CommandResultType ResultType => CommandResultType.Enumerable;

		public abstract int Count { get; }

		public abstract ICommandResult this[int index]{ get; }
	}

	public class EnumerableCommandResultRange : EnumerableCommandResult
	{
		readonly EnumerableCommandResult internResult;
		readonly int start;
		readonly int count;

		public override int Count => Math.Min(internResult.Count - start, count);

		public override ICommandResult this[int index]
		{
			get
			{
				if (index >= count)
					throw new IndexOutOfRangeException($"{index} >= {count}");
				return internResult[index + start];
			}
		}

		public EnumerableCommandResultRange(EnumerableCommandResult internResultArg, int startArg, int countArg = int.MaxValue)
		{
			internResult = internResultArg;
			start = startArg;
			count = countArg;
		}
	}

	public class EnumerableCommandResultMerge : EnumerableCommandResult
	{
		readonly IEnumerable<EnumerableCommandResult> internResult;

		public override int Count => internResult.Select(r => r.Count).Sum();

		public override ICommandResult this[int index]
		{
			get
			{
				foreach (var r in internResult)
				{
					if (r.Count < index)
						return r[index];
					index -= r.Count;
				}
				throw new IndexOutOfRangeException("Not enough content available");
			}
		}

		public EnumerableCommandResultMerge(IEnumerable<EnumerableCommandResult> internResultArg)
		{
			internResult = internResultArg;
		}
	}

	public class StaticEnumerableCommandResult: EnumerableCommandResult
	{
		readonly IEnumerable<ICommandResult> content;

		public override int Count => content.Count();

		public override ICommandResult this[int index] => content.ElementAt(index);

		public StaticEnumerableCommandResult(IEnumerable<ICommandResult> contentArg, bool flattenArg = false)
		{
			content = contentArg;
		}
	}

	public class StringCommandResult : ICommandResult
	{
		readonly string content;

		public override CommandResultType ResultType => CommandResultType.String;
		public virtual string Content => content;

		public StringCommandResult(string contentArg)
		{
			content = contentArg;
		}
	}

	#endregion

	public class CommandException : Exception
	{
		public CommandException(string message) : base(message) {}
	}
}