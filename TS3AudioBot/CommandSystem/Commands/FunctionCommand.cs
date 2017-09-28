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
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Reflection;
	using Web.Api;

	public class FunctionCommand : ICommand
	{
		/// <summary>
		/// Parameter types of the underlying function that won't be filled normally.
		/// All theses types have a special meaning and don't count to the required parameters.
		/// </summary>
		public static readonly Type[] SpecialTypes = { typeof(ExecutionInformation), typeof(IReadOnlyList<ICommand>), typeof(IReadOnlyList<CommandResultType>) };

		// Needed for non-static member methods
		private readonly object callee;
		/// <summary>The amount of non-special parameter.</summary>
		public int NormalParameters { get; }
		/// <summary>
		/// The method that will be called internally by this command.
		/// </summary>
		private readonly MethodInfo internCommand;
		/// <summary>All parameter types, including special types.</summary>
		public Type[] CommandParameter { get; }
		public Type CommandReturn { get; }
		/// <summary>
		/// How many free arguments have to be applied to this function.
		/// This includes only user-supplied arguments, e.g. the ExecutionInformation is not included.
		/// </summary>
		public int RequiredParameters { get; private set; }

		public FunctionCommand(MethodInfo command, object obj = null, int? requiredParameters = null)
		{
			internCommand = command;
			CommandParameter = command.GetParameters().Select(p => p.ParameterType).ToArray();
			CommandReturn = command.ReturnType;

			callee = obj;
			// Require all parameters by default
			NormalParameters = CommandParameter.Count(p => !SpecialTypes.Contains(p));
			RequiredParameters = requiredParameters ?? NormalParameters;
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

		protected virtual object ExecuteFunction(object[] parameters)
		{
			try
			{
				return internCommand.Invoke(callee, parameters);
			}
			catch (TargetInvocationException ex)
			{
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
				throw ex.InnerException;
			}
		}

		/// <summary>
		/// Try to fit the given arguments to the underlying function.
		/// This function will throw an exception if the parameters can't be applied.
		/// The parameters that are extracted from the arguments will be returned if they can be applied successfully.
		/// </summary>
		/// <param name="info">The ExecutionInformation.</param>
		/// <param name="arguments">The arguments that are applied to this function.</param>
		/// <param name="returnTypes">The possible return types.</param>
		/// <param name="availableArguments">How many arguments could be set.</param>
		public object[] FitArguments(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes, out int availableArguments)
		{
			var parameters = new object[CommandParameter.Length];

			// availableArguments: Iterate through arguments
			// p: Iterate through parameters
			availableArguments = 0;
			for (int p = 0; p < parameters.Length; p++)
			{
				var arg = CommandParameter[p];
				if (arg == typeof(ExecutionInformation))
					parameters[p] = info;
				else if (arg == typeof(IReadOnlyList<ICommand>))
					parameters[p] = arguments;
				else if (arg == typeof(IReadOnlyList<CommandResultType>))
					parameters[p] = returnTypes;
				// Only add arguments if we still have some
				else if (availableArguments < arguments.Count)
				{
					if (arg.IsArray) // array
					{
						var typeArr = arg.GetElementType();
						var args = Array.CreateInstance(typeArr, arguments.Count - availableArguments);
						try
						{
							for (int i = 0; i < args.Length; i++, availableArguments++)
							{
								var argResult = ((StringCommandResult)arguments[availableArguments].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
								var convResult = ConvertParam(argResult, typeArr);
								args.SetValue(convResult, i);
							}
						}
						catch (FormatException ex) { throw new CommandException("Could not convert to " + arg.Name, ex, CommandExceptionReason.CommandError); }
						catch (OverflowException ex) { throw new CommandException("The number is too big.", ex, CommandExceptionReason.CommandError); }

						parameters[p] = args;
					}
					else // primitive value
					{
						var argResult = ((StringCommandResult)arguments[availableArguments].Execute(info, StaticList.Empty<ICommand>(), new[] { CommandResultType.String })).Content;
						try { parameters[p] = ConvertParam(argResult, arg); }
						catch (FormatException ex) { throw new CommandException("Could not convert to " + UnwrapType(arg).Name, ex, CommandExceptionReason.CommandError); }
						catch (OverflowException ex) { throw new CommandException("The number is too big.", ex, CommandExceptionReason.CommandError); }

						availableArguments++;
					}
				}
				else
					parameters[p] = GetDefault(arg);
			}

			// Check if we were able to set enough arguments
			if (availableArguments < Math.Min(parameters.Length, RequiredParameters) && !returnTypes.Contains(CommandResultType.Command))
				throw new CommandException("Not enough arguments for function " + internCommand.Name, CommandExceptionReason.MissingParameter);

			return parameters;
		}

		public override ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			// Make arguments lazy, we only want to execute them once
			arguments = arguments.Select(c => new LazyCommand(c)).ToArray();
			object[] parameters = FitArguments(info, arguments, returnTypes, out int availableArguments);

			// Check if we were able to set enough arguments
			if (availableArguments < Math.Min(parameters.Length, RequiredParameters))
			{
				if (returnTypes.Contains(CommandResultType.Command))
				{
					return arguments.Any()
						? new CommandCommandResult(new AppliedCommand(this, arguments))
						: new CommandCommandResult(this);
				}
				throw new CommandException("Not enough arguments for function " + internCommand.Name, CommandExceptionReason.MissingParameter);
			}

			if (CommandReturn == typeof(ICommandResult))
				return (ICommandResult)ExecuteFunction(parameters);

			bool executed = false;
			object result = null;
			// Take first fitting command result
			foreach (var returnType in returnTypes)
			{
				switch (returnType)
				{
				case CommandResultType.Command:
					// Return a command if we can take more arguments
					if (CommandParameter.Any(p => p == typeof(string[])) || availableArguments < NormalParameters)
						return new CommandCommandResult(new AppliedCommand(this, arguments));
					break;
				case CommandResultType.Empty:
					if (!executed)
						ExecuteFunction(parameters);
					return new EmptyCommandResult();
				case CommandResultType.String:
					if (!executed)
					{
						result = ExecuteFunction(parameters);
						executed = true;
					}
					if (!string.IsNullOrEmpty(result?.ToString()))
						return new StringCommandResult(result.ToString());
					break;
				case CommandResultType.Json:
					if (!executed)
					{
						result = ExecuteFunction(parameters);
						executed = true;
					}
					if (result is JsonObject jsonResult)
						return new JsonCommandResult(jsonResult);
					break;
				}
			}
			// Try to return an empty string
			if (returnTypes.Contains(CommandResultType.String) && executed)
				return new StringCommandResult("");
			throw new CommandException("Couldn't find a proper command result for function " + internCommand.Name, CommandExceptionReason.NoReturnMatch);
		}

		public static Type UnwrapType(Type type)
		{
			if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				return type.GenericTypeArguments[0];
			return type;
		}

		private static object ConvertParam(string value, Type targetType)
		{
			if (targetType == typeof(string))
				return value;
			if (targetType.IsEnum)
			{
				var enumVals = Enum.GetValues(targetType).Cast<Enum>();
				var result = XCommandSystem.FilterList(enumVals.Select(x => new KeyValuePair<string, Enum>(x.ToString(), x)), value).Select(x => x.Value).FirstOrDefault();
				if(result == null)
					throw new CommandException($"Invalid parameter \"{value}\"", CommandExceptionReason.MissingParameter);
				return result;
			}
			if (targetType.IsConstructedGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
				targetType = targetType.GenericTypeArguments[0];

			return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
		}

		private static object GetDefault(Type type)
		{
			if (type.IsArray)
			{
				var typeArr = type.GetElementType();
				return Array.CreateInstance(typeArr, 0);
			}
			if (type.IsValueType)
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
