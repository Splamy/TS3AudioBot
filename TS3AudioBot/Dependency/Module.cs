// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Dependency
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text;

	internal class Module
	{
		public InitState Status { get; private set; }
		public object Obj { get; }
		public Type Type => Obj.GetType();
		public Action<object> Initializer { get; }
		// object SyncContext;

		public Module(object obj, Action<object> initializer)
		{
			Status = initializer == null ? InitState.SetOnly : InitState.SetAndInit;
			Initializer = initializer;
			Obj = obj;
		}

		public IEnumerable<Type> GetDependants(HashSet<Type> deps)
		{
			var type = Obj.GetType();
			return GetModuleProperties(deps, type).Select(p => p.PropertyType);
		}

		public IEnumerable<PropertyInfo> GetModuleProperties(HashSet<Type> deps) => GetModuleProperties(deps, Obj.GetType());

		private static IEnumerable<PropertyInfo> GetModuleProperties(HashSet<Type> deps, IReflect type) =>
			type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Where(p => p.CanRead && p.CanWrite && deps.Any(x => x.IsAssignableFrom(p.PropertyType)));

		public void SetInitalized()
		{
			if (Status == InitState.SetAndInit)
				Initializer?.Invoke(Obj);
			Status = InitState.Done;
		}

		public override string ToString()
		{
			var strb = new StringBuilder();
			strb.Append(Type.Name);
			switch (Status)
			{
				case InitState.Done: strb.Append("+"); break;
				case InitState.SetOnly: strb.Append("*"); break;
				case InitState.SetAndInit: strb.Append("-"); break;
				default: throw new ArgumentOutOfRangeException();
			}
			return strb.ToString();
		}
	}

	internal enum InitState
	{
		Done,
		SetOnly,
		SetAndInit,
	}
}
