namespace TS3AudioBot.Algorithm
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public class XCommandFilter<T> : ICommandFilter<T> where T : class
	{
		private IList<Tuple<string, T>> dict = new List<Tuple<string, T>>();

		public XCommandFilter()
		{
		}

		public virtual void Add(string key, T value) => dict.Add(new Tuple<string, T>(key, value));

		private IEnumerable<string> Choose(string input)
		{
			var possibilities = dict.Select(t => new Tuple<string, int>(t.Item1, 0));
			// Filter matching commands
			foreach (var c in input)
			{
				var newPossibilities = from p in possibilities
									   let pos = p.Item1.IndexOf(c, p.Item2)
									   where pos != -1
									   select new Tuple<string, int>(p.Item1, pos + 1);
				if (newPossibilities.Any())
					possibilities = newPossibilities;
			}
			// Take command with lowest index
			int minIndex = possibilities.Min(t => t.Item2);

			return possibilities.Where(t => t.Item2 == minIndex).Select(t => t.Item1);
		}

		public virtual bool TryGetValue(string key, out T value)
		{
			var possibilities = Choose(key).Take(2);
			if (possibilities.Count() != 1)
			{
				value = default(T);
				return false;
			}
			var cmds = dict.Where(t => t.Item1 == possibilities.First()).Take(2);
			if (cmds.Count() != 1)
			{
				value = default(T);
				return false;
			}
			value = cmds.First().Item2;
			return true;
		}
	}
}