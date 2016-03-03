namespace TS3AudioBot.CommandSystem
{
	using System;

	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	sealed class CommandAttribute : Attribute
	{
		public CommandAttribute(string commandNameSpace) { CommandNameSpace = commandNameSpace; }
		public string CommandNameSpace { get; }
	}
}
