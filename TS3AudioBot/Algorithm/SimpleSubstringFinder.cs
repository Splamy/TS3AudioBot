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
