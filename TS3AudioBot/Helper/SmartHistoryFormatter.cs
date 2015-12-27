using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TS3AudioBot.Helper
{
	class SmartHistoryFormatter
	{
		private const int TS3_MAXLENGTH = 1024;

		public string ProcessQuery(AudioLogEntry entry)
		{
			return ProcessQuery(new[] { entry });
		}

		public string ProcessQuery(IEnumerable<AudioLogEntry> entries)
		{
			const string header = "Look what I found:";
			List<LineBuilder> lines = new List<LineBuilder>();

			int currentLength = header.Length;
			int maxLimit = header.Length;
			int skip = 0;
			foreach (var entry in entries)
			{
				var lb = new LineBuilder(entry.Id.ToString(), entry.Title, entry.UserInvokeId.ToString());
				lines.Add(lb);
				currentLength += lb.Length;
				maxLimit += lb.MinLength;
				if (maxLimit > TS3_MAXLENGTH)
					maxLimit -= lines[skip++].Length;
			}

			StringBuilder strb;
			if (lines.Count == 0)
			{
				return "I found nothing!";
			}
			else
			{
				strb = new StringBuilder(TS3_MAXLENGTH);
				strb.AppendLine(header);
			}

			if (currentLength < TS3_MAXLENGTH)
			{
				for (int i = 0; i < lines.Count; i++)
					lines[i].Append(strb);
			}
			else
			{
				int limitTo;
				int linesLeft = lines.Count - skip;
				foreach (var line in lines.Skip(skip))
				{
					if (skip > 0)
						limitTo = 0;  // minimal size per line
					else
						limitTo = (TS3_MAXLENGTH - strb.Length) / linesLeft;
					linesLeft--;

					line.LimitTo = limitTo;
					line.Append(strb);
				}
			}
			return strb.ToString();
		}

		class LineBuilder
		{
			private const int TITLE_MIN_LENGTH = 10;

			private const int NEWLINE_LENGTH = 2;
			private const int FORMAT_CHARS_LENGTH = 5 + NEWLINE_LENGTH;
			private const int DOTS_LENGTH = 3;
			public const int FORMAT_MIN_LENGTH = TITLE_MIN_LENGTH + DOTS_LENGTH;

			public string Id { get; private set; }
			public string Title { get; private set; }
			public string User { get; private set; }

			public string TitleFinal
			{
				get
				{
					if (LimitTo < 0 || Title.Length + ConstLength < LimitTo)
						return Title;
					else
					{
						int titleForceLen = Math.Max(LimitTo - (ConstLength + DOTS_LENGTH), TITLE_MIN_LENGTH);
						return Title.Substring(0, titleForceLen) + "...";
					}
				}
			}
			public int Length { get { return ConstLength + TitleFinal.Length; } }
			public int MinLength { get { return ConstLength + Math.Min(Title.Length, FORMAT_MIN_LENGTH); } }
			private int ConstLength { get { return Id.Length + User.Length + FORMAT_CHARS_LENGTH; } }

			public int LimitTo { get; set; }

			public LineBuilder(string id, string title, string user)
			{
				Id = id;
				Title = title;
				User = user;
				LimitTo = -1;
				// <ID> (<USER>): <TITLE> = 5
			}

			public void Append(StringBuilder strb)
			{
				strb.Append(Id).Append(" (").Append(User).Append("): ").AppendLine(TitleFinal);
			}
		}
	}
}
