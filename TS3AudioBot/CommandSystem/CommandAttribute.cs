// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TS3AudioBot.CommandSystem
{
	/// <summary>
	/// Marks a method as callable from the CommandSystem.
	/// The containing class must be registered in the CommandSystem to use this method.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public sealed class CommandAttribute : Attribute
	{
		public CommandAttribute(string commandNameSpace, string overrideHelpName = null)
		{
			CommandNameSpace = commandNameSpace;
			OverrideHelpName = overrideHelpName;
		}

		public string CommandNameSpace { get; }
		public string OverrideHelpName { get; }
	}

	/// <summary>
	/// Gives an example on how to use this method.
	/// Can be used to clarify different functionality from various overloads.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
	public sealed class UsageAttribute : Attribute
	{
		public UsageAttribute(string syntax, string help)
		{
			UsageSyntax = syntax;
			UsageHelp = help;
		}

		public string UsageSyntax { get; }
		public string UsageHelp { get; }
	}
}
