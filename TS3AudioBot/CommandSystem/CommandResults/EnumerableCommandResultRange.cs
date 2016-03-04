namespace TS3AudioBot.CommandSystem
{
	using System;

	public class EnumerableCommandResultRange : EnumerableCommandResult
	{
		readonly EnumerableCommandResult internResult;
		readonly int start;
		readonly int count;

		public override int Count => Math.Min(internResult.Count - start, count);

		public override ICommandResult this[int index]
		{
			get
			{
				if (index >= count)
					throw new IndexOutOfRangeException($"{index} >= {count}");
				return internResult[index + start];
			}
		}

		public EnumerableCommandResultRange(EnumerableCommandResult internResultArg, int startArg, int countArg = int.MaxValue)
		{
			internResult = internResultArg;
			start = startArg;
			count = countArg;
		}
	}

}
