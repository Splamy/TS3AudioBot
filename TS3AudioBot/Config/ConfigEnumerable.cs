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

	public abstract class ConfigEnumerable : ConfigPart
	{
		protected static readonly object emptyObject = new object();

		protected virtual TomlTable.TableTypes TableType { get => TomlTable.TableTypes.Default; }
		public TomlTable TomlObject { get; set; }

		public override void FromToml(TomlObject tomlObject)
		{
			if (tomlObject == null)
			{
				if (Parent == null)
					TomlObject = Toml.Create();
				else
					TomlObject = Parent.TomlObject.Add(Key, emptyObject, TableType);
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
			var table = Create<T>(key, doc);
			table.Parent = parent;
			table.FromToml(fromToml);
			return table;
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
			ToToml(writeDefaults, writeDocumentation);
			try { Toml.WriteFile(TomlObject, path); }
			catch (Exception ex) { return ex; }
			return R.Ok;
		}
	}
}
