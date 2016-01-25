namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	// TODO either static all, or get an interface
	internal class SmartHistoryFormatter
	{
		private const int TS3_MAXLENGTH = 1024;

		public string ProcessQuery(AudioLogEntry entry)
		{
			return ProcessQuery(new[] { entry });
		}

		public string ProcessQuery(IEnumerable<AudioLogEntry> entries)
		{
			const string header = "Look what I found:\n";
			List<LineBuilder> lines = new List<LineBuilder>();

			int currentLength = TokenLength(header);
			int maxLimit = currentLength;
			int skip = 0;
			foreach (var entry in entries)
			{
				var lb = new LineBuilder(entry.Id.ToString(), entry.Title, entry.UserInvokeId.ToString());
				lines.Add(lb);
				currentLength += lb.TokenLength;
				maxLimit += lb.MinLength;
				if (maxLimit > TS3_MAXLENGTH)
					maxLimit -= lines[skip++].TokenLength;
			}

			StringBuilder strb;
			if (lines.Count == 0)
			{
				return "I found nothing!";
			}
			else
			{
				strb = new StringBuilder(TS3_MAXLENGTH);
				strb.Append(header);
			}

			if (currentLength < TS3_MAXLENGTH)
			{
				for (int i = 0; i < lines.Count; i++)
					lines[i].InsertTo(strb);
			}
			else
			{
				int tokenCount = TokenLength(header);
				int limitTo;
				int linesLeft = lines.Count - skip;
				foreach (var line in lines.Skip(skip))
				{
					if (skip > 0)
						limitTo = 0;  // minimal size per line
					else
						limitTo = (TS3_MAXLENGTH - tokenCount) / linesLeft;
					linesLeft--;

					line.LimitTo = limitTo;
					line.InsertTo(strb);
					tokenCount += line.TokenLength;
				}
			}
			string result = strb.ToString();
			// Validate
			if (TokenLength(result) > TS3_MAXLENGTH)
			{
				Log.Write(Log.Level.Error, "Formatter: Parsed string is too long: {0}", result);
				result = "Internal parsing error";
			}
			return result;
		}

		class LineBuilder
		{
			private const int TITLE_MIN_LENGTH = 10;

			private const string FORMATCHARS = " (): \n";
			private static readonly int FORMAT_CHARS_LENGTH = TokenLength(FORMATCHARS);
			private const int DOTS_LENGTH = 3;
			public const int FORMAT_MIN_LENGTH = TITLE_MIN_LENGTH + DOTS_LENGTH;

			public string Id { get; private set; }
			public string Title { get; private set; }
			public string User { get; private set; }

			public string TitleFinal
			{
				get
				{
					if (LimitTo < 0 || TokenLength(Title) + ConstLength < LimitTo)
						return Title;
					else
					{
						int titleForceLen = Math.Max(LimitTo - (ConstLength + DOTS_LENGTH), TITLE_MIN_LENGTH);
						string primSub = Title.Substring(0, titleForceLen);
						int curTrim = TokenLength(primSub);
						if (curTrim > titleForceLen)
						{
							int trimCnt = 0;
							for (int i = primSub.Length - 1; i >= 0 && curTrim > titleForceLen; i--, trimCnt++)
								if (IsDoubleChar(primSub[i])) curTrim -= 2;
								else curTrim--;
							primSub = primSub.Substring(0, titleForceLen - trimCnt);
						}
						return primSub + "...";
					}
				}
			}
			public int TokenLength { get { return ConstLength + TokenLength(TitleFinal); } }
			public int MinLength { get { return ConstLength + Math.Min(TokenLength(Title), FORMAT_MIN_LENGTH); } }
			private int ConstLength { get { return TokenLength(Id) + TokenLength(User) + FORMAT_CHARS_LENGTH; } }

			public int LimitTo { get; set; }

			public LineBuilder(string id, string title, string user)
			{
				Id = id;
				Title = title;
				User = user;
				LimitTo = -1;
				// <ID> (<USER>): <TITLE> = 5
			}

			public void InsertTo(StringBuilder strb)
			{
				strb.Append(Id)
					.Append(" (")
					.Append(User)
					.Append("): ")
					.Append(TitleFinal)
					.Append('\n');
			}
		}

		private static int TokenLength(string str)
		{
			int finLen = str.Length;
			finLen += str.Count(IsDoubleChar);
			return finLen;
		}

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
	}
}
