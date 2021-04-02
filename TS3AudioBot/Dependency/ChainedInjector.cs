// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TS3AudioBot.Dependency
{
	public class ChainedInjector<T> : IInjector where T : class, IInjector
	{
		public IInjector ParentInjector { get; set; }
		public T OwnInjector { get; protected set; }

		public ChainedInjector(IInjector parent, T own)
		{
			ParentInjector = parent ?? throw new ArgumentNullException(nameof(parent));
			OwnInjector = own ?? throw new ArgumentNullException(nameof(parent));
		}

		public virtual object? GetModule(Type type)
			=> OwnInjector.GetModule(type) ?? ParentInjector.GetModule(type);

		public virtual void AddModule(Type type, object obj)
			=> OwnInjector.AddModule(type, obj);
	}
}
