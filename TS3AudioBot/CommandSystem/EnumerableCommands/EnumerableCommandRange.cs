namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;

	public class EnumerableCommandRange : IEnumerableCommand
	{
		readonly IEnumerableCommand internCommand;
		readonly int start;
		readonly int count;

		public int Count => Math.Min(internCommand.Count - start, count);

		public EnumerableCommandRange(IEnumerableCommand command, int startArg, int countArg = int.MaxValue)
		{
			internCommand = command;
			start = startArg;
			count = countArg;
		}

		public ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (index < 0)
				throw new CommandException("Negative arguments?? (EnumerableCommandRange)");
			return internCommand.Execute(index + start, info, arguments, returnTypes);
		}
	}

}
