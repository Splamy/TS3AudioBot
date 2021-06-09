// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Text.Encodings.Web;
using System.Text.Json;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.Helper.Json;

namespace TS3AudioBot.Web.Api
{
	public abstract class JsonObject : IWrappedResult
	{
		protected static readonly JsonSerializerOptions DefaultJsonOptions = new()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		};

		static JsonObject()
		{
			DefaultJsonOptions.Converters.Add(new IJsonSerializableConverter());
			DefaultJsonOptions.Converters.Add(new TimeSpanConverter(TimeSpanFormatting.Seconds));
		}

		protected JsonObject() { }

		object? IWrappedResult.Content => GetSerializeObject();
		public virtual object GetSerializeObject() => this;
		public virtual string Serialize() => JsonSerializer.Serialize(GetSerializeObject(), DefaultJsonOptions);
		public override abstract string ToString();
	}
}
