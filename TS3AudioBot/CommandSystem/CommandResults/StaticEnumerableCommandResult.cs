namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Linq;

	public class StaticEnumerableCommandResult : EnumerableCommandResult
	{
		readonly IEnumerable<ICommandResult> content;

		public override int Count => content.Count();

		public override ICommandResult this[int index] => content.ElementAt(index);

		public StaticEnumerableCommandResult(IEnumerable<ICommandResult> contentArg)
		{
			content = contentArg;
		}
	}
}
