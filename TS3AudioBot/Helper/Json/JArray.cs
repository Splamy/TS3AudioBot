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
public class JArray
{
	private List<object?>? values;

	public JArray(params object?[] props)
	{
		if (props.Length > 0)
			values = new List<object?>(props);
	}

	public void Add(object? prop) => (values ??= new()).Add(prop);

	class Converter : JsonConverter<JArray>
	{
		public override JArray? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
		public override void Write(Utf8JsonWriter writer, JArray value, JsonSerializerOptions options)
		{
			writer.WriteStartArray();
			if (value.values != null)
			{
				foreach (var prop in value.values)
				{
					JsonSerializer.Serialize(writer, prop, options);
				}
			}
			writer.WriteEndArray();
		}
	}
}
