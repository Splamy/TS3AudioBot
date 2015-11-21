using System.Collections.Generic;

namespace TS3AudioBot.Algorithm
{
	public interface ISubstringSearch<T>
	{
		void Add(string key, T value);
		IList<T> GetValues(string key);
		void Clear();
	}
}
