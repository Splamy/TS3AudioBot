// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.CommandSystem
{
	using System;

	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public sealed class CommandAttribute : Attribute
	{
		public CommandAttribute(CommandRights requiredRights, string commandNameSpace)
			: this(requiredRights, commandNameSpace, null)
		{ }

		public CommandAttribute(CommandRights requiredRights, string commandNameSpace, string help)
		{
			CommandNameSpace = commandNameSpace;
			CommandHelp = help;
			RequiredRights = requiredRights;
		}

		public string CommandNameSpace { get; }
		public string CommandHelp { get; }
		public CommandRights RequiredRights { get; }
	}

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

	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
	public sealed class RequiredParametersAttribute : Attribute
	{
		public RequiredParametersAttribute(int amount)
		{
			Count = amount;
		}
		public int Count { get; }
	}
}
