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
	using Localization;
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
		public ParamInfo[] CommandParameter { get; }
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
			CommandParameter = command.GetParameters().Select(p => new ParamInfo(p, ParamKind.Unknown, p.IsOptional || p.GetCustomAttribute<ParamArrayAttribute>() != null)).ToArray();
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
		private object[] FitArguments(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes, out int takenArguments)
		{
			var parameters = new object[CommandParameter.Length];
			var filterLazy = new Lazy<Algorithm.Filter>(() => info.TryGet<Algorithm.Filter>(out var filter) ? filter : Algorithm.Filter.DefaultFilter, false);

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
						throw new MissingContextCommandException($"Command '{internCommand.Name}' missing execution context '{arg.Name}'", arg);
					break;

				case ParamKind.NormalCommand:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(arg); break; }
					parameters[p] = arguments[takenArguments];
					takenArguments++;
					break;

				case ParamKind.NormalParam:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(arg); break; }

					var argResultP = ((StringCommandResult)arguments[takenArguments].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
					parameters[p] = ConvertParam(argResultP, arg, filterLazy.Value.Current);

					takenArguments++;
					break;

				case ParamKind.NormalArray:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(arg); break; }

					var typeArr = arg.GetElementType();
					var args = Array.CreateInstance(typeArr, arguments.Count - takenArguments);
					for (int i = 0; i < args.Length; i++, takenArguments++)
					{
						var argResultA = ((StringCommandResult)arguments[takenArguments].Execute(info, Array.Empty<ICommand>(), XCommandSystem.ReturnString)).Content;
						var convResult = ConvertParam(argResultA, typeArr, filterLazy.Value.Current);
						args.SetValue(convResult, i);
					}

					parameters[p] = args;
					break;

				default:
					throw new ArgumentOutOfRangeException();
				}
			}

			// Check if we were able to set enough arguments
			int wantArgumentCount = Math.Min(parameters.Length, RequiredParameters);
			if (takenArguments < wantArgumentCount && !returnTypes.Contains(CommandResultType.Command))
				throw ThrowAtLeastNArguments(wantArgumentCount);

			return parameters;
		}

		public virtual ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			// Make arguments lazy, we only want to execute them once
			arguments = arguments.Select(c => new LazyCommand(c)).ToArray();
			var parameters = FitArguments(info, arguments, returnTypes, out int availableArguments);

			// Check if we were able to set enough arguments
			int wantArgumentCount = Math.Min(parameters.Length, RequiredParameters);
			if (availableArguments < wantArgumentCount)
			{
				if (returnTypes.Contains(CommandResultType.Command))
				{
					return arguments.Count > 0
						? new CommandCommandResult(new AppliedCommand(this, arguments))
						: new CommandCommandResult(this);
				}
				throw ThrowAtLeastNArguments(wantArgumentCount);
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
					return EmptyCommandResult.Instance;
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
				ref var paramInfo = ref CommandParameter[i];
				var arg = paramInfo.type;
				if (arg == typeof(IReadOnlyList<ICommand>))
					paramInfo.kind = ParamKind.SpecialArguments;
				else if (arg == typeof(IReadOnlyList<CommandResultType>))
					paramInfo.kind = ParamKind.SpecialReturns;
				else if (arg == typeof(ICommand))
					paramInfo.kind = ParamKind.NormalCommand;
				else if (arg.IsArray)
					paramInfo.kind = ParamKind.NormalArray;
				else if (arg.IsEnum
					|| XCommandSystem.BasicTypes.Contains(arg)
					|| XCommandSystem.BasicTypes.Contains(UnwrapParamType(arg)))
					paramInfo.kind = ParamKind.NormalParam;
				else
					paramInfo.kind = ParamKind.Dependency;
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
				if (genDef == typeof(JsonArray<>))
					return type.GenericTypeArguments[0].MakeArrayType();
			}
			return type;
		}

		public static CommandException ThrowAtLeastNArguments(int count)
		{
			if (count <= 0)
				throw new ArgumentOutOfRangeException(nameof(count), count, "The count must be at least 1");

			string throwString;
			switch (count)
			{
			case 1: throwString = strings.error_cmd_at_least_one_argument; break;
			case 2: throwString = strings.error_cmd_at_least_two_argument; break;
			case 3: throwString = strings.error_cmd_at_least_three_argument; break;
			case 4: throwString = strings.error_cmd_at_least_four_argument; break;
			default: throwString = string.Format(strings.error_cmd_at_least_n_arguments, count); break;
			}
			return new CommandException(throwString, CommandExceptionReason.MissingParameter);
		}

		private static object ConvertParam(string value, Type targetType, Algorithm.IFilterAlgorithm filter)
		{
			if (targetType == typeof(string))
				return value;
			if (targetType.IsEnum)
			{
				var enumVals = Enum.GetValues(targetType).Cast<Enum>();
				var result = filter.Filter(enumVals.Select(x => new KeyValuePair<string, Enum>(x.ToString(), x)), value).Select(x => x.Value).FirstOrDefault();
				if (result is null)
					throw new CommandException(string.Format(strings.error_cmd_could_not_convert_to, value, targetType.Name), CommandExceptionReason.MissingParameter);
				return result;
			}
			var unwrappedTargetType = UnwrapParamType(targetType);

			try { return Convert.ChangeType(value, unwrappedTargetType, CultureInfo.InvariantCulture); }
			catch (FormatException ex) { throw new CommandException(string.Format(strings.error_cmd_could_not_convert_to, value, unwrappedTargetType.Name), ex, CommandExceptionReason.MissingParameter); }
			catch (OverflowException ex) { throw new CommandException(strings.error_cmd_number_too_big, ex, CommandExceptionReason.MissingParameter); }
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

	public struct ParamInfo
	{
		public ParameterInfo param;
		public Type type => param.ParameterType;
		public ParamKind kind;
		public bool optional;

		public ParamInfo(ParameterInfo param, ParamKind kind, bool optional)
		{
			this.param = param;
			this.kind = kind;
			this.optional = optional;
		}
	}

	public static class FunctionCommandExtensions
	{
		public static bool IsNormal(this ParamKind kind) => kind == ParamKind.NormalParam || kind == ParamKind.NormalArray || kind == ParamKind.NormalCommand;
	}
}
