namespace TS3AudioBot.CommandSystem
{
	public class EmptyCommandResult : ICommandResult
	{
		public override CommandResultType ResultType => CommandResultType.Empty;
	}
}
