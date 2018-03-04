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
	using System.Linq;

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
							   where p.kind.IsNormal()
							   select FunctionCommand.UnwrapParamType(p.type)).ToList();

				var params2 = (from p in f2.CommandParameter
							   where p.kind.IsNormal()
							   select FunctionCommand.UnwrapParamType(p.type)).ToList();

				for (int i = 0; i < params1.Count; i++)
				{
					// Prefer functions with higher parameter count
					if (i >= params2.Count)
						return -1;
					int i1 = Array.IndexOf(XCommandSystem.TypeOrder, params1[i]);
					if (i1 == -1)
						i1 = XCommandSystem.TypeOrder.Length;
					int i2 = Array.IndexOf(XCommandSystem.TypeOrder, params2[i]);
					if (i2 == -1)
						i2 = XCommandSystem.TypeOrder.Length;
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

		public virtual ICommandResult Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<CommandResultType> returnTypes)
		{
			// Make arguments lazy, we only want to execute them once
			arguments = arguments.Select(c => new LazyCommand(c)).ToArray();
			foreach (var f in Functions)
			{
				// Find out if this overload works
				var fitresult = f.FitArguments(info, arguments, returnTypes, out var _);
				if (fitresult.Ok)
				{
					// Call this overload
					return f.Execute(info, arguments, returnTypes);
				}

				if (fitresult.Error.Reason == CommandExceptionReason.MissingContext)
					throw fitresult.Error;

			}
			throw new CommandException("No matching function overload could be found", CommandExceptionReason.FunctionNotFound);
		}

	}
}
