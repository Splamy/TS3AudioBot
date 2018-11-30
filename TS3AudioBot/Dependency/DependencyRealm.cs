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

	public class DependencyRealm : IInjector
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly HashSet<Type> registeredTypes;
		private readonly List<Module> modules;

		public DependencyRealm()
		{
			Util.Init(out registeredTypes);
			Util.Init(out modules);
		}

		/// <summary>Will add the type to pool of registered types which can be injected into other dependencies.</summary>
		/// <typeparam name="TModule">The type to add</typeparam>
		public void RegisterType<TModule>() => RegisterType(typeof(TModule));

		private void RegisterType(Type modType)
		{
			if (registeredTypes.Contains(modType))
				return;
			registeredTypes.Add(modType);
		}

		/// <summary>Adds an object as a new module to the module pool.
		/// The realm will inject all registered types into public propeties as soon as available.</summary>
		/// <typeparam name="TMod">The type of the module to add</typeparam>
		/// <param name="module">The object to add as a new module.</param>
		/// <param name="onInit">An initialize method that gets called when all dependencies are inected into this module.
		/// Note that declaring this param will force the realm to be more strict with this modul
		/// and make cyclic dependencies harder to resolve.</param>
		public void RegisterModule<TMod>(TMod module, Action<TMod> onInit = null) where TMod : class
		{
			var onInitObject = onInit != null ? new Action<object>(x => onInit((TMod)x)) : null;
			RegisterModule(module, onInitObject);
		}
		private void RegisterModule(object module, Action<object> onInit = null)
		{
			var mod = new Module(module, onInit);
			modules.Add(mod);
			DoQueueInitialize(false);
		}

		/// <summary>Injects all dependencies into the passe object without registering it as a new module.</summary>
		/// <param name="obj">The object to fill.</param>
		/// <returns>True if all registered types were available and could be injected, false otherwise.</returns>
		public bool TryInject(object obj) => TryResolve(obj, InitState.SetOnly, false);

		// Maybe in future update child realm when parent gets updated
		public T CloneRealm<T>() where T : DependencyRealm, new()
		{
			var child = new T();
			child.modules.AddRange(modules);
			child.registeredTypes.UnionWith(registeredTypes);
			return child;
		}

		/// <summary>Tries to initialize all modules while allowing undefined behaviour when resolving cyclic dependecies.</summary>
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
				var modulesIterCache = modules.ToArray();
				foreach (var mod in modulesIterCache)
				{
					if (mod.Status == InitState.Done || mod.Status == InitState.Initializing)
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
			=> GetDependants(module).Where(dep => FindInjectableModule(dep, module.Status) is null);

		private Module FindInjectableModule(Type type, InitState? state)
			=> modules.FirstOrDefault(
				x => (x.Status == InitState.Done || !state.HasValue || x.Status == InitState.SetOnly && state.Value == InitState.SetOnly) &&
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
				var depModule = FindInjectableModule(prop.PropertyType, force ? (InitState?)null : state);
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

		/// <summary>Gets a module assignable to the requested type.</summary>
		/// <typeparam name="TModule">The type to get.</typeparam>
		/// <returns>The object if found, null otherwiese.</returns>
		public TModule GetModule<TModule>() where TModule : class => (TModule)GetModule(typeof(TModule));
		public object GetModule(Type type) => FindInjectableModule(type, InitState.Done)?.Obj;

		void IInjector.AddModule(object obj) => RegisterModule(obj);

		public IEnumerable<object> GetAllModules() => modules.Select(x => x.Obj);

		/// <summary>Checks if all module could get initialized.</summary>
		/// <returns>True if all are initialized, false otherwise.</returns>
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

	public interface IInjector
	{
		object GetModule(Type type);
		void AddModule(object obj);
	}
}
