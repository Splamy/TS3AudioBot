// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace TS3AudioBot.Dependency
{
	public static class InjectorExtensions
	{
		public static T? GetModule<T>(this IInjector injector) where T : class
		{
			return (T?)injector.GetModule(typeof(T));
		}

		public static T GetModuleOrThrow<T>(this IInjector injector) where T : class
		{
			var t = (T?)injector.GetModule(typeof(T));
			if (t is null) throw new Exception($"{typeof(T).Name} was not found");
			return t;
		}

		public static bool TryGet<T>(this IInjector injector, [NotNullWhen(true)] out T? obj) where T : class
		{
			obj = injector.GetModule<T>();
			return obj != null;
		}

		public static bool TryGet(this IInjector injector, Type t, [NotNullWhen(true)] out object? obj)
		{
			obj = injector.GetModule(t);
			return obj != null;
		}

		public static void AddModule<T>(this IInjector injector, T obj) where T : notnull
		{
			injector.AddModule(typeof(T), obj);
		}

		public static bool TryCreate<T>(this IInjector injector, [NotNullWhen(true)] out T? obj) where T : class
		{
			if (injector.TryCreate(typeof(T), out var oobj))
			{
				obj = (T)oobj;
				return true;
			}
			else
			{
				obj = default;
				return false;
			}
		}

		public static bool TryCreate(this IInjector injector, Type type, [NotNullWhen(true)] out object? obj)
		{
			var param = DependencyBuilder.GetContructorParam(type);
			if (param == null)
				throw new ArgumentException("Invalid type, no constructors");

			var call = new object[param.Length];
			for (int i = 0; i < param.Length; i++)
			{
				if (!injector.TryGet(param[i], out var dep))
				{
					obj = default;
					return false;
				}
				call[i] = dep;
			}
			obj = Activator.CreateInstance(type, call) ?? throw new ArgumentException("Activator didn't do his job...");
			return true;
		}

		public static void FillProperties(this IInjector injector, object obj)
		{
			var type = obj.GetType();
			var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Where(p => p.CanRead && p.CanWrite);
			foreach (var prop in props)
			{
				if (injector.TryGet(prop.PropertyType, out var dep))
					prop.SetValue(obj, dep);
			}
		}
	}
}
