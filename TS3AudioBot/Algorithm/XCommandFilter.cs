using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TS3AudioBot.Algorithm
{
	public class XCommandFilter<T> : ICommandFilter<T> where T : class
	{
		private IList<Tuple<string, T>> dict = new List<Tuple<string, T>>();

		public XCommandFilter()
		{
		}

		public virtual void Add(string key, T value) => dict.Add(new Tuple<string, T>(key, value));

		private string[] Choose(string input)
		{
			IEnumerable<Tuple<string, int>> possibilities = dict.Select(
				t => new Tuple<string, int>(t.Item1, 0));
			int inputPos = 0;
			// Filter matching commands
			while (inputPos < input.Length)
			{
				IList<Tuple<string, int>> newPossibilities = new List<Tuple<string, int>>();
				foreach (var p in possibilities)
				{
					int pos = p.Item1.IndexOf(input[inputPos], p.Item2);
					if (pos != -1)
						newPossibilities.Add(new Tuple<string, int>(p.Item1, pos + 1));
				}
				if (newPossibilities.Any())
					possibilities = newPossibilities;
				inputPos++;
			}
			// Take command with lowest index
			int minIndex = possibilities.Select(t => t.Item2).Min();

			return possibilities.Where(t => t.Item2 == minIndex).Select(t => t.Item1).ToArray();
		}

		public virtual bool TryGetValue(string key, out T value)
		{
			string[] possibilities = Choose(key);
			if (possibilities.Length != 1)
			{
				value = default(T);
				return false;
			}
			Tuple<string, T>[] cmds = dict.Where(t => t.Item1 == possibilities[0]).ToArray();
			if (cmds.Length != 1)
			{
				value = default(T);
				return false;
			}
			value = cmds[0].Item2;
			return true;
		}
	}
}

