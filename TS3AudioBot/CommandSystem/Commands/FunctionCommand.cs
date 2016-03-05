namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.ComponentModel;

	public class FunctionCommand : ICommand
	{
		static readonly Type[] specialTypes = { typeof(ExecutionInformation), typeof(IEnumerable<ICommand>), typeof(IEnumerable<CommandResultType>) };
		static readonly TypeConverter stringConverter = TypeDescriptor.GetConverter(typeof(string));

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
			normalParameters = internCommand.GetParameters().Count(p => !specialTypes.Contains(p.ParameterType));
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

		public virtual ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			object[] parameters = new object[internCommand.GetParameters().Length];
			Lazy<List<ICommand>> argList = new Lazy<List<ICommand>>(() => arguments.ToList());

			// a: Iterate through arguments
			// p: Iterate through parameters
			int a = 0;
			for (int p = 0; p < parameters.Length; p++)
			{
				var arg = internCommand.GetParameters()[p].ParameterType;
				if (arg == typeof(ExecutionInformation))
					parameters[p] = info;
				else if (arg == typeof(IEnumerable<ICommand>))
					parameters[p] = arguments;
				else if (arg == typeof(IEnumerable<CommandResultType>))
					parameters[p] = returnTypes;
				// Only add arguments if we still have some
				else if (a < argList.Value.Count)
				{
					if (arg.IsArray) // array
					{
						var typeArr = arg.GetElementType();
						var args = Array.CreateInstance(typeArr, argList.Value.Count - a);
						try
						{
							for (int i = 0; i < args.Length; i++, a++)
							{
								var argResult = ((StringCommandResult)argList.Value[a].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
								var convResult = ConvertParam(argResult, typeArr);
								args.SetValue(convResult, i);
							}
						}
						catch (FormatException ex) { throw new CommandException("Could not convert to " + arg.Name, ex); }
						catch (OverflowException ex) { throw new CommandException("The number is too big.", ex); }

						parameters[p] = args;
					}
					else // primitive value
					{
						var argResult = ((StringCommandResult)argList.Value[a].Execute(info, Enumerable.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
						try { parameters[p] = ConvertParam(argResult, arg); }
						catch (FormatException ex) { throw new CommandException("Could not convert to " + GetTypeName(arg), ex); }
						catch (OverflowException ex) { throw new CommandException("The number is too big.", ex); }

						a++;
					}
					// TODO IEnumerable
				}
				else
					parameters[p] = GetDefault(arg);
			}

			// Check if we were able to set enough arguments
			if (a < Math.Min(parameters.Length, RequiredParameters))
			{
				if (returnTypes.Contains(CommandResultType.Command))
				{
					if (!arguments.Any())
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

		private static string GetTypeName(Type type)
		{
			if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				return type.GenericTypeArguments[0].Name;
			else
				return type.Name;
		}

		private static object ConvertParam(string value, Type targetType)
		{
			if (targetType == typeof(string))
				return value;
			else
			{
				if (targetType.IsConstructedGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
					targetType = targetType.GenericTypeArguments[0];

				return Convert.ChangeType(value, targetType);
			}
		}

		public static object GetDefault(Type type)
		{
			if (type.IsArray)
			{
				var typeArr = type.GetElementType();
				return Array.CreateInstance(typeArr, 0);
			}
			else if (type.IsValueType)
			{
				return Activator.CreateInstance(type);
			}
			return null;
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
