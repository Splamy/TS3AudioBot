using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot.CommandSystem
{
	internal static class StaticList
	{
		public static IReadOnlyList<T> Empty<T>() => StaticListInternal<T>.Empty();

		public static IReadOnlyList<T> TrySegment<T>(this IReadOnlyList<T> list, int start)
		{
			if (start == 0)
				return list;
			if (list is T[] array)
				return new ArraySegment<T>(array, start, array.Length - start);
			return list.Skip(start).ToArray();
		}

		public static IReadOnlyList<T> TrySegment<T>(this IReadOnlyList<T> list, int start, int length)
		{
			if (start == 0)
				return list;
			if (list is T[] array)
				return new ArraySegment<T>(array, start, length);
			return list.Skip(start).Take(length).ToArray();
		}

		public static void CopyTo<T>(this IReadOnlyList<T> list, int srcOffset, T[] target, int dstOffset)
			=> CopyTo<T>(list, srcOffset, target, dstOffset, list.Count - srcOffset);

		public static void CopyTo<T>(this IReadOnlyList<T> list, int srcOffset, T[] target, int dstOffset, int length)
		{
			if (list is T[] array)
				Array.Copy(array, srcOffset, target, dstOffset, length);
			else if (list is ArraySegment<T> segArray)
			{
				if (srcOffset + length > segArray.Count)
					throw new ArgumentOutOfRangeException(nameof(length));
				Array.Copy(segArray.Array, segArray.Offset + srcOffset, target, dstOffset, length);
			}
			else
			{
				for (int i = 0; i < length; i++)
					target[dstOffset + i] = list[srcOffset + i];
			}
		}

		private static class StaticListInternal<T>
		{
			private static readonly T[] EmptyInstance = new T[0];

			public static IReadOnlyList<T> Empty() => EmptyInstance;
		}
	}
}
