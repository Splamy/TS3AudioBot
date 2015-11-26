using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot.Helper
{
	class SmartHistoryFormatter
	{
		private const int TS3_MAXLENGTH = 1024;

		public void ProcessQuery(SeachQuery query, AudioLogEntry entry)
		{

		}

		public string ProcessQuery(SeachQuery query, IEnumerable<AudioLogEntry> entries)
		{
			const string header = "Look what I found:";
			List<LineBuilder> lines = new List<LineBuilder>();

			int currentLength = header.Length;
			int maxLimit = header.Length;
			foreach (var entry in entries)
			{
				var lb = new LineBuilder(entry.Id.ToString(), entry.Title, entry.UserInvokeId.ToString());
				lines.Add(lb);
				currentLength += lb.Length;
				maxLimit = lb.MinLength;
			}

			StringBuilder strb;
			if (lines.Count == 0)
				return "I found nothing!";
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
			else if (maxLimit < TS3_MAXLENGTH)
			{
				int limitTo = (TS3_MAXLENGTH - header.Length) / lines.Count;
				for (int i = 0; i < lines.Count; i++)
				{
					lines[i].LimitTo = limitTo;
					lines[i].Append(strb);
				}
			}
			else
			{
				int maxAdd = (TS3_MAXLENGTH - header.Length) / LineBuilder.MIN_TITLE_LENGTH;
				int limitTo = (TS3_MAXLENGTH - header.Length) / lines.Count;
				for (int i = 0; i < maxAdd; i++)
				{
					lines[i].LimitTo = limitTo;
					lines[i].Append(strb);
				}
			}
			return strb.ToString();
		}

		class LineBuilder
		{
			private const int FORMAT_LENGTH = 5;
			private const int NEWLINE_LENGTH = 2;
			private const int DOTS_LENGTH = 3;
			public const int MIN_TITLE_LENGTH = 10 + DOTS_LENGTH; // 10 = <min title len>, 3 = "..."

			public string Id { get; private set; }
			private string title;
			public string Title
			{
				get
				{
					if (LimitTo > -1)
						return title.Substring(Math.Min(title.Length, Math.Max(LimitTo - DOTS_LENGTH, MIN_TITLE_LENGTH))) + "...";
					else
						return title;
				}
			}
			public string User { get; private set; }

			public LineBuilder(string id, string title, string user)
			{
				Id = id;
				this.title = title;
				User = user;
				LimitTo = -1;
				// <ID> (<USER>): <TITLE> = 5
			}

			public int Length
			{
				get { return Id.Length + title.Length + User.Length + FORMAT_LENGTH + NEWLINE_LENGTH; }
			}
			public bool CanLimitTitle { get { return title.Length > MIN_TITLE_LENGTH; } }
			public int LimitTo { get; set; }
			public int MinLength { get { return Math.Min(title.Length, MIN_TITLE_LENGTH); } }

			public void Append(StringBuilder strb)
			{
				strb.Append(Id).Append(" (").Append(User).Append("): ").AppendLine(Title);
			}
		}
	}
}
