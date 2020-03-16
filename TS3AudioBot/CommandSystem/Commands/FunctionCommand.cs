// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.Dependency;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Web.Api;
using TSLib.Helper;
using static TS3AudioBot.CommandSystem.CommandSystemTypes;

namespace TS3AudioBot.CommandSystem.Commands
{
	public class FunctionCommand : ICommand
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		// Needed for non-static member methods
		private readonly object? callee;
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

		public FunctionCommand(MethodInfo command, object? obj = null, int? requiredParameters = null)
		{
			internCommand = command;
			CommandParameter = command.GetParameters().Select(p => new ParamInfo(p, ParamKind.Unknown, p.IsOptional || p.GetCustomAttribute<ParamArrayAttribute>() != null)).ToArray();
			PrecomputeTypes();
			CommandReturn = command.ReturnType;

			callee = obj;

			NormalParameters = CommandParameter.Count(p => p.Kind.IsNormal());
			RequiredParameters = requiredParameters ?? CommandParameter.Count(p => !p.Optional && p.Kind.IsNormal());
		}

		// Provide some constructors that take lambda expressions directly
		public FunctionCommand(Delegate command, int? requiredParameters = null) : this(command.Method, command.Target, requiredParameters) { }
		public FunctionCommand(Action command) : this(command.Method, command.Target) { }
		public FunctionCommand(Func<string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Action<string> command) : this(command.Method, command.Target) { }
		public FunctionCommand(Func<string, string> command) : this(command.Method, command.Target) { }

		protected virtual object? ExecuteFunction(object?[] parameters)
		{
			try
			{
				return internCommand.Invoke(callee, parameters);
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException is null)
					throw;
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
		private object?[] FitArguments(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type?> returnTypes, out int takenArguments)
		{
			var parameters = new object?[CommandParameter.Length];
			var filterLazy = info.GetFilterLazy();

			// takenArguments: Index through arguments which have been moved into a parameter
			// p: Iterate through parameters
			takenArguments = 0;
			for (int p = 0; p < parameters.Length; p++)
			{
				var arg = CommandParameter[p];
				var argType = arg.Type;
				switch (arg.Kind)
				{
				case ParamKind.SpecialArguments:
					parameters[p] = arguments;
					break;

				case ParamKind.SpecialReturns:
					parameters[p] = returnTypes;
					break;

				case ParamKind.Dependency:
					if (info.TryGet(argType, out var obj))
						parameters[p] = obj;
					else if (arg.Optional)
						parameters[p] = null;
					else
						throw new MissingContextCommandException($"Command '{internCommand.Name}' missing execution context '{argType.Name}'", argType);
					break;

				case ParamKind.NormalCommand:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(argType); break; }
					parameters[p] = arguments[takenArguments];
					takenArguments++;
					break;

				case ParamKind.NormalParam:
				case ParamKind.NormalTailString:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(argType); break; }

					var types = GetTypes(argType);
					if (arg.Kind == ParamKind.NormalTailString)
						types.Insert(0, typeof(TailString));

					var argResultP = arguments[takenArguments].Execute(info, Array.Empty<ICommand>(), types);
					if (arg.Kind == ParamKind.NormalTailString && argResultP is TailString tailString)
						parameters[p] = tailString.Tail;
					else
						parameters[p] = ConvertParam(UnwrapPrimitive(argResultP), argType, filterLazy);

					takenArguments++;
					break;

				case ParamKind.NormalArray:
					if (takenArguments >= arguments.Count) { parameters[p] = GetDefault(argType); break; }

					var typeArr = argType.GetElementType()!;
					var args = Array.CreateInstance(typeArr, arguments.Count - takenArguments);
					for (int i = 0; i < args.Length; i++, takenArguments++)
					{
						var argResultA = arguments[takenArguments].Execute(info, Array.Empty<ICommand>(), GetTypes(typeArr));
						var convResult = ConvertParam(UnwrapPrimitive(argResultA), typeArr, filterLazy);
						args.SetValue(convResult, i);
					}

					parameters[p] = args;
					break;

				default:
					throw Tools.UnhandledDefault(arg.Kind);
				}
			}

			// Check if we were able to set enough arguments
			int wantArgumentCount = Math.Min(parameters.Length, RequiredParameters);
			if (takenArguments < wantArgumentCount && !returnTypes.Contains(typeof(ICommand)))
				throw ThrowAtLeastNArguments(wantArgumentCount);

