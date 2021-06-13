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
using System.Xml;
using TSLib.Helper;

namespace TS3AudioBot.Helper.Json
{
	internal class TimeSpanConverter : JsonConverter<TimeSpan>
	{
		private readonly TimeSpanFormatting format;

		public TimeSpanConverter(TimeSpanFormatting format)
		{
			this.format = format;
		}

		public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
		{
			switch (format)
			{
			case TimeSpanFormatting.Simple:
				writer.WriteStringValue(TextUtil.FormatTimeAsSimple(value));
				break;
			case TimeSpanFormatting.Seconds:
				writer.WriteNumberValue(value.TotalSeconds);
				break;
			case TimeSpanFormatting.Xml:
				writer.WriteStringValue(XmlConvert.ToString(value));
				break;
			case var _unhandled:
				throw Tools.UnhandledDefault(_unhandled);
			}
		}

		public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
			case JsonTokenType.String:
			case JsonTokenType.Null:
				var str = reader.GetString() ?? throw new JsonException("TimeSpan value is empty");
				return TextUtil.ParseTime(str) ?? throw new JsonException("Invalid TimeSpan");
			case JsonTokenType.Number:
				var secs = reader.GetDouble();
				return TimeSpan.FromSeconds(secs);
			default:
				throw new JsonException($"Invalid token type '{reader.TokenType}' for TimeSpan");
			}
		}
	}

	enum TimeSpanFormatting
	{
		Simple,
		Seconds,
		Xml,
	}
}
