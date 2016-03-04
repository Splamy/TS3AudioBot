namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;

	public interface ICommand
	{
		/// <summary>Execute this command.</summary>
		/// <param name="info">All global informations for this execution.</param>
		/// <param name="arguments">
		/// The arguments for this command.
		/// They are evaluated lazy which means they will only be evaluated if needed.
		/// </param>
		/// <param name="returnTypes">
		/// The possible return types that should be returned by this execution.
		/// They are ordered by priority so, if possible, the first return type should be picked, then the second and so on.
		/// </param>
		ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes);
	}
}
