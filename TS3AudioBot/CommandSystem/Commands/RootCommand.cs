namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// A special group command that also accepts commands as first parameter and executes them on the left over parameters.
	/// </summary>
	public class RootCommand : CommandGroup
	{
		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (!arguments.Any())
				return base.Execute(info, arguments, returnTypes);

			var result = arguments.First().Execute(info, Enumerable.Empty<ICommand>(), new CommandResultType[] { CommandResultType.Command, CommandResultType.String });
			if (result.ResultType == CommandResultType.String)
				return base.Execute(info, arguments, returnTypes);

			return ((CommandCommandResult)result).Command.Execute(info, arguments.Skip(1), returnTypes);
		}
	}
}
