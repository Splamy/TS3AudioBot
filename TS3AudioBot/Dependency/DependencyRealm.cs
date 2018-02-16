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
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;

	public sealed class CoreInjector : DependencyRealm { }
	public sealed class BotInjector : DependencyRealm { }

	public class DependencyRealm
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly List<Module> modules;
		private readonly HashSet<Type> registeredTypes;

		public DependencyRealm()
		{
			Util.Init(out registeredTypes);
			Util.Init(out modules);
		}

		// TODO doc
		public void RegisterType<TModule>() => RegisterType(typeof(TModule));

		private void RegisterType(Type modType)
		{
			if (registeredTypes.Contains(modType))
				return;
			registeredTypes.Add(modType);
		}

		// TODO doc
		public void RegisterModule<TMod>(TMod module, Action<TMod> onInit = null) where TMod : class
		{
			var onInitObject = onInit != null ? new Action<object>(x => onInit((TMod)x)) : null;
			var mod = new Module(module, onInitObject);
			modules.Add(mod);
			DoQueueInitialize(false);
		}

		// TODO doc
		public bool TryInject(object obj) => TryResolve(obj, InitState.SetOnly, false);

		// Maybe in future update child realm when parent gets updated
		public T CloneRealm<T>() where T : DependencyRealm, new()
		{
			var child = new T();
			child.modules.AddRange(modules);
			child.registeredTypes.UnionWith(registeredTypes);
			return child;
		}

		// TODO doc
		public void ForceCyclicResolve()
		{
			DoQueueInitialize(true);
		}

		private void DoQueueInitialize(bool force)
		{
			bool changed;
			do
			{
				changed = false;
				foreach (var mod in modules)
				{
					if (mod.Status == InitState.Done)
						continue;

					if (!TryResolve(mod, force))
						continue;
					changed = true;
				}
			} while (changed);
		}

		private IEnumerable<Type> GetDependants(Module mod)
		{
			return GetModuleProperties(mod.Type).Select(p => p.PropertyType);
		}

		private IEnumerable<PropertyInfo> GetModuleProperties(IReflect type) =>
			type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Where(p => p.CanRead && p.CanWrite && registeredTypes.Any(x => x.IsAssignableFrom(p.PropertyType)));

		private IEnumerable<Type> GetUnresolvedResolvable(Module module)
			=> GetDependants(module).Where(dep => FindInjectableModule(dep, module.Status, false) == null);

		private Module FindInjectableModule(Type type, InitState state, bool force)
			=> modules.FirstOrDefault(
				x => (x.Status == InitState.Done || x.Status == InitState.SetOnly && state == InitState.SetOnly || force) &&
					 type.IsAssignableFrom(x.Type));

		private bool TryResolve(Module module, bool force)
		{
			var result = TryResolve(module.Obj, module.Status, force);
			if (result)
			{
				module.SetInitalized();
				Log.ConditionalTrace("Module {0} added", module);
			}
			else
			{
				Log.ConditionalTrace("Module {0} waiting for {1}", module,
					string.Join(", ", GetUnresolvedResolvable(module).Select(x => x.Name)));
			}

			return result;
		}

		private bool TryResolve(object obj, InitState state, bool force)
		{
			var props = GetModuleProperties(obj.GetType());
			foreach (var prop in props)
			{
				var depModule = FindInjectableModule(prop.PropertyType, state, force);
				if (depModule != null)
				{
					prop.SetValue(obj, depModule.Obj);
				}
				else
				{
					return false;
				}
			}

			return true;
		}

		// TODO doc
		public R<TModule> GetModule<TModule>() where TModule : class
		{
			var mod = FindInjectableModule(typeof(TModule), InitState.Done, false);
			if (mod != null)
				return (TModule)mod.Obj;
			return "Module not found";
		}

		// TODO doc
		public bool AllResolved() => modules.All(x => x.Status == InitState.Done);

		public void Unregister(Type type)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			int done = modules.Count(x => x.Status == InitState.Done);
			int set = modules.Count(x => x.Status == InitState.SetOnly);
			int setinit = modules.Count(x => x.Status == InitState.SetAndInit);
			return $"Done: {done} Set: {set} SetInit: {setinit}";
		}
	}
}
