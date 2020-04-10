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
using System.Reflection;
using System.Threading.Tasks;
using TS3AudioBot.Web.Api;

namespace TS3AudioBot.CommandSystem.Commands
{
	public class PickObjectCommand : ICommand
	{
		public object Obj { get; }

		public PickObjectCommand(object obj)
		{
			Obj = obj;
		}

		public async ValueTask<object?> Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
		{
			if (Obj == null)
				return null; // TODO maybe error ?
			if (arguments.Count == 0)
				return JsonValue.Create(Obj);
			var paramName = await arguments[0].ExecuteToString(info, Array.Empty<ICommand>());
			var type = Obj.GetType();
			var prop = type.GetProperty(paramName, BindingFlags.Public | BindingFlags.Instance);
			if (prop is null)
				throw new CommandException("Property not found" /* TODO LOC */, CommandExceptionReason.CommandError);
			var val = prop.GetValue(Obj);
			if (val is null)
				return null;
			return JsonValue.Create(val);
		}
	}
}
