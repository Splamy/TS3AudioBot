// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace TS3AudioBot.Localization
{
	internal class DynamicResourceManager : ResourceManager
	{
		private readonly Dictionary<string, ResourceSet> dynamicResourceSets = new Dictionary<string, ResourceSet>();

		public DynamicResourceManager(string baseName, Assembly assembly) : base(baseName, assembly)
		{

		}

		public void SetResourceSet(CultureInfo culture, ResourceSet set)
		{
			dynamicResourceSets[culture.Name] = set;
		}

		public override ResourceSet GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
		{
			if (culture is null)
			{
				culture = Thread.CurrentThread.CurrentUICulture;
			}

			if (dynamicResourceSets.TryGetValue(culture.Name, out var set))
			{
				return set;
			}

			return base.GetResourceSet(culture, createIfNotExists, tryParents);
		}

		public override string GetString(string name, CultureInfo culture)
		{
			if (culture is null)
			{
				culture = Thread.CurrentThread.CurrentUICulture;
			}

			string str;
			if (dynamicResourceSets.TryGetValue(culture.Name, out var set))
			{
				if ((str = set.GetString(name)) != null)
					return str;
			}

			if ((str = base.GetString(name, culture)) != null)
				return str;

			if ((str = base.GetString(name, CultureInfo.InvariantCulture)) != null)
				return str;

			//$"The localized entry {name} was not found"
			return null;
		}
	}
}
