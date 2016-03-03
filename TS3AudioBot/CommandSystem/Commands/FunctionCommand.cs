namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;

	public class FunctionCommand : ICommand
	{
		static readonly Type[] SpecialTypes
			= new Type[] { typeof(ExecutionInformation), typeof(IEnumerableCommand), typeof(IEnumerable<CommandResultType>) };

		// Needed for non-static member methods
		readonly object callee;
		readonly MethodInfo internCommand;
		readonly int normalParameters;
		/// <summary>
		/// How many free arguments have to be applied to this function.
		/// This includes only user-supplied arguments, e.g. the ExecutionInformation is not included.
		/// </summary>
		public int RequiredParameters { get; private set; }

		public FunctionCommand(MethodInfo command, object obj = null, int? requiredParameters = null)
		{
			internCommand = command;
			callee = obj;
			// Require all parameters by default
			normalParameters = internCommand.GetParameters().Count(p => !SpecialTypes.Contains(p.ParameterType));
			RequiredParameters = requiredParameters ?? normalParameters;
		}

		// Provide some constructors that take lambda expressions directly
		public FunctionCommand(Action command) : this(command.Method, command.Target) { }
		public FunctionCommand(Func<string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Action<string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Func<string, string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Action<ExecutionInformation> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Func<ExecutionInformation, string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Action<ExecutionInformation, string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Func<ExecutionInformation, string, string> command) : this(command.Method, command.Target) { }

		object ExecuteFunction(object[] parameters)
		{
			try
			{
				return internCommand.Invoke(callee, parameters);
			}
			catch (TargetInvocationException ex)
			{
				throw ex.InnerException;
			}
		}

		public virtual ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
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
				else if (arg == typeof(IEnumerableCommand))
					parameters[p] = arguments;
				else if (arg == typeof(IEnumerable<CommandResultType>))
					parameters[p] = returnTypes;
				// Only add arguments if we still have some
				else if (a < arguments.Count)
				{
					var argResult = ((StringCommandResult)arguments.Execute(a, info, new EmptyEnumerableCommand(), new[] { CommandResultType.String })).Content;
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
							args[i] = ((StringCommandResult)arguments.Execute(a, info, new EmptyEnumerableCommand(),
								new[] { CommandResultType.String })).Content;
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

			if (internCommand.ReturnType == typeof(ICommandResult))
				return (ICommandResult)ExecuteFunction(parameters);

			bool executed = false;
			object result = null;
			// Take first fitting command result
			foreach (var returnType in returnTypes)
			{
				switch (returnType)
				{
				case CommandResultType.Command:
					// Return a command if possible
					// Only do this if the command was not yet executed to prevent executing a command more than once
					if (!executed &&
						(internCommand.GetParameters().Any(p => p.ParameterType == typeof(string[])) ||
						 a < normalParameters))
						return new CommandCommandResult(new AppliedCommand(this, arguments));
					break;
				case CommandResultType.Empty:
					if (!executed)
						ExecuteFunction(parameters);
					return new EmptyCommandResult();
				case CommandResultType.Enumerable:
					if (internCommand.ReturnType == typeof(string[]))
					{
						if (!executed)
							result = ExecuteFunction(parameters);
						return new StaticEnumerableCommandResult(((string[])result).Select(s => new StringCommandResult(s)));
					}
					break;
				case CommandResultType.String:
					if (!executed)
					{
						result = ExecuteFunction(parameters);
						executed = true;
					}
					if (result != null && !string.IsNullOrEmpty(result.ToString()))
						return new StringCommandResult(result.ToString());
					break;
				}
			}
			// Try to return an empty string
			if (returnTypes.Contains(CommandResultType.String) && executed)
				return new StringCommandResult("");
			throw new CommandException("Couldn't find a proper command result for function " + internCommand.Name);
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

}
