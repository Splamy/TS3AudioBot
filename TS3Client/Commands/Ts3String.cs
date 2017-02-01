// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

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
			StringBuilder strb = new StringBuilder(stringToEscape);
			strb = strb.Replace("\\", "\\\\"); // Backslash
			strb = strb.Replace("/", "\\/");   // Slash
			strb = strb.Replace(" ", "\\s");   // Whitespace
			strb = strb.Replace("|", "\\p");   // Pipe
			strb = strb.Replace("\f", "\\f");  // Formfeed
			strb = strb.Replace("\n", "\\n");  // Newline
			strb = strb.Replace("\r", "\\r");  // Carriage Return
			strb = strb.Replace("\t", "\\t");  // Horizontal Tab
			strb = strb.Replace("\v", "\\v");  // Vertical Tab
			return strb.ToString();
		}

		public static string Unescape(string stringToUnescape)
		{
			StringBuilder strb = new StringBuilder(stringToUnescape.Length);
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
