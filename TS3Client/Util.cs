using System;

namespace TS3Client
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	internal static class Util
	{
		public static IEnumerable<T> Slice<T>(this IList<T> arr, int from) => Slice(arr, from, arr.Count - from);
		public static IEnumerable<T> Slice<T>(this IEnumerable<T> arr, int from, int len) => arr.Skip(from).Take(len);

		public static T Init<T>(ref T fld) where T : new() => fld = new T();

		public static Encoding Encoder { get; } = Encoding.UTF8;
	}
}
