// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Nett;
using System;
using System.Collections.Generic;
using System.Text.Json;
using TS3AudioBot.Helper;

namespace TS3AudioBot.Config;

public class ConfigArray<T> : ConfigValue<IReadOnlyList<T>> where T : notnull
{
	public ConfigArray(string key, IReadOnlyList<T> defaultVal, string doc = "") : base(key, defaultVal, doc) { }

	public override void FromToml(TomlObject? tomlObject)
	{
		if (tomlObject != null && tomlObject.TryGetValueArray<T>(out var array))
		{
			Value = array;
		}
	}

	public override void ToJson(Utf8JsonWriter writer, JsonSerializerOptions options)
	{
		JsonSerializer.Serialize(writer, Value, options);
	}

	public override E<string> FromJson(ref Utf8JsonReader reader, JsonSerializerOptions options)
	{
		try
		{
			var value = JsonSerializer.Deserialize<T[]>(ref reader, options);
			value ??= Array.Empty<T>();
			Value = value;
			return R.Ok;
		}
		catch (JsonException ex) { return $"Could not read value: {ex.Message}"; }
	}
}
