#if NETSTANDARD2_0

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

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

	// Stream

	public static int Read(this Stream stream, Span<byte> buffer)
	{
		byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
		try
		{
			int read = stream.Read(sharedBuffer, 0, sharedBuffer.Length);
			sharedBuffer.AsSpan(0, read).CopyTo(buffer);
			return read;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(sharedBuffer);
		}
	}
}

#endif
