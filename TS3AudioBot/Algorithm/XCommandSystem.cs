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

		private ICommand rootCommand;

		public ICommand RootCommand { get { return rootCommand; } }

		public XCommandSystem(ICommand rootCommandArg)
		{
			rootCommand = rootCommandArg;
		}

		public ICommandResult Execute(ExecutionInformation info, string command)
		{
			return Execute(info, command, new [] { CommandResultType.String, CommandResultType.Empty });
		}

		public ICommandResult Execute(ExecutionInformation info, string command, IEnumerable<CommandResultType> returnTypes)
		{
			return new EmptyCommandResult();
		}

		public ICommandResult Execute(ExecutionInformation info, EnumerableCommandResult arguments)
		{
			return Execute(info, arguments, new [] { CommandResultType.String, CommandResultType.Empty });
		}

		public ICommandResult Execute(ExecutionInformation info, EnumerableCommandResult arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return rootCommand.Execute(info, arguments, returnTypes);
		}

		public string ExecuteCommand(ExecutionInformation info, string command)
		{
			ICommandResult result = Execute(info, command);
			if (result.ResultType == CommandResultType.String ||
				result.ResultType == CommandResultType.Empty)
				return result.ToString();
			throw new CommandException("Command result is not a string");
		}
	}

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

		public ICommandResult Execute(ExecutionInformation info, EnumerableCommandResult arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (arguments.Count < 1)
			{
				if (returnTypes.Contains(CommandResultType.Command))
					return new CommandCommandResult(this);
				throw new CommandException("CommandGroup can't be executed without arguments");
			}

			var result = arguments[0];
			if (result.ResultType != CommandResultType.String)
				throw new CommandException("CommandGroup must get a string as first argument");

			var commandResults = XCommandSystem.FilterList(commandNames, ((StringCommandResult) result).Content).ToList();
			if (commandResults.Count != 1)
				throw new CommandException("Ambigous command, possible names: " + string.Join(", ", commandResults));

			return commands.First(c => c.Item1 == commandResults[0]).Item2.Execute(
				info, new EnumerableCommandResultRange(arguments, 1), returnTypes);
		}
	}

	public class FunctionCommand : ICommand
	{
		/// <summary>
		/// A FunctionCommand that has some already applied arguments.
		/// </summary>
		protected class PartialFunctionCommand : ICommand
		{
			readonly EnumerableCommandResult savedArguments;
			readonly ICommand internCommand;

			public PartialFunctionCommand(ICommand internCommandArg, EnumerableCommandResult arguments)
			{
				internCommand = internCommandArg;
				savedArguments = arguments;
			}

			public ICommandResult Execute(ExecutionInformation info, EnumerableCommandResult arguments, IEnumerable<CommandResultType> returnTypes)
			{
				return internCommand.Execute(info, new EnumerableCommandResultMerge(new []{ savedArguments, arguments }), returnTypes);
			}
		}

		// Needed for non-static member methods
		private readonly object callee;
		private readonly MethodInfo internCommand;

		public FunctionCommand(MethodInfo command, object obj = null)
		{
			internCommand = command;
			callee = obj;
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

		public ICommandResult Execute(ExecutionInformation info, EnumerableCommandResult arguments, IEnumerable<CommandResultType> returnTypes)
		{
			object[] parameters = new object[internCommand.GetParameters().Length];
			// The first argument can be the ExecutionInformation
			bool getsInfo = parameters.Length != 0 && internCommand.GetParameters()[0].ParameterType == typeof(ExecutionInformation);
			int parameter = 0;
			if (getsInfo)
				parameters[parameter++] = info;
			// Check if we have enough arguments left
			if (arguments.Count < (getsInfo ? parameters.Length - 1 : parameters.Length))
			{
				if (returnTypes.Contains(CommandResultType.Command))
				{
					if (arguments.Count == 0)
						return new CommandCommandResult(this);
					return new CommandCommandResult(new PartialFunctionCommand(this, arguments));
				}
				throw new CommandException("Not enough arguments for function " + internCommand.Name);
			}
			// Fill the missing parameters
			for (int i = 0; parameter < parameters.Length; i++, parameter++)
			{
				var arg = internCommand.GetParameters()[parameter].ParameterType;
				if (arg == typeof(string))
					parameters[parameter] = arguments[i];
				else if (arg == typeof(int))
				{
					parameters[parameter] = arguments[i];
				}
				else
					throw new CommandException("Found inconvertable parameter type: " + arg.Name);
			}

			object result = internCommand.Invoke(callee, parameters);

			// Return the appropriate result
			if (internCommand.ReturnType == typeof(void))
				return new EmptyCommandResult();
			return new StringCommandResult(result.ToString());
		}
	}

	public interface ICommand
	{
		ICommandResult Execute(ExecutionInformation info, EnumerableCommandResult arguments, IEnumerable<CommandResultType> returnTypes);
	}

	public struct ExecutionInformation
	{
		public BotSession session;
		public TS3Query.Messages.TextMessage textMessage;
	}

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
			return "CommandResult can't be converted to a string";
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
		public abstract bool Flatten { get; }

		public abstract ICommandResult this[int index]{ get; }
	}

	public class EnumerableCommandResultRange : EnumerableCommandResult
	{
		readonly EnumerableCommandResult internResult;
		readonly int start;
		readonly int count;

		public override int Count => Math.Min(internResult.Count - start, count);
		public override bool Flatten => internResult.Flatten;

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
		public override bool Flatten => internResult.Any(r => r.Flatten);

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
		private readonly IEnumerable<ICommandResult> content;
		private readonly bool flatten;

		public override int Count => content.Count();
		public override bool Flatten => flatten;

		public override ICommandResult this[int index] => content.ElementAt(index);

		public StaticEnumerableCommandResult(IEnumerable<ICommandResult> contentArg, bool flattenArg = false)
		{
			content = contentArg;
			flatten = flattenArg;   // TODO implement flatten?
		}
	}

	public class StringCommandResult : ICommandResult
	{
		private readonly string content;

		public override CommandResultType ResultType => CommandResultType.String;
		public virtual string Content => content;

		public StringCommandResult(string contentArg)
		{
			content = contentArg;
		}
	}

	public class CommandException : Exception
	{
		public CommandException(string message) : base(message) {}
	}
}