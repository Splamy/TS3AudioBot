// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TS3AudioBot.Plugins
{
	public interface ITabPlugin : IDisposable
	{
		void Initialize();
	}

	public interface ICorePlugin : ITabPlugin { }

	public interface IBotPlugin : ITabPlugin { }

	public interface IPluginMeta
	{
		string Name { get; }
		string Description { get; }
		string Author { get; }
		Uri ProjectUrl { get; }
		Version Version { get; }
	}

	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	[Obsolete("Static Plugins are deprecated, use an ICorePlugin instead")]
	public sealed class StaticPluginAttribute : Attribute { }
}
