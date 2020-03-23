// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Linq;
using System.Text;
using TSLib.Helper;
#if NETCOREAPP3_1
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace TSLib.Commands
{
	public static class TsString
	{
		public static string Escape(string stringToEscape) => Escape(stringToEscape.AsSpan());

		public static string Escape(ReadOnlySpan<char> stringToEscape)
		{
			var strb = new StringBuilder(stringToEscape.Length);
			for (int i = 0; i < stringToEscape.Length; i++)
			{
				switch (stringToEscape[i])
				{
				case '\\': strb.Append("\\\\"); break; // Backslash
				case '/': strb.Append("\\/"); break;   // Slash
				case ' ': strb.Append("\\s"); break;   // Whitespace
				case '|': strb.Append("\\p"); break;   // Pipe
				case '\f': strb.Append("\\f"); break;  // Formfeed
				case '\n': strb.Append("\\n"); break;  // Newline
				case '\r': strb.Append("\\r"); break;  // Carriage Return
				case '\t': strb.Append("\\t"); break;  // Horizontal Tab
				case '\v': strb.Append("\\v"); break;  // Vertical Tab
				default: strb.Append(stringToEscape[i]); break;
				}
			}
			return strb.ToString();
		}

		public static string Unescape(string stringToUnescape) => Unescape(stringToUnescape.AsSpan());

		public static string Unescape(ReadOnlySpan<char> stringToUnescape)
		{
			var strb = new StringBuilder(stringToUnescape.Length);
			for (int i = 0; i < stringToUnescape.Length; i++)
			{
				char c = stringToUnescape[i];
				if (c == '\\')
				{
					if (++i >= stringToUnescape.Length) throw new FormatException();
					switch (stringToUnescape[i])
					{
					case 'v': strb.Append('\v'); break;  // Vertical Tab
					case 't': strb.Append('\t'); break;  // Horizontal Tab
					case 'r': strb.Append('\r'); break;  // Carriage Return
					case 'n': strb.Append('\n'); break;  // Newline
					case 'f': strb.Append('\f'); break;  // Formfeed
					case 'p': strb.Append('|'); break;   // Pipe
					case 's': strb.Append(' '); break;   // Whitespace
					case '/': strb.Append('/'); break;   // Slash
					case '\\': strb.Append('\\'); break; // Backslash
					default: throw new FormatException();
					}
				}
				else strb.Append(c);
			}
			return strb.ToString();
		}

		public static string Unescape(ReadOnlySpan<byte> stringToUnescape)
		{
			// The unescaped string is always equal or shorter than the original.
			var strb = new byte[stringToUnescape.Length];
			int writepos = 0;
			for (int i = 0; i < stringToUnescape.Length; i++)
			{
				byte c = stringToUnescape[i];
				if (c == (byte)'\\')
				{
					if (++i >= stringToUnescape.Length) throw new FormatException();
					strb[writepos++] = (stringToUnescape[i]) switch
					{
						(byte)'v' => (byte)'\v', // Vertical Tab
						(byte)'t' => (byte)'\t', // Horizontal Tab
						(byte)'r' => (byte)'\r', // Carriage Return
						(byte)'n' => (byte)'\n', // Newline
						(byte)'f' => (byte)'\f', // Formfeed
						(byte)'p' => (byte)'|',  // Pipe
						(byte)'s' => (byte)' ',  // Whitespace
						(byte)'/' => (byte)'/',  // Slash
						(byte)'\\' => (byte)'\\',// Backslash
						_ => throw new FormatException(),
					};
				}
				else strb[writepos++] = c;
			}
			return Tools.Utf8Encoder.GetString(strb, 0, writepos);
		}

		public static int TokenLength(string str) => Tools.Utf8Encoder.GetByteCount(str) + str.Count(IsDoubleChar);

		public static bool IsDoubleChar(char c) => unchecked(c == (byte)c) && IsDoubleChar(unchecked((byte)c));

#if NETCOREAPP3_1
		private static readonly Vector128<byte> doubleVec = Vector128.Create((byte)'\\', (byte)'/', (byte)' ', (byte)'|', (byte)'\f', (byte)'\n', (byte)'\r', (byte)'\t', (byte)'\v', 0, 0, 0, 0, 0, 0, 0);
#endif

		public static bool IsDoubleChar(byte c)
		{
#if NETCOREAPP3_1
			if (Sse2.IsSupported)
			{
				var inc = Vector128.Create(c);
				var res = Sse2.CompareEqual(doubleVec, inc);
				var mask = Sse2.MoveMask(res);
				return mask != 0;
			}
#endif

			return c == ' ' ||
				c == '/' ||
				c == '|' ||
				c == '\\' ||
				c == '\n' ||
				c == '\r' ||
				c == '\f' ||
				c == '\t' ||
				c == '\v';
		}
	}
}
