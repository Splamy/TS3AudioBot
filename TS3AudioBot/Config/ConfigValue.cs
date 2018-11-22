// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Config
{
	using Nett;
	using Newtonsoft.Json;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using Helper;

	[DebuggerDisplay("{Key}:{Value}")]
	public class ConfigValue<T> : ConfigPart
	{
		public override bool ExpectsString => typeof(T) == typeof(string) || typeof(T) == typeof(TimeSpan) || typeof(T).IsEnum;
		private ConfigValue<T> backingValue;
		private bool hasValue = false;
		public T Default { get; }
		private T value;
		public T Value
		{
			get
			{
				if (hasValue)
					return value;
				if (backingValue != null)
					return backingValue.Value;
				return Default;
			}
			set
			{
				hasValue = true;
				if (EqualityComparer<T>.Default.Equals(this.value, value))
					return;
				this.value = value;
				if (Changed != null)
				{
					var args = new ConfigChangedEventArgs<T>(value);
					Changed?.Invoke(this, args);
				}
			}
		}

		public event EventHandler<ConfigChangedEventArgs<T>> Changed;

		public ConfigValue(string key, T defaultVal, string doc = "") : base(key)
		{
			Documentation = doc;
			Default = defaultVal;
		}

		private void InvokeChange(object sender, ConfigChangedEventArgs<T> args) => Changed?.Invoke(sender, args);

		public override void FromToml(TomlObject tomlObject)
		{
			if (tomlObject != null)
			{
				if (tomlObject.TryGetValue<T>(out var tomlValue))
					Value = tomlValue;
				else
					Log.Warn("Failed to read '{0}', got {1} with {2}", Key, tomlObject.ReadableTypeName, tomlObject.DumpToJson());
			}
		}

		public override void ToToml(bool writeDefaults, bool writeDocumentation)
		{
			// Keys with underscore are read-only
			if (Key.StartsWith("_"))
				return;

			// Set field if either
			// - this value is set
			// - or we explicitely want to write out default values
			var selfToml = Parent.TomlObject.TryGetValue(Key);
			if (hasValue || (writeDefaults && selfToml is null)) // TODO optimize: check if existing value is same as Own.Value
			{
				selfToml = Parent.TomlObject.Set(Key, Value);
			}
			if (writeDocumentation && selfToml != null)
			{
				CreateDocumentation(selfToml);
			}
		}

		public override void Derive(ConfigPart derived)
		{
			if (derived is ConfigValue<T> derivedValue)
			{
				derivedValue.backingValue = this;
				Changed -= derivedValue.InvokeChange;
				Changed += derivedValue.InvokeChange;
			}
		}

		public override void ToJson(JsonWriter writer)
		{
			writer.WriteValue(Value);
		}

		public override E<string> FromJson(JsonReader reader)
		{
			try
			{
				var err = reader.TryReadValue<T>(out var tomlValue);
				if (err.Ok)
				{
					Value = tomlValue;
					return R.Ok;
				}
				return err;
			}
			catch (JsonReaderException ex) { return $"Could not read value: {ex.Message}"; }
		}

		public override string ToString() => Value.ToString();

		public static implicit operator T(ConfigValue<T> conf) => conf.Value;
	}

	public class ConfigChangedEventArgs<T> : EventArgs
	{
		public T NewValue { get; }

		public ConfigChangedEventArgs(T newValue)
		{
			NewValue = newValue;
		}
	}
}
