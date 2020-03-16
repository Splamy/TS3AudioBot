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
	public class ResultCommand : ICommand
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public object Content { get; }

		public ResultCommand(object contentArg)
		{
			Content = contentArg;
		}

		public virtual object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			if (!ResultHelper.IsValidResult(Content, returnTypes))
			{
				Log.Debug("Failed to return {0} ({1})", Content.GetType(), Content);
				throw new CommandException(strings.error_cmd_no_matching_overload, CommandExceptionReason.NoReturnMatch);
			}
			return Content;
		}

		public override string ToString() => "<result>";
	}
}
