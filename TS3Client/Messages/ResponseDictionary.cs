// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Messages
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using KeyType = System.String;
	using ValueType = System.String;

	public class ResponseDictionary : IDictionary<KeyType, ValueType>, IResponse
	{
		private readonly IDictionary<KeyType, ValueType> data;

		public ResponseDictionary() { data = new Dictionary<KeyType, ValueType>(); }
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

		public void SetField(string name, string value) => data[name] = value;
		public string ReturnCode
		{
			get { return data.ContainsKey("return_code") ? data["return_code"] : string.Empty; }
			set { data["return_code"] = value; }
		}
	}

	public sealed class ResponseVoid : IResponse
	{
		public string ReturnCode { get; set; }
		public void SetField(string name, string value) { }
	}
}
