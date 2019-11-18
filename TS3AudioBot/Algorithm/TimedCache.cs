// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Concurrent;
using System.Linq;
using TSLib.Helper;

namespace TS3AudioBot.Algorithm
{
	public class TimedCache<TK, TV>
	{
		public TimeSpan Timeout { get; }
		private readonly ConcurrentDictionary<TK, TimedData> cachedData;

		public TimedCache() : this(TimeSpan.FromSeconds(3)) { }

		public TimedCache(TimeSpan timeout)
		{
			Timeout = timeout;
			cachedData = new ConcurrentDictionary<TK, TimedData>();
		}

		public bool TryGetValue(TK key, out TV value)
		{
			if (!cachedData.TryGetValue(key, out var data)
				|| Tools.Now - Timeout > data.Timestamp)
			{
				CleanCache();
				value = default;
				return false;
			}
			value = data.Data;
			return true;
		}

		public void Set(TK key, TV value)
		{
			cachedData[key] = new TimedData { Data = value, Timestamp = Tools.Now };
		}

		public void Clear()
		{
			cachedData.Clear();
		}

		private void CleanCache()
		{
			var now = Tools.Now - Timeout;
			foreach (var item in cachedData.Where(kvp => now > kvp.Value.Timestamp).ToList())
			{
				cachedData.TryRemove(item.Key, out _);
			}
		}

		private struct TimedData
		{
			public TV Data;
			public DateTime Timestamp;
		}
	}
}
