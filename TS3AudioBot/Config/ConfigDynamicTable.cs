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
	using Helper;
	using Nett;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;

	[DebuggerDisplay("dyntable:{Key}")]
	public class ConfigDynamicTable<T> : ConfigEnumerable, IDynamicTable where T : ConfigEnumerable, new()
	{
		private readonly Dictionary<string, T> dynamicTables;

		public ConfigDynamicTable()
		{
			Util.Init(out dynamicTables);
		}

		public override void FromToml(TomlObject tomlObject)
		{
			base.FromToml(tomlObject);

			dynamicTables.Clear();

			if (tomlObject != null)
			{
				if (!(tomlObject is TomlTable tomlTable))
					throw new InvalidCastException();

				foreach (var child in tomlTable.Rows)
				{
					var childConfig = Create<T>(child.Key, this, child.Value);
					dynamicTables.Add(child.Key, childConfig);
				}
			}
		}

		public override IEnumerable<ConfigPart> GetAllChildren() => GetAllItems();

		public override ConfigPart GetChild(string key) => GetItem(key);

		public ConfigPart GetOrCreateChild(string key) => GetOrCreateItem(key);

		public override void Derive(ConfigPart derived)
		{
			// TODO
		}

		public T GetItem(string key) => dynamicTables.TryGetValue(key, out var item) ? item : null;

		public IEnumerable<T> GetAllItems() => dynamicTables.Values;

		public T CreateItem(string key)
		{
			var childConfig = Create<T>(key, this, null);
			dynamicTables.Add(key, childConfig);
			return childConfig;
		}

		public T GetOrCreateItem(string key) => GetItem(key) ?? CreateItem(key);
	}

	public interface IDynamicTable
	{
		ConfigPart GetOrCreateChild(string key);
	}
}
