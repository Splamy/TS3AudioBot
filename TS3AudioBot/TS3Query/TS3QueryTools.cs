namespace TS3Query
{
	using System;
	using System.Text;

	public static class TS3QueryTools
	{
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
	}

	static class Helper
	{
		public static T Init<T>(ref T fld) where T : new() => fld = new T();
	}
}
