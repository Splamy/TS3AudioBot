namespace TS3AudioBot.CommandSystem
{
	public class CommandCommandResult : ICommandResult
	{
		readonly ICommand command;

		public override CommandResultType ResultType => CommandResultType.Command;

		public virtual ICommand Command => command;

		public CommandCommandResult(ICommand commandArg)
		{
			command = commandArg;
		}
	}
}
