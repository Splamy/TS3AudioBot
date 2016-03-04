namespace TS3AudioBot.CommandSystem
{
	using System.Linq;
	using System.Collections.Generic;

	public class AppliedCommand : ICommand
	{
		readonly ICommand internCommand;
		readonly IEnumerable<ICommand> internArguments;

		public AppliedCommand(ICommand command, IEnumerable<ICommand> arguments)
		{
			internCommand = command;
			internArguments = arguments;
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			return internCommand.Execute(info, internArguments.Concat(arguments), returnTypes);
		}
	}
}
