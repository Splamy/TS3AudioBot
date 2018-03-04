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
	using System.Globalization;
	using System.Linq;
	using System.Reflection;
	using Web.Api;

	public class FunctionCommand : ICommand
	{
		// Needed for non-static member methods
		private readonly object callee;
		/// <summary>The method that will be called internally by this command.</summary>
		private readonly MethodInfo internCommand;

		/// <summary>All parameter types, including special types.</summary>
		public (Type type, ParamKind kind, bool optional)[] CommandParameter { get; }
		/// <summary>Return type of method.</summary>
		public Type CommandReturn { get; }
		/// <summary>Count of parameter, without special types.</summary>
		public int NormalParameters { get; }
		/// <summary>
		/// How many free arguments have to be applied to this function.
		/// This includes only user-supplied arguments, e.g. the <see cref="ExecutionInformation"/> is not included.
		/// </summary>
		private int RequiredParameters { get; }

		public FunctionCommand(MethodInfo command, object obj = null, int? requiredParameters = null)
		{
			internCommand = command;
			CommandParameter = command.GetParameters().Select(p => (p.ParameterType, ParamKind.Unknown, p.IsOptional || p.GetCustomAttribute<ParamArrayAttribute>() != null)).ToArray();
			PrecomputeTypes();
			CommandReturn = command.ReturnType;

			callee = obj;

			NormalParameters = CommandParameter.Count(p => p.kind.IsNormal());
			RequiredParameters = requiredParameters ?? CommandParameter.Count(p => !p.optional && p.kind.IsNormal());
		}

		// Provide some constructors that take lambda expressions directly
		public FunctionCommand(Delegate command, int? requiredParameters = null) : this(command.Method, command.Target, requiredParameters) { }
		public FunctionCommand(Action command) : this(command.Method, command.Target) { }
		public FunctionCommand(Func<string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Action<string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Func<string, string> command) : this(command.Method, command.Target) { }

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
		/// <param name="info">The current call <see cref="ExecutionInformation"/>.</param>
		/// <param name="arguments">The arguments that are applied to this function.</param>
		/// <param name="returnTypes">The possible return types.</param>
		/// <param name="takenArguments">How many arguments could be set.</param>
		public R<object[], CommandException> FitArguments(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes, out int takenArguments)
		{
			var parameters = new object[CommandParameter.Length];

			// takenArguments: Index through arguments which have been moved into a parameter
			// p: Iterate through parameters
			takenArguments = 0;
			for (int p = 0; p < parameters.Length; p++)
			{
				var arg = CommandParameter[p].type;
				switch (CommandParameter[p].kind)
				{
				case ParamKind.SpecialArguments:
					parameters[p] = arguments;
					break;

				case ParamKind.SpecialReturns:
					parameters[p] = returnTypes;
					break;

				case ParamKind.Dependency:
					if (info.TryGet(arg, out var obj))
						parameters[p] = obj;
					else if (CommandParameter[p].optional)
						parameters[p] = null;
					else
						return new CommandException($"Command '{internCommand.Name}' missing execution context '{arg.Name}'", CommandExceptionReason.MissingContext);
					break;

				case ParamKind.NormalCommand:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(arg); break; }
					parameters[p] = arguments[takenArguments];
					takenArguments++;
					break;

				case ParamKind.NormalParam:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(arg); break; }

					var argResultP = ((StringCommandResult)arguments[takenArguments].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
					try { parameters[p] = ConvertParam(argResultP, arg); }
					catch (FormatException ex) { return new CommandException("Could not convert to " + UnwrapParamType(arg).Name, ex, CommandExceptionReason.CommandError); }
					catch (OverflowException ex) { return new CommandException("The number is too big.", ex, CommandExceptionReason.CommandError); }

					takenArguments++;
					break;

				case ParamKind.NormalArray:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(arg); break; }

					var typeArr = arg.GetElementType();
					var args = Array.CreateInstance(typeArr, arguments.Count - takenArguments);
					try
					{
						for (int i = 0; i < args.Length; i++, takenArguments++)
						{
							var argResultA = ((StringCommandResult)arguments[takenArguments].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
							var convResult = ConvertParam(argResultA, typeArr);
							args.SetValue(convResult, i);
						}
					}
					catch (FormatException ex) { return new CommandException("Could not convert to " + arg.Name, ex, CommandExceptionReason.CommandError); }
					catch (OverflowException ex) { return new CommandException("The number is too big.", ex, CommandExceptionReason.CommandError); }

					parameters[p] = args;
					break;

				default:
					throw new ArgumentOutOfRangeException();
				}
			}

			// Check if we were able to set enough arguments
			if (takenArguments < Math.Min(parameters.Length, RequiredParameters) && !returnTypes.Contains(CommandResultType.Command))
				throw new CommandException("Not enough arguments for function " + internCommand.Name, CommandExceptionReason.MissingParameter);

			return parameters;
		}

		public virtual ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			// Make arguments lazy, we only want to execute them once
			arguments = arguments.Select(c => new LazyCommand(c)).ToArray();
			var fitresult = FitArguments(info, arguments, returnTypes, out int availableArguments);
			if (!fitresult)
				throw fitresult.Error;
			object[] parameters = fitresult.Value;

			// Check if we were able to set enough arguments
			if (availableArguments < Math.Min(parameters.Length, RequiredParameters))
			{
				if (returnTypes.Contains(CommandResultType.Command))
				{
					return arguments.Count > 0
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
					if (CommandParameter.Any(p => p.type == typeof(string[])) || availableArguments < NormalParameters)
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
					var resultStr = result?.ToString();
					if (!string.IsNullOrEmpty(resultStr))
						return new StringCommandResult(resultStr);
					break;
				case CommandResultType.Json:
					if (!executed)
					{
						result = ExecuteFunction(parameters);
						executed = true;
					}

					switch (result)
					{
					case null: break;
					case JsonObject jsonResult: return new JsonCommandResult(jsonResult);
					default: return new JsonCommandResult((JsonObject)Activator.CreateInstance(typeof(JsonValue<>).MakeGenericType(result.GetType()), result));
					}
					break;

				default:
					throw new ArgumentOutOfRangeException();
				}
			}
			// Try to return an empty string
			if (returnTypes.Contains(CommandResultType.String) && executed)
				return new StringCommandResult("");
			throw new CommandException("Couldn't find a proper command result for function " + internCommand.Name, CommandExceptionReason.NoReturnMatch);
		}

		private void PrecomputeTypes()
		{
			for (int i = 0; i < CommandParameter.Length; i++)
			{
				var arg = CommandParameter[i].type;
				if (arg == typeof(IReadOnlyList<ICommand>))
					CommandParameter[i].kind = ParamKind.SpecialArguments;
				else if (arg == typeof(IReadOnlyList<CommandResultType>))
					CommandParameter[i].kind = ParamKind.SpecialReturns;
				else if (arg == typeof(ICommand))
					CommandParameter[i].kind = ParamKind.NormalCommand;
				else if (arg.IsArray)
					CommandParameter[i].kind = ParamKind.NormalArray;
				else if (arg.IsEnum
					|| XCommandSystem.BasicTypes.Contains(arg)
					|| XCommandSystem.BasicTypes.Contains(UnwrapParamType(arg)))
					CommandParameter[i].kind = ParamKind.NormalParam;
				else
					CommandParameter[i].kind = ParamKind.Dependency;
			}
		}

		public static Type UnwrapParamType(Type type)
		{
			if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				return type.GenericTypeArguments[0];
			return type;
		}

		public static Type UnwrapReturnType(Type type)
		{
			if (type.IsConstructedGenericType)
			{
				var genDef = type.GetGenericTypeDefinition();
				if (genDef == typeof(Nullable<>))
					return type.GenericTypeArguments[0];
				if (genDef == typeof(JsonValue<>))
					return type.GenericTypeArguments[0];
			}
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
				if (result == null)
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
	}

	public enum ParamKind
	{
		Unknown,
		SpecialArguments,
		SpecialReturns,
		Dependency,
		NormalCommand,
		NormalParam,
		NormalArray,
	}

	public static class FunctionCommandExtensions
	{
		public static bool IsNormal(this ParamKind kind) => kind == ParamKind.NormalParam || kind == ParamKind.NormalArray || kind == ParamKind.NormalCommand;
	}
}
