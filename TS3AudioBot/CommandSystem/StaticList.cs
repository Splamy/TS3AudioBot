// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TS3AudioBot.CommandSystem
{
	internal static class StaticList
	{
		public static IReadOnlyList<T> TrySegment<T>(this IReadOnlyList<T> list, int start)
		{
			if (start == 0)
				return list;
			if (start >= list.Count)
				return Array.Empty<T>();
			return list switch
			{
				T[] array => new ArraySegment<T>(array, start, array.Length - start),
				ArraySegment<T> arrayseg => arrayseg[start..],
				_ => list.Skip(start).ToArray(),
			};
		}

		public static IReadOnlyList<T> TrySegment<T>(this IReadOnlyList<T> list, int start, int length)
		{
			return list switch
			{
				T[] array => new ArraySegment<T>(array, start, length),
				ArraySegment<T> arrayseg => arrayseg.Slice(start, length),
				_ => list.Skip(start).Take(length).ToArray(),
			};
		}

		public static void CopyTo<T>(this IReadOnlyList<T> list, int srcOffset, T[] target, int dstOffset)
			=> CopyTo(list, srcOffset, target, dstOffset, list.Count - srcOffset);

		public static void CopyTo<T>(this IReadOnlyList<T> list, int srcOffset, T[] target, int dstOffset, int length)
		{
			switch (list)
			{
			case T[] array:
				Array.Copy(array, srcOffset, target, dstOffset, length);
				break;

			case ArraySegment<T> segArray:
				if (srcOffset + length > segArray.Count)
					throw new ArgumentOutOfRangeException(nameof(length));
				Array.Copy(segArray.Array!, segArray.Offset + srcOffset, target, dstOffset, length);
				break;

			default:
				for (int i = 0; i < length; i++)
					target[dstOffset + i] = list[srcOffset + i];
				break;
			}
		}
	}
}
