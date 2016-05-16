namespace TS3AudioBot.Algorithm
{
	using System.Collections.Generic;

	public interface ISubstringSearch<T>
	{
		void Add(string key, T value);
		void Remove(string key);
		IList<T> GetValues(string key);
		void Clear();
	}
}
