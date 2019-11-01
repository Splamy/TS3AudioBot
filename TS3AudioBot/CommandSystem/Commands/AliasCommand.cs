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
using TS3AudioBot.Dependency;

namespace TS3AudioBot.CommandSystem.Commands
{
	public class AliasCommand : ICommand
	{
		private readonly ICommand aliasCommand;
		public string AliasString { get; }

		public AliasCommand(XCommandSystem root, string command)
		{
			var ast = CommandParser.ParseCommandRequest(command);
			aliasCommand = root.AstToCommandResult(ast);
			AliasString = command;
		}

		public object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
		{
			info.UseComplexityTokens(1);

			IReadOnlyList<ICommand> backupArguments = null;
			if (!info.TryGet<AliasContext>(out var aliasContext))
			{
				aliasContext = new AliasContext();
				info.AddModule(aliasContext);
			}
			else
			{
				backupArguments = aliasContext.Arguments;
			}

			aliasContext.Arguments = arguments.Select(c => new LazyCommand(c)).ToArray();
			var ret = aliasCommand.Execute(info, Array.Empty<ICommand>(), returnTypes);
			aliasContext.Arguments = backupArguments;
			return ret;
		}
	}

	public class AliasContext
	{
		public IReadOnlyList<ICommand> Arguments { get; set; }
	}
}
