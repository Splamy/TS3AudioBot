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
