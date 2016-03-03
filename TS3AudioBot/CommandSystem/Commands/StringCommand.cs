namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;

	public class StringCommand : ICommand
	{
		readonly string content;

		public StringCommand(string contentArg)
		{
			content = contentArg;
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return new StringCommandResult(content);
		}
	}
}
