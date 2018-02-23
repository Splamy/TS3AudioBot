// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.CommandResults
{
	using Web.Api;

	public class JsonCommandResult : ICommandResult
	{
		public CommandResultType ResultType => CommandResultType.Json;

		public JsonObject JsonObject { get; }

		public JsonCommandResult(JsonObject jsonObj)
		{
			JsonObject = jsonObj;
		}

		public override string ToString() => "JsonCommandResult can't be converted into a string";
	}
}
