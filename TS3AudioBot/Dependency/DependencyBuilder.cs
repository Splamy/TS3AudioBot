// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TS3AudioBot.Dependency
{
	public sealed class CoreInjector : BasicInjector { }
	public sealed class BotInjector : ChainedInjector<BasicInjector>
	{
		public BotInjector(IInjector parent) : base(parent, new BasicInjector())
		{
		}
	}

	public class DependencyBuilder : IInjector
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private readonly IInjector injector;
		private readonly LinkedList<Module> modules = new LinkedList<Module>();

		public DependencyBuilder(IInjector injector)
		{
			this.injector = injector;
		}

		public DependencyBuilder RequestModule<TService, TImplementation>() where TImplementation : class, TService => RequestModule(typeof(TService), typeof(TImplementation));

		private DependencyBuilder RequestModule(Type tService, Type tImplementation)
		{
			Log.Trace("Requested Service {0} with {1}", tService.Name, tImplementation.Name);
			var mod = new Module(tService, tImplementation);

			for (var cur = modules.First; cur != null; cur = cur.Next)
			{
				if (mod.ConstructorParam.Contains(cur.Value.TService) || mod.TImplementation == cur.Value.TService)
				{
					modules.AddBefore(cur, mod);
					return this;
				}
			}
			modules.AddLast(mod);
			return this;
		}

		public DependencyBuilder RequestModule<TService>() where TService : class => RequestModule<TService, TService>();

		// Pass through injector methods
		public object GetModule(Type type) => injector.GetModule(type);
		public void AddModule(Type type, object obj) => injector.AddModule(type, obj);

		/// <summary>Creates all modules.</summary>
		/// <returns>True if all are initialized, false otherwise.</returns>
		public bool Build()
		{
			for (var curNode = modules.Last; curNode != null; curNode = curNode.Previous, modules.RemoveLast())
			{
				var cur = curNode.Value;
				var obj = injector.GetModule(cur.TImplementation);
				if (obj != null)
				{
					injector.AddModule(cur.TService, obj);
				}
				else
				{
					if (!this.TryCreate(cur.TImplementation, out obj))
						return false;
					injector.AddModule(cur.TService, obj);
				}
			}
			return true;
		}

		internal static Type[] GetContructorParam(Type type)
		{
			var fod = type.GetConstructors().FirstOrDefault();
			if (fod == null)
				return null;
			return fod.GetParameters().Select(p => p.ParameterType).ToArray();
		}

		public override string ToString()
		{
			return $"Unresolved: {modules.Count}";
		}
	}
}
