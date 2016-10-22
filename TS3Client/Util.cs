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

		public static void Init<T>(ref T fld) where T : new() => fld = new T();

		public static Encoding Encoder { get; } = new UTF8Encoding(false);

		public static IEnumerable<string> SplitInParts(this string s, int partLength)
		{
			if (s == null)
				throw new ArgumentNullException(nameof(s));
			if (partLength <= 0)
				throw new ArgumentException("Part length has to be positive.", nameof(partLength));

			for (var i = 0; i < s.Length; i += partLength)
				yield return s.Substring(i, Math.Min(partLength, s.Length - i));
		}

		public static byte[] Hex2Byte(this string str)
		{
			return (str.Contains(' ')
					? str.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
					: str.SplitInParts(2))
				.Select(x => Convert.ToByte(x, 16))
				.ToArray();
		}

		public static string Byte2Hex(this IList<byte> ba) => Byte2Hex(ba, 0, ba.Count);
		public static string Byte2Hex(this IList<byte> ba, int index, int length)
		{
			StringBuilder hex = new StringBuilder(length * 2);
			int max = index + length;
			for (int i = index; i < max; i++)
				hex.AppendFormat("{0:x2} ", ba[i]);
			return hex.ToString();
		}
	}
}
