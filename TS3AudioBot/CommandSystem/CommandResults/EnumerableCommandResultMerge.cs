namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public class EnumerableCommandResultMerge : EnumerableCommandResult
	{
		readonly IEnumerable<EnumerableCommandResult> internResult;

		public override int Count => internResult.Select(r => r.Count).Sum();

		public override ICommandResult this[int index]
		{
			get
			{
				foreach (var r in internResult)
				{
					if (r.Count < index)
						return r[index];
					index -= r.Count;
				}
				throw new IndexOutOfRangeException("Not enough content available");
			}
		}

		public EnumerableCommandResultMerge(IEnumerable<EnumerableCommandResult> internResultArg)
		{
			internResult = internResultArg;
		}
	}
}
