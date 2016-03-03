namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;

	public class AppliedCommand : ICommand
	{
		readonly ICommand internCommand;
		readonly IEnumerableCommand internArguments;

		public AppliedCommand(ICommand command, IEnumerableCommand arguments)
		{
			internCommand = command;
			internArguments = arguments;
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return internCommand.Execute(info, new EnumerableCommandMerge(new IEnumerableCommand[] { internArguments, arguments }), returnTypes);
		}
	}

}
