// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem
{
	using Dependency;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public class BasicInjector : IInjector
	{
		private readonly Dictionary<Type, object> dynamicObjects;
		public BasicInjector() { dynamicObjects = new Dictionary<Type, object>(); }
		public object GetModule(Type type) => dynamicObjects.Where(x => x.Key.IsAssignableFrom(type)).Select(x => x.Value).FirstOrDefault();
		public void AddModule(object obj) => dynamicObjects[obj.GetType()] = obj;
		public IEnumerable<object> GetAllModules() => dynamicObjects.Values;
	}
}
