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

	public sealed class CoreInjector : DependencyRealm { }
	public sealed class BotInjector : DependencyRealm { }

	public class DependencyRealm
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly HashSet<Type> registeredTypes;
		private readonly List<Module> modules;

		public DependencyRealm()
		{
			Util.Init(out registeredTypes);
			Util.Init(out modules);
		}

		public void RegisterType<TModule>() => RegisterType(typeof(TModule));
		private void RegisterType(Type modType)
		{
			if (registeredTypes.Contains(modType))
				return;
			registeredTypes.Add(modType);
		}

		public void RegisterModule<TMod>(TMod module, Action<TMod> onInit = null) where TMod : class
		{
			var onInitObject = onInit != null ? new Action<object>(x => onInit((TMod)x)) : null;
			var mod = new Module(module, onInitObject);
			modules.Add(mod);
			DoQueueInitialize(false);
		}

		// Maybe in future update child realm when parent gets updated
		public T CloneRealm<T>() where T : DependencyRealm, new()
		{
			var child = new T();
			child.modules.AddRange(modules);
			child.registeredTypes.UnionWith(registeredTypes);
			return child;
		}

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

		private IEnumerable<Type> GetUnresolvedResolvable(Module module)
			=> module.GetDependants(registeredTypes).Where(dep => FindInjectableModule(dep, module.Status, false) == null);

		private Module FindInjectableModule(Type type, InitState state, bool force)
			=> modules.FirstOrDefault(
				x => (x.Status == InitState.Done || x.Status == InitState.SetOnly && state == InitState.SetOnly || force) && type.IsAssignableFrom(x.Type));

		private bool TryResolve(Module module, bool force)
		{
			var props = module.GetModuleProperties(registeredTypes);
			foreach (var prop in props)
			{
				var depModule = FindInjectableModule(prop.PropertyType, module.Status, force);
				if (depModule != null)
				{
					prop.SetValue(module.Obj, depModule.Obj);
				}
				else
				{
					Log.ConditionalTrace("Module {0} waiting for {1}", module, string.Join(", ", GetUnresolvedResolvable(module).Select(x => x.Name)));
					return false;
				}
			}
			module.SetInitalized();
			Log.ConditionalTrace("Module {0} added", module);
			return true;
		}

		public R<TModule> GetModule<TModule>() where TModule : class
		{
			var mod = FindInjectableModule(typeof(TModule), InitState.Done, false);
			if (mod != null)
				return (TModule)mod.Obj;
			return "Module not found";
		}

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
