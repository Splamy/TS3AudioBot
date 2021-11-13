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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TS3AudioBot.Helper.Json;

[JsonConverter(typeof(Converter))]
public class JObject
{
	private JsonSerializerOptions? customOptions;
	private List<object?>? properties;

	public JObject(params object?[] props)
	{
		if (props.Length > 0)
			properties = new List<object?>(props);
	}

	public JObject Add(object? prop)
	{
		(properties ??= new()).Add(prop);
		return this;
	}

	public JObject CustomJsonOptions(JsonSerializerOptions options)
	{
		customOptions = options;
		return this;
	}

	class Converter : JsonConverter<JObject>
	{
		public override JObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
		public override void Write(Utf8JsonWriter writer, JObject value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			if (value.properties != null)
			{
				foreach (var prop in value.properties)
				{
					JsonSerializer.Serialize(writer, prop, value.customOptions ?? options);
				}
			}
			writer.WriteEndObject();
		}
	}
}
