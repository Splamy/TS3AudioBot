// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Nett;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace TS3AudioBot.Config
{
	public abstract class ConfigEnumerable : ConfigPart
	{
		private static readonly object EmptyObject = new object();

		protected virtual TomlTable.TableTypes TableType { get => TomlTable.TableTypes.Default; }
		public TomlTable TomlObject { get; set; }
		public override bool ExpectsString => false;

		public override void ClearEvents()
		{
			foreach (var child in GetAllChildren())
				child.ClearEvents();
		}

		public override void FromToml(TomlObject tomlObject)
		{
			if (tomlObject is null)
			{
				if (Parent is null)
					TomlObject = Toml.Create();
				else
					TomlObject = Parent.TomlObject.Add(Key, EmptyObject, TableType).Added;
			}
			else
			{
				if (tomlObject is TomlTable tomlTable)
					TomlObject = tomlTable;
				else
					throw new InvalidCastException();
			}
		}

		public override void ToToml(bool writeDefaults, bool writeDocumentation)
		{
			if (writeDocumentation)
				CreateDocumentation(TomlObject);
			foreach (var part in GetAllChildren())
			{
				part.ToToml(writeDefaults, writeDocumentation);
			}
		}

		public override void ToJson(JsonWriter writer)
		{
			writer.WriteStartObject();
			foreach (var item in GetAllChildren())
			{
				writer.WritePropertyName(item.Key);
				item.ToJson(writer);
			}
			writer.WriteEndObject();
		}

		public override E<string> FromJson(JsonReader reader)
		{
			try
			{
				if (!reader.Read() || (reader.TokenType != JsonToken.StartObject))
					return $"Wrong type, expected start of object but found {reader.TokenType}";

				while (reader.Read()
					&& (reader.TokenType == JsonToken.PropertyName))
				{
					var childName = (string)reader.Value;
					var child = GetChild(childName);
					if (child is null)
					{
						if (this is IDynamicTable dynTable)
							child = dynTable.GetOrCreateChild(childName);
						else
							return "No child found";
					}

					child.FromJson(reader);
				}

				if (reader.TokenType != JsonToken.EndObject)
					return $"Expected end of array but found {reader.TokenType}";

				return R.Ok;
			}
			catch (JsonReaderException ex) { return $"Could not read value: {ex.Message}"; }
		}

		// Virtual table methods

		public abstract ConfigPart GetChild(string key);

		public abstract IEnumerable<ConfigPart> GetAllChildren();

		// Static factory methods

		protected static T Create<T>(string key, string doc = "") where T : ConfigEnumerable, new()
		{
			return new T
			{
				Key = key,
				Documentation = doc,
			};
		}

		protected static T Create<T>(string key, ConfigEnumerable parent, TomlObject fromToml, string doc = "") where T : ConfigEnumerable, new()
		{
			return Init(Create<T>(key, doc), parent, fromToml);
		}

		protected static T Init<T>(T part, ConfigEnumerable parent, TomlObject fromToml) where T : ConfigPart
		{
			part.Parent = parent;
			part.FromToml(fromToml);
			return part;
		}

		public static T CreateRoot<T>() where T : ConfigEnumerable, new() => Create<T>(null, null, null, "");

		public static R<T, Exception> Load<T>(string path) where T : ConfigEnumerable, new()
		{
			TomlTable rootToml;
			try { rootToml = Toml.ReadFile(path); }
			catch (Exception ex) { return ex; }
			return Create<T>(null, null, rootToml);
		}

		public E<Exception> Save(string path, bool writeDefaults, bool writeDocumentation = true)
		{
			try
			{
				lock (this)
				{
					ToToml(writeDefaults, writeDocumentation);
					Toml.WriteFile(TomlObject, path);
				}
			}
			catch (Exception ex) { return ex; }
			return R.Ok;
		}
	}
}
