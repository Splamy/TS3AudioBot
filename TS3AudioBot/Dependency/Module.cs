// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Linq;

namespace TS3AudioBot.Dependency
{
	internal class Module
	{
		public Type TImplementation { get; }
		public Type TService { get; }
		public Type[] ConstructorParam { get; }

		public Module(Type tService, Type tImplementation)
		{
			TService = tService;
			TImplementation = tImplementation;
			ConstructorParam = DependencyBuilder.GetContructorParam(TImplementation) ?? throw new ArgumentException("Invalid type");
		}

		public override string ToString() => $"{TService.Name}({(TService != TImplementation ? TImplementation.Name : "-")}) => {string.Join(",", ConstructorParam.Select(x => x.Name))}";
	}
}
