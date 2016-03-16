namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Linq;

	public class LazyCommand : ICommand
	{
		readonly ICommand innerCommand;
		/// <summary>
		/// The cached result, if available.
		/// </summary>
		ICommandResult result;
		
		public LazyCommand(ICommand innerCommandArg)
		{
			innerCommand = innerCommandArg;
		}

		public ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (result == null)
			{
				result = innerCommand.Execute(info, arguments, returnTypes);
				return result;
			}
			// Check if we can return that type
			if (!returnTypes.Contains(result.ResultType))
				throw new CommandException("The cached result can't be returned");
			return result;
		}
	}
}
