// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Web.Api
{
	using Newtonsoft.Json;
	using CommandSystem;

	public class JsonValue<T> : JsonValueBase
	{
		public new T Value => (T)base.Value;

		public JsonValue(T value) : base(value, value?.ToString() ?? string.Empty) { }
		public JsonValue(T value, string msg) : base(value, msg) { }
	}

	public class JsonValueBase : JsonObject
	{
		protected object Value { get; }

		public JsonValueBase(object value) : this(value, value?.ToString() ?? string.Empty) { }
		public JsonValueBase(object value, string msg) : base(msg) { Value = value; }

		public override object GetSerializeObject() => Value;

		public override string Serialize()
		{
			var seriObj = GetSerializeObject();
			if (seriObj != null && XCommandSystem.BasicTypes.Contains(seriObj.GetType()))
				return JsonConvert.SerializeObject(this);
			return base.Serialize();
		}
	}
}
