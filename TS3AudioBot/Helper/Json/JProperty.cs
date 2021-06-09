// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TS3AudioBot.Helper.Json
{
	[JsonConverter(typeof(Converter))]
	public class JProperty
	{
		public string Key { get; }
		public object? Value { get; }

		public JProperty(string key, object? value)
		{
			Key = key;
			Value = value;
		}

		class Converter : JsonConverter<JProperty>
		{
			public override JProperty? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
			public override void Write(Utf8JsonWriter writer, JProperty value, JsonSerializerOptions options)
			{
				if (options.IgnoreNullValues && value.Value is null)
					return;
				writer.WritePropertyName(value.Key);
				JsonSerializer.Serialize(writer, value.Value, options);
			}
		}
	}
}
