// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using System;
using TS3AudioBot.CommandSystem;

namespace TS3AudioBot.Web.Api
{
	public class JsonValue<T> : JsonValue where T : notnull
	{
		protected Func<T, string>? AsString { get; }

		new public T Value => (T)base.Value;

		public JsonValue(T value) : base(value) { }
		public JsonValue(T value, string msg) : base(value, msg) { }
		public JsonValue(T value, Func<T, string>? asString) : base(value)
		{
			AsString = asString;
		}

		public override string ToString()
		{
			if (AsStringResult is null)
			{
				if (AsString != null)
					AsStringResult = AsString.Invoke(Value);
				else
					AsStringResult = Value?.ToString() ?? string.Empty;
			}
			return AsStringResult;
		}
	}

	public abstract class JsonValue : JsonObject
	{
		protected string? AsStringResult { get; set; }
		public object Value { get; }

		protected JsonValue(object value) { Value = value; AsStringResult = null; }
		protected JsonValue(object value, string msg) { Value = value; AsStringResult = msg ?? string.Empty; }

		public override object GetSerializeObject() => Value;

		public override string Serialize()
		{
			var seriObj = GetSerializeObject();
			if (seriObj != null && CommandSystemTypes.BasicTypes.Contains(seriObj.GetType()))
				return JsonConvert.SerializeObject(this);
			return base.Serialize();
		}

		public override string ToString()
		{
			if (AsStringResult is null)
				AsStringResult = Value?.ToString() ?? string.Empty;
			return AsStringResult;
		}

		// static creator methods for anonymous stuff

		public static JsonValue<T> Create<T>(T anon) where T : notnull => new JsonValue<T>(anon);
		public static JsonValue<T> Create<T>(T anon, string msg) where T : notnull => new JsonValue<T>(anon, msg);
		public static JsonValue<T> Create<T>(T anon, Func<T, string>? asString) where T : notnull => new JsonValue<T>(anon, asString);
	}
}
