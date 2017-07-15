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
