namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Linq;

	public class EnumerableCommandMerge : IEnumerableCommand
	{
		readonly IEnumerable<IEnumerableCommand> internCommands;

		public int Count => internCommands.Select(c => c.Count).Sum();

		public EnumerableCommandMerge(IEnumerable<IEnumerableCommand> commands)
		{
			internCommands = commands;
		}

		public ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (index < 0)
				throw new CommandException("Negative arguments?? (EnumerableCommandMerge)");
			foreach (var c in internCommands)
			{
				if (index < c.Count)
					return c.Execute(index, info, arguments, returnTypes);
				index -= c.Count;
			}
			throw new CommandException("Requested too many arguments (EnumerableCommandMerge)");
		}
	}

}
