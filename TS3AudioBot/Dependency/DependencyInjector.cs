using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace TS3AudioBot.Dependency
{
	using Helper;

	public class Injector : DependencyRealm, ICoreModule
	{
		public Injector()
		{
		}

		public void Initialize() { }

		public T GetCoreModule<T>() where T : ICoreModule
		{
			throw new NotImplementedException();
		}
	}

	public class Module
	{
		private static readonly ConcurrentDictionary<Type, Type[]> typeData;

		public bool IsInitialized { get; set; }
		public object Obj { get; }
		public Type BaseType { get; }
		// object SyncContext;

		static Module()
		{
			Util.Init(out typeData);
		}

		public Module(object obj, Type baseType)
		{
			IsInitialized = false;
			Obj = obj;
			BaseType = baseType;
		}

		public Type[] GetDependants() => GetDependants(Obj.GetType());

		public IEnumerable<PropertyInfo> GetModuleProperties() => GetModuleProperties(Obj.GetType());

		private static Type[] GetDependants(Type type)
		{
			if (!typeData.TryGetValue(type, out var depArr))
			{
				depArr = GetModuleProperties(type).Select(p => p.PropertyType).ToArray();
				typeData[type] = depArr;
			}
			return depArr;
		}

		private static IEnumerable<PropertyInfo> GetModuleProperties(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
			.Where(p => p.CanRead && p.CanWrite && typeof(ITabModule).IsAssignableFrom(p.PropertyType));
	}

	public class DependencyRealm
	{
		protected ConcurrentDictionary<Type, Module> loaded;
		protected List<Module> waiting;

		public DependencyRealm()
		{
			Util.Init(out loaded);
			Util.Init(out waiting);
		}

		public T Create<T>() where T : ICoreModule => (T)CreateFromType(typeof(T));
		public object CreateFromType(Type type)
		{
			var obj = Activator.CreateInstance(type);
			RegisterInjectable(obj, false);
			return obj;
		}
		public void RegisterModule<T>(T obj, bool initialized = false) where T : ICoreModule => RegisterInjectable(obj, initialized, typeof(T));
		public void RegisterInjectable(object obj, bool initialized = false, Type baseType = null)
		{
			var modType = baseType ?? obj.GetType();
			var mod = new Module(obj, modType) { IsInitialized = initialized };
			if (initialized)
				SetInitalized(mod);
			else
				waiting.Add(mod);
			DoQueueInitialize();
		}

		public void SkipInitialized(object obj)
		{
			var index = waiting.Select((m, i) => new Tuple<Module, int>(m, i)).FirstOrDefault(t => t.Item1.Obj == obj);
			if (index == null)
				return;

			waiting.RemoveAt(index.Item2);
			SetInitalized(index.Item1);

			DoQueueInitialize();
		}

		public void ForceCyclicResolve()
		{

		}

		protected bool SetInitalized(Module module)
		{
			if (!module.IsInitialized && module.Obj is ITabModule tabModule)
			{
				tabModule.Initialize();
			}
			module.IsInitialized = true;
			loaded[module.BaseType] = module;
			return true;
		}

		protected void DoQueueInitialize()
		{
			bool changed;
			do
			{
				changed = false;
				for (int i = 0; i < waiting.Count; i++)
				{
					var mod = waiting[i];
					if (IsResolvable(mod))
					{
						if (!DoResolve(mod))
						{
							// TODO warn
							continue;
						}
						changed = true;
						waiting.RemoveAt(i);
						i--;
					}
				}
			} while (changed);
		}

		protected bool IsResolvable(Module module)
		{
			var deps = module.GetDependants();
			foreach (var depeningType in deps)
			{
				if (!loaded.ContainsKey(depeningType)) // todo maybe to some linear inheritance checking
					return false;
			}
			return true;
		}

		protected bool DoResolve(Module module)
		{
			var props = module.GetModuleProperties();
			foreach (var prop in props)
			{
				if (loaded.TryGetValue(prop.PropertyType, out var depModule))
				{
					prop.SetValue(module.Obj, depModule.Obj);
				}
				else
				{
					return false;
				}
			}
			return SetInitalized(module);
		}

		public bool AllResolved() => waiting.Count == 0;

		public void Unregister(Type type)
		{
			throw new NotImplementedException();
		}
	}
}
