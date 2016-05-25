// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.Algorithm
{
	using System;
	using System.Collections.Generic;

	public class SimpleSubstringFinder<T> : ISubstringSearch<T>
	{
		private List<string> keys;
		private List<T> values;
		private HashSet<string> keyHash;

		public SimpleSubstringFinder()
		{
			keys = new List<string>();
			values = new List<T>();
			keyHash = new HashSet<string>();
		}

		public void Add(string key, T value)
		{
			if (!keyHash.Contains(key))
			{
				keys.Add(key);
				keyHash.Add(key);
				values.Add(value);
			}
		}

		public void Remove(string key)
		{
			for (int i = 0; i < keys.Count; i++)
			{
				if (keys[i] == key)
				{
					RemoveIndex(i);
					return;
				}
			}
		}

		private void RemoveIndex(int i)
		{
			string value = keys[i];
			keys.RemoveAt(i);
			values.RemoveAt(i);
			keyHash.Remove(value);
		}

		public IList<T> GetValues(string key)
		{
			var result = new List<T>();
			for (int i = 0; i < keys.Count; i++)
			{
				if (keys[i].IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					result.Add(values[i]);
				}
			}
			return result;
		}

		public void Clear()
		{
			keys.Clear();
			values.Clear();
			keyHash.Clear();
		}
	}
}