			return parameters;
		}

		public virtual object? Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type?> returnTypes)
		{
			// Make arguments lazy, we only want to execute them once
			arguments = arguments.Select(c => new LazyCommand(c)).ToArray();
			var parameters = FitArguments(info, arguments, returnTypes, out int availableArguments);

			// Check if we were able to set enough arguments
			int wantArgumentCount = Math.Min(parameters.Length, RequiredParameters);
			if (availableArguments < wantArgumentCount)
			{
				if (returnTypes.Contains(typeof(ICommand)))
				{
					return arguments.Count > 0
						? (object)new AppliedCommand(this, arguments)
						: this;
				}
				throw ThrowAtLeastNArguments(wantArgumentCount);
			}

			if (returnTypes[0] == null)
			{
				// Evaluate
				ExecuteFunction(parameters);
				return null;
			}

			if (returnTypes[0] == typeof(ICommand))
			{
				// Return a command if we can take more arguments
				if (CommandParameter.Any(p => p.Type == typeof(string[])) || availableArguments < NormalParameters)
					return new AppliedCommand(this, arguments);
			}

			Log.Debug("Iterating over return types [{@returnTypes}]", returnTypes);

			var result = ExecuteFunction(parameters);
			if (ResultHelper.IsValidResult(result, returnTypes))
			{
				Log.Debug("{0} can be directly returned", result);
				return result;
			}

			if (result is null)
				throw new CommandException("Couldn't find a proper command result for function " + internCommand.Name, CommandExceptionReason.NoReturnMatch);
			var unwrapedResult = UnwrapReturn(result);
			var resultType = result.GetType();
			var unwrapedResultType = unwrapedResult.GetType();

			// Take first fitting command result
			foreach (var returnType in returnTypes)
			{
				if (returnType == null)
					return null;

				if (returnType.IsAssignableFrom(resultType))
					return ResultHelper.ToResult(returnType, result);
				else if (returnType.IsAssignableFrom(unwrapedResultType))
					return ResultHelper.ToResult(returnType, unwrapedResult);
				else if (returnType == typeof(string))
				{
					Log.Debug("Convert {0} to a string", result);
					var resultStr = result.ToString();
					if (!string.IsNullOrEmpty(resultStr))
						return new PrimitiveResult<string>(resultStr);
				}
				else if (BasicTypes.Contains(returnType))
				{
					if (BasicTypes.Contains(unwrapedResultType) && unwrapedResultType != typeof(string))
					{
						// Automatically try to convert between primitive types
						try
						{
							return ResultHelper.ToResult(resultType,
								Convert.ChangeType(unwrapedResult, returnType, CultureInfo.InvariantCulture));
						}
						catch
						{
						}
					}
				}
				else if (returnType == typeof(JsonObject))
				{
					if (result is JsonObject jsonResult)
						return jsonResult;
					else
						return Activator.CreateInstance(typeof(JsonValue<>).MakeGenericType(result.GetType()), result);
				}
				// Ignore unknown types
			}
			throw new CommandException("Couldn't find a proper command result for function " + internCommand.Name, CommandExceptionReason.NoReturnMatch);
		}

		private void PrecomputeTypes()
		{
			for (int i = 0; i < CommandParameter.Length; i++)
			{
				ref var paramInfo = ref CommandParameter[i];
				var arg = paramInfo.Type;
				if (arg == typeof(IReadOnlyList<ICommand>))
					paramInfo.Kind = ParamKind.SpecialArguments;
				else if (arg == typeof(IReadOnlyList<Type>))
					paramInfo.Kind = ParamKind.SpecialReturns;
				else if (arg == typeof(ICommand))
					paramInfo.Kind = ParamKind.NormalCommand;
				else if (arg.IsArray)
					paramInfo.Kind = ParamKind.NormalArray;
				else if (arg.IsEnum
					|| BasicTypes.Contains(arg)
					|| BasicTypes.Contains(UnwrapParamType(arg)))
					paramInfo.Kind = ParamKind.NormalParam;
				// TODO How to distinguish between special type and dependency?
				else if (AdvancedTypes.Contains(arg))
					paramInfo.Kind = ParamKind.NormalParam;
				else
					paramInfo.Kind = ParamKind.Dependency;
			}

			var tailStringIndex = Array.FindLastIndex(CommandParameter, c => c.Kind.IsNormal());
			if (tailStringIndex >= 0 && CommandParameter[tailStringIndex].Type == typeof(string))
				CommandParameter[tailStringIndex].Kind = ParamKind.NormalTailString;
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

		[return: NotNullIfNotNull("value")]
		private static object? UnwrapReturn(object? value)
		{
			if (value is null)
				return value;

			if (value is JsonValue jValue)
				return jValue.Value;

			var type = value.GetType();
			if (type.IsConstructedGenericType)
			{
				var genDef = type.GetGenericTypeDefinition();
				if (genDef == typeof(Nullable<>))
					return type.GetProperty("Value")!.GetValue(value);
			}
			return value;
		}

		public static CommandException ThrowAtLeastNArguments(int count)
		{
			if (count <= 0)
				throw new ArgumentOutOfRangeException(nameof(count), count, "The count must be at least 1");
			var throwString = count switch
			{
				1 => strings.error_cmd_at_least_one_argument,
				2 => strings.error_cmd_at_least_two_argument,
				3 => strings.error_cmd_at_least_three_argument,
				4 => strings.error_cmd_at_least_four_argument,
				_ => string.Format(strings.error_cmd_at_least_n_arguments, count),
			};
			return new CommandException(throwString, CommandExceptionReason.MissingParameter);
		}

		[return: NotNullIfNotNull("value")]
		public static object? ConvertParam(object? value, Type targetType, Lazy<Algorithm.IFilter> filter)
		{
			if (value is null)
				return null;
			var valueType = value.GetType();
			if (targetType.IsAssignableFrom(valueType))
				return value;
			if (targetType.IsEnum)
			{
				var strValue = value.ToString() ?? throw new ArgumentNullException(nameof(value));
				var enumVals = Enum.GetValues(targetType).Cast<Enum>();
				var result = filter.Value.Filter(enumVals.Select(x => new KeyValuePair<string, Enum>(x.ToString(), x)), strValue).Select(x => x.Value).FirstOrDefault();
				if (result is null)
					throw new CommandException(string.Format(strings.error_cmd_could_not_convert_to, strValue, targetType.Name), CommandExceptionReason.MissingParameter);
				return result;
			}
			var unwrappedTargetType = UnwrapParamType(targetType);
			if (valueType == typeof(string) && unwrappedTargetType == typeof(TimeSpan))
			{
				var time = TextUtil.ParseTime((string)value);
				if (time is null)
					throw new CommandException(string.Format(strings.error_cmd_could_not_convert_to, value, nameof(TimeSpan)), CommandExceptionReason.MissingParameter);
				return time.Value;
			}

			// Autoconvert
			try { return Convert.ChangeType(value, unwrappedTargetType, CultureInfo.InvariantCulture); }
			catch (FormatException ex) { throw new CommandException(string.Format(strings.error_cmd_could_not_convert_to, value, unwrappedTargetType.Name), ex, CommandExceptionReason.MissingParameter); }
			catch (OverflowException ex) { throw new CommandException(strings.error_cmd_number_too_big, ex, CommandExceptionReason.MissingParameter); }
			catch (InvalidCastException ex) { throw new CommandException(string.Format(strings.error_cmd_could_not_convert_to, value, unwrappedTargetType.Name), ex, CommandExceptionReason.MissingParameter); }
		}

		private static List<Type> GetTypes(Type targetType)
		{
			var types = new List<Type> { targetType };
			var unwrappedTargetType = UnwrapParamType(targetType);
			if (unwrappedTargetType != targetType)
				types.Add(unwrappedTargetType);

			// Allow fallbacks to string
			if (!types.Contains(typeof(string)))
				types.Add(typeof(string));
			return types;
		}

		[return: NotNullIfNotNull("o")]
		private static object? UnwrapPrimitive(object? o)
		{
			if (o is null)
				return null;
			if (o is IPrimitiveResult prim)
				return prim.Get();
			else
				return o;
		}

		private static object? GetDefault(Type type)
		{
			if (type.IsArray)
			{
				var typeArr = type.GetElementType()!;
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
		NormalTailString,
	}

	public struct ParamInfo
	{
		public ParameterInfo Param { get; set; }
		public Type Type => Param.ParameterType;
		public string Name => Param.Name ?? "<no name>";
		public ParamKind Kind;
		public bool Optional;

		public ParamInfo(ParameterInfo param, ParamKind kind, bool optional)
		{
			Param = param;
			Kind = kind;
			Optional = optional;
		}
	}

	public static class FunctionCommandExtensions
	{
		public static bool IsNormal(this ParamKind kind)
			=> kind == ParamKind.NormalParam
			|| kind == ParamKind.NormalArray
			|| kind == ParamKind.NormalCommand
			|| kind == ParamKind.NormalTailString;
	}
}
