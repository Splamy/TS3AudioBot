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

namespace TS3AudioBot.Algorithm.EbnfParser
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Text;
	using System.Text.RegularExpressions;
	using Rule = System.Func<Context, System.Collections.Generic.IList<Token>>;

	public class Parser
	{
		private static readonly Regex lineEndRegex = new Regex(@"(\r\n|\r|\n)");
		private static readonly char[] lineEnd = new char[] { '\r', '\n' };

		public Parser()
		{

		}

		public static R<Token> Tokenize(string input, Rule language)
		{
			var ctx = new Context(input);
			var token = language.Invoke(ctx);

			if (token != null)
			{
				var strb = new StringBuilder();
				token[0].BuildTree(strb, 0);
				Console.WriteLine(strb.ToString());
			}

			if (token == null || ctx.Position < input.Length)
			{
				var errFormat = GenerateError(ctx);
				return errFormat;
			}

			return token[0];
		}

		private static string GenerateError(Context ctx)
		{
			string[] parts = SplitWithLineEnding(ctx.Input);
			int pos = 0, line = 0;
			for (; line < parts.Length - 1; line++)
			{
				if (pos + parts[line].Length > ctx.HighestPosition)
					break;
				pos += parts[line].Length;
			}
			int charpos = ctx.HighestPosition - pos;

			var strb = new StringBuilder();
			GenerateErrorScope(strb, parts, line, charpos, "Unexpected token found");
			return strb.ToString();
		}

		private static void GenerateErrorScope(StringBuilder strb, string[] lines, int line, int charpos, string errorMsg)
		{
			int lineFormatLen = (line + 1).ToString().Length;

			// pre line
			if (line > 0)
				strb.Append((line - 1).ToString().PadLeft(lineFormatLen, '0')).Append(": ")
					.AppendLine(lines[line - 1].TrimEnd(lineEnd));

			// focus/error line
			strb.Append((line).ToString().PadLeft(lineFormatLen, '0')).Append(": ")
				.AppendLine(lines[line].TrimEnd(lineEnd));
			strb.Append(' ', lineFormatLen + 2).Append('~', Math.Max(charpos, 0)).Append('^').AppendLine();

			// post line
			if (line + 1 < lines.Length)
				strb.Append((line + 1).ToString().PadLeft(lineFormatLen, '0')).Append(": ")
					.AppendLine(lines[line + 1].TrimEnd(lineEnd));

			// error message
			strb.AppendLine();
			strb.Append("> ").AppendLine(errorMsg);
		}

		private static string[] SplitWithLineEnding(string text)
		{
			var resultList = new List<string>();

			int prevPos = 0;
			var matches = lineEndRegex.Matches(text);
			foreach (Match match in matches)
			{
				int newPos = match.Index + match.Length;
				resultList.Add(text.Substring(prevPos, newPos - prevPos));
				prevPos = newPos;
			}
			resultList.Add(text.Substring(prevPos, text.Length - prevPos));
			return resultList.ToArray();
		}
	}


}
