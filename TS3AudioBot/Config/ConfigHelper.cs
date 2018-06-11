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
	using Newtonsoft.Json;
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using TS3AudioBot.CommandSystem;

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

		public static E<string> FromJson(this IJsonConfig jsonConfig, string json)
		{
			var sr = new StringReader(json);
			using (var reader = new JsonTextReader(sr))
			{
				return jsonConfig.FromJson(reader);
			}
		}

		public static string ToJson(this IJsonConfig jsonConfig)
		{
			var sb = new StringBuilder();
			var sw = new StringWriter(sb);
			using (var writer = new JsonTextWriter(sw))
			{
				writer.Formatting = Formatting.Indented;
				jsonConfig.ToJson(writer);
			}
			return sb.ToString();
		}
	}
}
