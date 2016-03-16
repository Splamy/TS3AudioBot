namespace TS3AudioBot.CommandSystem
{
	using System;

	public abstract class ICommandResult : MarshalByRefObject
	{
		public abstract CommandResultType ResultType { get; }

		public override string ToString()
		{
			if (ResultType == CommandResultType.String)
				return ((StringCommandResult)this).Content;
			if (ResultType == CommandResultType.Empty)
				return string.Empty;
			return "CommandResult can't be converted into a string";
		}
	}
}
