namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;

	public interface IEnumerableCommand
	{
		int Count { get; }

		ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes);
	}
}
