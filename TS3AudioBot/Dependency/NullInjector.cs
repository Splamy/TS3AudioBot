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
	public class NullInjector : IInjector
	{
		public static readonly IInjector Instance = new NullInjector();
		private NullInjector() { }
		public object GetModule(Type type) => null;
		public void AddModule(Type type, object obj) { }
	}
}
