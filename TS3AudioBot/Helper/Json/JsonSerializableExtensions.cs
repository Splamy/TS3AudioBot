// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TS3AudioBot.Helper.Json
{
	public static class JsonSerializableExtensions
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			AllowTrailingCommas = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			NumberHandling = JsonNumberHandling.AllowReadingFromString,
			PropertyNameCaseInsensitive = true,
			WriteIndented = true,
		};
		private static readonly JsonReaderOptions JsonReaderOptions = new()
		{
			AllowTrailingCommas = true,
			CommentHandling = JsonCommentHandling.Skip,
		};
		private static readonly JsonWriterOptions JsonWriterOptions = new()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			Indented = true,
		};

		static JsonSerializableExtensions()
		{
			JsonOptions.Converters.Add(new TimeSpanConverter(TimeSpanFormatting.Simple));
		}

		public static E<string> FromJson(this IJsonSerializable jsonConfig, string json)
		{
			if (jsonConfig.ExpectsString)
				json = JsonSerializer.Serialize(json);

			var data = Encoding.UTF8.GetBytes(json);
			var reader = new Utf8JsonReader(data, JsonReaderOptions);
			return jsonConfig.FromJson(ref reader, JsonOptions);
		}

		public static string ToJson(this IJsonSerializable jsonConfig)
		{
			using var mem = new MemoryStream();
			using (var writer = new Utf8JsonWriter(mem, JsonWriterOptions))
			{
				jsonConfig.ToJson(writer, JsonOptions);
			}
			mem.Seek(0, SeekOrigin.Begin);
			return Encoding.UTF8.GetString(mem.ToArray());
		}
	}
}
