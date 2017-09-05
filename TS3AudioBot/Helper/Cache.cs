// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Concurrent;
	using System.Linq;

	class Cache<K, V>
	{
		public TimeSpan Timeout { get; set; }
		private ConcurrentDictionary<K, TimedData> cachedData;

		public Cache() : this(TimeSpan.FromSeconds(3)) { }

		public Cache(TimeSpan timeout)
		{
			Timeout = timeout;
			cachedData = new ConcurrentDictionary<K, TimedData>();
		}

		public bool TryGetValue(K key, out V value)
		{
			if (!cachedData.TryGetValue(key, out var data)
				|| Util.GetNow() - Timeout > data.Timestamp)
			{
				CleanCache();
				value = default(V);
				return false;
			}
			value = data.Data;
			return true;
		}

		public void Store(K key, V value)
		{
			cachedData[key] = new TimedData { Data = value, Timestamp = Util.GetNow() };
		}

		public void Invalidate()
		{
			cachedData.Clear();
		}

		private void CleanCache()
		{
			var now = Util.GetNow() - Timeout;
			foreach (var item in cachedData.Where(kvp => now > kvp.Value.Timestamp).ToList())
			{
				cachedData.TryRemove(item.Key, out var data);
			}
		}

		private struct TimedData
		{
			public V Data;
			public DateTime Timestamp;
		}
	}
}
