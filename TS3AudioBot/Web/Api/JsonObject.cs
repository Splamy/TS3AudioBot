// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using TS3AudioBot.Helper;

namespace TS3AudioBot.Web.Api
{
	public abstract class JsonObject
	{
		private static readonly JsonSerializerSettings DefaultSettigs = new JsonSerializerSettings();

		protected string AsStringResult { get; set; }

		static JsonObject()
		{
			DefaultSettigs.Converters.Add(new IJsonSerializableConverter());
			DefaultSettigs.Converters.Add(new TimeSpanConverter());
		}

		protected JsonObject(string stringResult)
		{
			AsStringResult = stringResult;
		}

		public override string ToString() => AsStringResult;

		public virtual object GetSerializeObject() => this;
		public virtual string Serialize() => JsonConvert.SerializeObject(GetSerializeObject(), DefaultSettigs);
	}
}
