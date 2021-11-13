using System;
using System.Text;

namespace TSLibAutogen
{
	public class CodeBuilder
	{
		public StringBuilder Strb = new();
		public int Level { get; set; } = 0;
		public string Indent => new('\t', Level);

		public void PushLevel() => Level++;
		public void PopLevel()
		{
			Level--;
			if (Level < 0)
			{
				Level = 0;
				Strb.Append("/* ERROR Indentation underflow ERROR */"); // TODO diag error
			}
		}

		public void AppendRaw(string s) => Strb.Append(s);

		public void AppendLine() => AppendSingleLine("");
		public void AppendLine(string s)
		{
			foreach (var line in s.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
				AppendSingleLine(line);
			if (s.EndsWith("{")) PushLevel();
		}

		private void AppendSingleLine(string s)
		{
			if (string.IsNullOrWhiteSpace(s))
				Strb.AppendLine();
			else
				Strb.Append(Indent).AppendLine(s);
		}

		public void AppendFormatLine(string f, params object?[] o)
		{
			Strb.Append(Indent).AppendFormat(f, o).AppendLine();
			if (f.EndsWith("{")) PushLevel();
		}

		public void PopCloseBrace()
		{
			PopLevel();
			AppendLine("}");
		}

		public override string ToString() => Strb.ToString();
	}
}
