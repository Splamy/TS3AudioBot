namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Linq;

	public class StaticEnumerableCommand : IEnumerableCommand
	{
		readonly IEnumerable<ICommand> internArguments;

		public int Count => internArguments.Count();

		public StaticEnumerableCommand(IEnumerable<ICommand> arguments)
		{
			internArguments = arguments;
		}

		public ICommandResult Execute(int index, ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (index < 0 || index >= internArguments.Count())
				throw new CommandException("Requested too many arguments (StaticEnumerableCommand)");
			return internArguments.ElementAt(index).Execute(info, arguments, returnTypes);
		}
	}

}
