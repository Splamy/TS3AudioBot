namespace TS3AudioBot.Algorithm
{
	using System;
	using System.Collections.Generic;
	using Helper;
	using System.Linq;

	public class SimpleSubstringFinder<T> : ISubstringSearch<T>
	{
		private Dictionary<string, T> values;

		public SimpleSubstringFinder()
		{
			Util.Init(ref values);
		}

		public void Add(string key, T value)
		{
			if (!values.ContainsKey(key))
				values.Add(key, value);
		}

		public void RemoveKey(string key)
		{
			if (!values.ContainsKey(key))
				values.Remove(key);
		}

		public void RemoveValue(T value)
		{
			var arr = values.Where(v => v.Value.Equals(value)).ToArray();
			for (int i = 0; i < arr.Length; i++)
				values.Remove(arr[i].Key);
		}

		public IList<T> GetValues(string key)
			=> values.Where(v => v.Key.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0).Select(v => v.Value).ToList();

		public void Clear()
		{
			values.Clear();
		}
	}
}
