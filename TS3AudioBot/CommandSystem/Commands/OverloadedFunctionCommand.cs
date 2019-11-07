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
using System.Linq;
using TS3AudioBot.Localization;

namespace TS3AudioBot.CommandSystem.Commands
{

	public class OverloadedFunctionCommand : ICommand
	{
		public List<FunctionCommand> Functions { get; }

		public OverloadedFunctionCommand() : this(Array.Empty<FunctionCommand>()) { }
		public OverloadedFunctionCommand(IEnumerable<FunctionCommand> functionsArg)
		{
			Functions = functionsArg.ToList();
		}

		public void AddCommand(FunctionCommand command)
		{
			Functions.Add(command);
			SortList();
		}
		public void RemoveCommand(FunctionCommand command) => Functions.Remove(command);

		private void SortList()
		{
			Functions.Sort((f1, f2) =>
			{
				// The first function in the list should be the most specialized.
				// If the execute the command we will iterate through the list from the beginning
				// and choose the first matching function.

				// Sort out special arguments
				// and remove the nullable wrapper
				var params1 = (from p in f1.CommandParameter
							   where p.Kind.IsNormal()
							   select FunctionCommand.UnwrapParamType(p.Type)).ToList();

				var params2 = (from p in f2.CommandParameter
							   where p.Kind.IsNormal()
							   select FunctionCommand.UnwrapParamType(p.Type)).ToList();

				for (int i = 0; i < params1.Count; i++)
				{
					// Prefer functions with higher parameter count
					if (i >= params2.Count)
						return -1;
					// Not found returns -1, so more important than any found index
					int i1 = Array.IndexOf(XCommandSystem.TypeOrder, params1[i]);
					int i2 = Array.IndexOf(XCommandSystem.TypeOrder, params2[i]);
					// Prefer lower argument
					if (i1 < i2)
						return -1;
					if (i1 > i2)
						return 1;
				}
				if (params2.Count > params1.Count)
					return 1;

				return 0;
			});
		}

		public virtual object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			// Make arguments lazy, we only want to execute them once
			arguments = arguments.Select(c => new LazyCommand(c)).ToArray();

			CommandException contextException = null;
			foreach (var f in Functions)
			{
				// Try to call each overload
				try
				{
					return f.Execute(info, arguments, returnTypes);
				}
				catch (CommandException cmdEx)
					when (cmdEx.Reason == CommandExceptionReason.MissingParameter
						|| cmdEx.Reason == CommandExceptionReason.MissingContext
						|| cmdEx.Reason == CommandExceptionReason.NoReturnMatch)
				{
					// When we encounter a missing module problem we store it for later, as it is more helpful
					// im most cases to know that some commands *could* have matched if the module were there.
					if (cmdEx.Reason == CommandExceptionReason.MissingContext)
						contextException = cmdEx;
				}
			}
			if (contextException != null)
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(contextException).Throw();
			throw new CommandException(strings.error_cmd_no_matching_overload, CommandExceptionReason.MissingParameter);
		}

		public override string ToString() => "<overload>";
	}
}
