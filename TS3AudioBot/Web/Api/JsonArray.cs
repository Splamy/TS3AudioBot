// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Web.Api
{
	using System;

	public class JsonArray<T> : JsonValue<T[]>
	{
		public JsonArray(T[] value, string msg) : base(value, msg) { }
		public JsonArray(T[] value, Func<T[], string> asString = null)
			: base(value, asString)
		{ }
	}
}
