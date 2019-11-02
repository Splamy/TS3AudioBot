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
	public class JsonValue<T> : JsonValue
	{
		protected Func<T, string> AsString { get; }

		public new T Value => (T)base.Value;

		public JsonValue(T value) : base(value) { }
		public JsonValue(T value, string msg) : base(value, msg) { }
		public JsonValue(T value, Func<T, string> asString = null) : base(value)
		{
			AsString = asString;
		}

		public override object GetSerializeObject()
		{
			return Value;
		}

		public override string ToString()
		{
			if (AsStringResult is null)
			{
				if (AsString != null)
					AsStringResult = AsString.Invoke(Value);
				else if (Value == null)
					AsStringResult = string.Empty;
				else
					AsStringResult = Value.ToString();
			}
			return AsStringResult;
		}
	}

	public abstract class JsonValue : JsonObject
	{
		protected object Value { get; }

		protected JsonValue(object value) : base(null) { Value = value; }
		protected JsonValue(object value, string msg) : base(msg ?? string.Empty) { Value = value; }

		public override object GetSerializeObject() => Value;

		public override string Serialize()
		{
			var seriObj = GetSerializeObject();
			if (seriObj != null && XCommandSystem.BasicTypes.Contains(seriObj.GetType()))
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

		public static JsonValue<T> Create<T>(T anon) => new JsonValue<T>(anon);
		public static JsonValue<T> Create<T>(T anon, string msg) => new JsonValue<T>(anon, msg);
		public static JsonValue<T> Create<T>(T anon, Func<T, string> asString = null) => new JsonValue<T>(anon, asString);
	}
}
