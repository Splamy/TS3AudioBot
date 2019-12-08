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
using TS3AudioBot.Algorithm;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.Dependency;
using TS3AudioBot.Localization;
using static TS3AudioBot.CommandSystem.CommandSystemTypes;

namespace TS3AudioBot.CommandSystem
{
	public static class CommandSystemExtensions
	{
		public static IFilter GetFilter(this IInjector injector)
		{
			if (injector.TryGet<IFilter>(out var filter))
				return filter;
			return Filter.DefaultFilter;
		}

		public static Lazy<IFilter> GetFilterLazy(this IInjector injector)
			=> new Lazy<IFilter>(() => injector.GetFilter(), false);

		public static string ExecuteToString(this ICommand com, ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			var res = com.Execute(info, arguments, ReturnString);
			if (res is IPrimitiveResult<string> primStr)
				return primStr.Get();
			throw new CommandException(string.Format(strings.error_cmd_could_not_convert_to, res, nameof(IPrimitiveResult<string>)), CommandExceptionReason.MissingParameter);
		}
	}
}
