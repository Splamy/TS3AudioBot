// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Commands
{
	using System;
	using System.Linq;
	using System.Text;

	public static class Ts3String
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
					switch (stringToUnescape[i])
					{
					case (byte)'v': strb[writepos++] = (byte)'\v'; break;  // Vertical Tab
					case (byte)'t': strb[writepos++] = (byte)'\t'; break;  // Horizontal Tab
					case (byte)'r': strb[writepos++] = (byte)'\r'; break;  // Carriage Return
					case (byte)'n': strb[writepos++] = (byte)'\n'; break;  // Newline
					case (byte)'f': strb[writepos++] = (byte)'\f'; break;  // Formfeed
					case (byte)'p': strb[writepos++] = (byte)'|'; break;   // Pipe
					case (byte)'s': strb[writepos++] = (byte)' '; break;   // Whitespace
					case (byte)'/': strb[writepos++] = (byte)'/'; break;   // Slash
					case (byte)'\\': strb[writepos++] = (byte)'\\'; break; // Backslash
					default: throw new FormatException();
					}
				}
				else strb[writepos++] = c;
			}
			return Encoding.UTF8.GetString(strb, 0, writepos);
		}

		public static int TokenLength(string str) => Encoding.UTF8.GetByteCount(str) + str.Count(IsDoubleChar);

		public static bool IsDoubleChar(char c)
		{
			return c == '\\' ||
				c == '/' ||
				c == ' ' ||
				c == '|' ||
				c == '\f' ||
				c == '\n' ||
				c == '\r' ||
				c == '\t' ||
				c == '\v';
		}
	}
}
