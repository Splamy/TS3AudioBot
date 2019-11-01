// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Nett;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TS3AudioBot.Config
{
	[DebuggerDisplay("table:{Key}")]
	public abstract class ConfigTable : ConfigEnumerable
	{
		protected List<ConfigPart> Properties { get; } = new List<ConfigPart>();

		protected ConfigTable()
		{
			GetMember();
			foreach (var configPart in Properties)
				configPart.Parent = this;
		}

		private IEnumerable<System.Reflection.PropertyInfo> GetConfigPartProperties()
		{
			return GetType()
				.GetProperties()
				.Where(x => typeof(ConfigPart).IsAssignableFrom(x.PropertyType));
		}

		private void GetMember()
		{
			Properties.Clear();
			Properties.AddRange(GetConfigPartProperties().Select(x => (ConfigPart)x.GetValue(this)));
		}

		public override void FromToml(TomlObject tomlObject)
		{
			base.FromToml(tomlObject);

			foreach (var part in Properties)
			{
				var child = TomlObject.TryGetValue(part.Key);
				part.FromToml(child);
			}
		}

		public override void Derive(ConfigPart derived)
		{
			foreach (var prop in GetConfigPartProperties())
			{
				var self = (ConfigPart)prop.GetValue(this);
				var other = (ConfigPart)prop.GetValue(derived);
				self.Derive(other);
			}
		}

		public override ConfigPart GetChild(string key) => Properties.FirstOrDefault(x => x.Key == key);

		public override IEnumerable<ConfigPart> GetAllChildren() => Properties;
	}
}
