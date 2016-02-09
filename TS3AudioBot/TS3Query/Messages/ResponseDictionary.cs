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
