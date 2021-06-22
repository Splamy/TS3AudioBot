#if NETSTANDARD2_0

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

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

	// String

	public static bool Contains(this string str, char c) => str.IndexOf(c) >= 0;

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

	// From .NET Source code
	public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
		{
			return new ValueTask(stream.WriteAsync(array.Array!, array.Offset, array.Count, cancellationToken));
		}

		byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
		buffer.Span.CopyTo(sharedBuffer);
		return new ValueTask(FinishWriteAsync(stream.WriteAsync(sharedBuffer, 0, buffer.Length, cancellationToken), sharedBuffer));

		static async Task FinishWriteAsync(Task writeTask, byte[] localBuffer)
		{
			try
			{
				await writeTask.ConfigureAwait(false);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(localBuffer);
			}
		}
	}

	// From .NET Source code
	public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
		{
			return new ValueTask<int>(stream.ReadAsync(array.Array!, array.Offset, array.Count, cancellationToken));
		}

		byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
		return FinishReadAsync(stream.ReadAsync(sharedBuffer, 0, buffer.Length, cancellationToken), sharedBuffer, buffer);

		static async ValueTask<int> FinishReadAsync(Task<int> readTask, byte[] localBuffer, Memory<byte> localDestination)
		{
			try
			{
				int result = await readTask.ConfigureAwait(false);
				new ReadOnlySpan<byte>(localBuffer, 0, result).CopyTo(localDestination.Span);
				return result;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(localBuffer);
			}
		}
	}

	// IDisposable

	public static ValueTask DisposeAsync(this IDisposable disp)
	{
		disp.Dispose();
		return default;
	}
}

#endif
