// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections;
using System.Collections.Generic;
using TSLib.Helper;
using KeyType = System.String;
using ValueType = System.String;

namespace TSLib.Messages
{
	public class ResponseDictionary : IDictionary<KeyType, ValueType>, IResponse
	{
		private readonly IDictionary<KeyType, ValueType> data;

		public ResponseDictionary() { data = new Dictionary<KeyType, ValueType>(); }
		public ResponseDictionary(IDictionary<KeyType, ValueType> dataDict) { data = dataDict; }

		public ValueType this[KeyType key]
		{
			get => data[key];
			set => throw new NotSupportedException();
		}
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

		public void SetField(string name, ReadOnlySpan<byte> value, Deserializer ser) => data[name] = value.NewUtf8String();
		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			foreach (var fld in flds)
			{
				if (TryGetValue(fld, out var fldval))
				{
					foreach (var toi in (ResponseDictionary[])to)
					{
						toi.data[fld] = fldval;
					}
				}
			}
		}
		public string ReturnCode
		{
			get => data.ContainsKey("return_code") ? data["return_code"] : string.Empty;
			set => data["return_code"] = value;
		}
	}

	public sealed class ResponseVoid : IResponse
	{
		public string ReturnCode { get; set; }
		public void SetField(string name, ReadOnlySpan<byte> value, Deserializer ser) { }
		public void Expand(IMessage[] to, IEnumerable<string> flds) { }
	}
}
