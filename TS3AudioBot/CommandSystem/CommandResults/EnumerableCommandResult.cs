namespace TS3AudioBot.CommandSystem
{
	public abstract class EnumerableCommandResult : ICommandResult
	{
		public override CommandResultType ResultType => CommandResultType.Enumerable;

		public abstract int Count { get; }

		public abstract ICommandResult this[int index] { get; }
	}
}
