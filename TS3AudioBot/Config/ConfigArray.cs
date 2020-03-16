// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Nett;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TS3AudioBot.Helper;

namespace TS3AudioBot.Config
{
	public class ConfigArray<T> : ConfigValue<IReadOnlyList<T>>
	{
		public ConfigArray(string key, IReadOnlyList<T> defaultVal, string doc = "") : base(key, defaultVal, doc) { }

		public override void FromToml(TomlObject tomlObject)
		{
			if (tomlObject != null)
			{
				var array = tomlObject.TryGetValueArray<T>();
				if (array != null)
				{
					Value = array;
				}
			}
		}

		public override void ToJson(JsonWriter writer)
		{
			writer.WriteStartArray();
			foreach (var item in Value)
			{
				writer.WriteValue(item);
			}
			writer.WriteEndArray();
		}

		public override E<string> FromJson(JsonReader reader)
		{
			try
			{
				if (reader.Read()
					&& (reader.TokenType == JsonToken.StartArray))
				{
					var list = new List<T>();
					while (reader.TryReadValue<T>(out var value))
					{
						list.Add(value);
					}

					if (reader.TokenType != JsonToken.EndArray)
						return $"Expected end of array but found {reader.TokenType}";

					Value = list;
					return R.Ok;
				}
				return $"Wrong type, expected {typeof(T).Name}, got {reader.TokenType}";
			}
			catch (JsonReaderException ex) { return $"Could not read value: {ex.Message}"; }
		}
	}
}
