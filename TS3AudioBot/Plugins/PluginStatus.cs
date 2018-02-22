// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Plugins
{
	public enum PluginStatus
	{
		/// <summary>The plugin has just been found and is ready to be prepared.</summary>
		Off,
		/// <summary>The plugin is valid and ready to be loaded.</summary>
		Ready,
		/// <summary>The plugin is currently active.</summary>
		Active,
		/// <summary>The plugin has been plugged off intentionally and will not be prepared with the next scan.</summary>
		Disabled,
		/// <summary>The plugin failed to load.</summary>
		Error,
		/// <summary>The plugin needs to be checked/loaded withing a Bot context.</summary>
		NotAvailable,
	}
}
