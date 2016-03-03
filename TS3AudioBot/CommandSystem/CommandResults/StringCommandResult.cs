namespace TS3AudioBot.CommandSystem
{
	public class StringCommandResult : ICommandResult
	{
		readonly string content;

		public override CommandResultType ResultType => CommandResultType.String;
		public virtual string Content => content;

		public StringCommandResult(string contentArg)
		{
			content = contentArg;
		}
	}

}
