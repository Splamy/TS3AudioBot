namespace TS3AudioBot.History
{
	using System;
	using System.Text;
	using System.Linq;
	using System.Collections.Generic;

	public class SmartHistoryFormatter2 : MarshalByRefObject, IHistoryFormatter
	{
		private const int TS3MAXLENGTH = 1024;
        // configurable constansts
		private const string LineBreak = "\n";
		private const int MinTokenLine = 100;
        // resulting constansts from configuration
		private static readonly int LineBreakLen = TokenLength(LineBreak);
        private static readonly int UseableTokenLine = MinTokenLine - LineBreakLen;

		public string ProcessQuery(AudioLogEntry entry, Func<AudioLogEntry, string> format)
		{
			throw new NotImplementedException();
		}

		public string ProcessQuery(IEnumerable<AudioLogEntry> entries, Func<AudioLogEntry, string> format)
		{
			//! entries[0] is the most recent entry
			// we want the most recent entry at the bottom so we reverse the list at the end
			var entryLines = entries.Select(e =>
			{
				string finStr = format(e);
				return new Line { Value = finStr, TokenLength = TokenLength(finStr) };
			}).Reverse().ToList();

			var queryTokenLen = entryLines.Sum(eL => eL.TokenLength) + (entryLines.Count * LineBreakLen);
			StringBuilder strb;

			// case 1: the entire content fits within the ts3 limitation, we can concat and return it.
			if (queryTokenLen <= TS3MAXLENGTH)
			{
				strb = new StringBuilder(queryTokenLen, queryTokenLen);
				foreach (var eL in entryLines)
					strb.Append(eL.Value).Append(LineBreak);
				return strb.ToString();
			}

			int listStart;
			// case 2: all entrys fit when we trim them to the minlength: trim, concat and return.
			if (entryLines.Count * MinTokenLine < TS3MAXLENGTH)
				listStart = 0;
			// case 3: same as 2 but we have to start a few entries later to fit as many as possible.
			else
                listStart = entryLines.Count - (TS3MAXLENGTH / MinTokenLine);

			int spareToken = TS3MAXLENGTH - (entryLines.Count * MinTokenLine);
			strb = new StringBuilder(queryTokenLen, queryTokenLen);
			for (int i = listStart; i < entryLines.Count; i++)
			{
                if(entryLines[i].TokenLength < UseableTokenLine)
                {
                    strb.Append(entryLines[i].Value).Append(LineBreak);
                    spareToken += UseableTokenLine - entryLines[i].TokenLength; 
                }
                else
                {
				    int bonusToken = spareToken / (entryLines.Count - i);
                    strb.Append(SubstringToken(entryLines[i].Value, UseableTokenLine + bonusToken)).Append(LineBreak);
                    spareToken -= bonusToken;
                }
			}

			return strb.ToString();
		}

		public static string DefaultAleFormat(AudioLogEntry e)
			=> string.Format("{0} ({2}): {1}", e.Id, e.ResourceTitle, e.UserInvokeId, e.PlayCount, e.Timestamp);

		/// <summary>Trims a string to have the given token count at max.</summary>
		/// <param name="value">The string to substring from the left side.</param>
		/// <param name="token">The max token count.</param>
		/// <returns>The new substring.</returns>
		private static string SubstringToken(string value, int token)
		{
			int tokens = 0;
			for (int i = 0; i < value.Length; i++)
			{
				int addToken = IsDoubleChar(value[i]) ? 2 : 1;
				if (tokens + addToken > tokens) return value.Substring(0, i);
				else tokens += addToken;
			}
			return value;
		}

		private static int TokenLength(string str) => str.Length + str.Count(IsDoubleChar);

		private static bool IsDoubleChar(char c)
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

		class Line
		{
			public string Value { get; set; }
			public int TokenLength { get; set; }
		}
	}
}