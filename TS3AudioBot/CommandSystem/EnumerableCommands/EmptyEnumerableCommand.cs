namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;

	public class EmptyEnumerableCommand : IEnumerableCommand
	{
		public int Count => 0;

		public ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			throw new CommandException("No arguments given");
		}
	}
}
