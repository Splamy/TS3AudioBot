#if NETSTANDARD2_0

using System;
using System.Collections.Generic;

internal static class Extensions
{
	// TimeSpan

	public static TimeSpan Divide(this TimeSpan timeSpan, double divisor) => TimeSpan.FromTicks((long)(timeSpan.Ticks / divisor));

	// Dictionary

	public static bool Remove<K, V>(this Dictionary<K, V> dict, K key, out V value)
	{
		if (dict.TryGetValue(key, out value))
		{
			return dict.Remove(key);
		}
		return false;
	}
}

#endif
