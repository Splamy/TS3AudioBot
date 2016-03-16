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

		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return new StringCommandResult(content);
		}
	}
}
