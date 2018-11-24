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

	public class ExecutionInformation
	{
		private readonly IInjector dynamicObjects;

		public ExecutionInformation() : this(new BasicInjector()) { }

		public ExecutionInformation(IInjector injector)
		{
			dynamicObjects = injector;
			AddDynamicObject(this);
		}

		public void AddDynamicObject(object obj) => dynamicObjects.AddModule(obj);

		public bool TryGet<T>(out T obj)
		{
			var ok = TryGet(typeof(T), out var oobj);
			if (ok) obj = (T)oobj;
			else obj = default;
			return ok;
		}
		public bool TryGet(Type t, out object obj)
		{
			obj = dynamicObjects.GetModule(t);
			return obj != null;
		}
	}
}
