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

namespace TS3Query.Messages
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using KeyType = System.String;
	using ValueType = System.String;

	class ResponseDictionary : IDictionary<KeyType, ValueType>, IResponse
	{
		private IDictionary<KeyType, ValueType> data;
		public ResponseDictionary(IDictionary<KeyType, ValueType> dataDict) { data = dataDict; }

		public ValueType this[KeyType key] { get { return data[key]; } set { throw new NotSupportedException(); } }
		public int Count => data.Count;
		public bool IsReadOnly => true;
		public ICollection<KeyType> Keys => data.Keys;
		public ICollection<ValueType> Values => data.Values;
		public void Add(KeyValuePair<KeyType, ValueType> item) { throw new NotSupportedException(); }
		public void Add(KeyType key, ValueType value) { throw new NotSupportedException(); }
		public void Clear() { throw new NotSupportedException(); }
		public bool Contains(KeyValuePair<KeyType, ValueType> item) => data.Contains(item);
		public bool ContainsKey(string key) => data.ContainsKey(key);
		public void CopyTo(KeyValuePair<KeyType, ValueType>[] array, int arrayIndex) => data.CopyTo(array, arrayIndex);
		public IEnumerator<KeyValuePair<KeyType, ValueType>> GetEnumerator() => data.GetEnumerator();
		public bool Remove(KeyValuePair<KeyType, ValueType> item) { throw new NotSupportedException(); }
		public bool Remove(KeyType key) { throw new NotSupportedException(); }
		public bool TryGetValue(KeyType key, out ValueType value) => data.TryGetValue(key, out value);
		IEnumerator IEnumerable.GetEnumerator() => data.GetEnumerator();
	}
}
