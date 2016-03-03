namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// A special group command that also accepts commands as first parameter and executes them on the left over parameters.
	/// </summary>
	public class RootCommand : CommandGroup
	{
		public override ICommandResult Execute(ExecutionInformation info, IEnumerableCommand arguments, IEnumerable<CommandResultType> returnTypes)
		{
			if (arguments.Count < 1)
				return base.Execute(info, arguments, returnTypes);

			var result = arguments.Execute(0, info, new EmptyEnumerableCommand(), new CommandResultType[] { CommandResultType.Command, CommandResultType.String });
			if (result.ResultType == CommandResultType.String)
				return base.Execute(info, arguments, returnTypes);

			return ((CommandCommandResult)result).Command.Execute(info, new EnumerableCommandRange(arguments, 1), returnTypes);
		}
	}
}
