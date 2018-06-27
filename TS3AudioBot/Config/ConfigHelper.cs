// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Config
{
	using CommandSystem;
	using Newtonsoft.Json;
	using System;
	using System.Linq;

	public static class ConfigHelper
	{
		public static ConfigPart[] ByPathAsArray(this ConfigPart config, string path)
		{
			try
			{
				return config.ByPath(path).ToArray();
			}
			catch (Exception ex)
			{
				throw new CommandException("Invalid TomlPath expression", ex, CommandExceptionReason.CommandError);
			}
		}

		public static bool TryReadValue<T>(this JsonReader reader, out T value)
		{
			if (reader.Read()
				&& (reader.TokenType == JsonToken.Boolean
				|| reader.TokenType == JsonToken.Date
				|| reader.TokenType == JsonToken.Float
				|| reader.TokenType == JsonToken.Integer
				|| reader.TokenType == JsonToken.String))
			{
				value = (T)Convert.ChangeType(reader.Value, typeof(T));
				return true;
			}
			value = default;
			return false;
		}
	}
}
