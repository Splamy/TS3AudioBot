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
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.Localization;

namespace TS3AudioBot.CommandSystem.Commands
{
	/// <summary>
	/// A command that stores a result and returns it.
	/// </summary>
	public class AutoConvertResultCommand : ICommand
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public IConvertible Content { get; }

		public AutoConvertResultCommand(IConvertible contentArg)
		{
			Content = contentArg;
		}

		public virtual object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			var filterLazy = info.GetFilterLazy();
			foreach (var type in returnTypes)
			{
				try
				{
					var result = FunctionCommand.ConvertParam(Content, type, filterLazy);
					Log.Debug("Converting command result {0} to {1} returns {2}", Content, type, result);

					return ResultHelper.ToResult(type, result);
				}
				catch (Exception ex)
				{
					Log.Debug(ex, "Converting command result {0} to {1} failed", Content, type);
				}
			}
			throw new CommandException(strings.error_cmd_no_matching_overload, CommandExceptionReason.NoReturnMatch);
		}

		public override string ToString() => "<auto-convert-result>";
	}
}
