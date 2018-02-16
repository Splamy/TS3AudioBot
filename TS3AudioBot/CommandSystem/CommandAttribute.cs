// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem
{
	using System;

	/// <summary>
	/// Marks a method as callable from the CommandSystem.
	/// The containing class must be registered in the CommandSystem to use this method.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public sealed class CommandAttribute : Attribute
	{
		public CommandAttribute(string commandNameSpace, string help = null)
		{
			CommandNameSpace = commandNameSpace;
			CommandHelp = help;
		}

		public string CommandNameSpace { get; }
		public string CommandHelp { get; }
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

	/// <summary>
	/// Denotes how many parameter are required for this method.
	/// Use this attribute to allow optional parameters.
	/// Special parameters like the <see cref="ExecutionInformation"/> context do not count towards this amount.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public sealed class RequiredParametersAttribute : Attribute
	{
		public RequiredParametersAttribute(int amount)
		{
			Count = amount;
		}
		public int Count { get; }
	}
}
