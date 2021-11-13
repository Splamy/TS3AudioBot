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

namespace TS3AudioBot.Helper.Json;

public class IJsonSerializableConverter : JsonConverter<IJsonSerializable>
{
	public override bool CanConvert(Type objectType) => typeof(IJsonSerializable).IsAssignableFrom(objectType);

	public override IJsonSerializable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

	public override void Write(Utf8JsonWriter writer, IJsonSerializable value, JsonSerializerOptions options)
	{
		if (value is null)
		{
			writer.WriteNullValue();
		}
		else
		{
			value.ToJson(writer, options);
		}
	}
}
