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
		public const int MaxMsgLength = 1024;

		public static string Escape(string stringToEscape)
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

		public static string Unescape(string stringToUnescape)
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

		public static int TokenLength(string str) => str.Length + str.Count(IsDoubleChar);

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
