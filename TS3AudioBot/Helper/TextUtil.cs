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

namespace TS3AudioBot.Helper
{
	using System;
	using System.Text.RegularExpressions;

	[Serializable]
	public static class TextUtil
	{
		public static int[] ToIntArray(this string value)
		{
			return Array.ConvertAll(value.SplitNoEmpty(','), int.Parse);
		}

		public static string[] SplitNoEmpty(this string value, char splitChar)
		{
			return value.Split(new[] { splitChar }, StringSplitOptions.RemoveEmptyEntries);
		}

		public static Answer GetAnswer(string answer)
		{
			string lowAnswer = answer.ToLower();
			if (lowAnswer.StartsWith("!y"))
				return Answer.Yes;
			else if (lowAnswer.StartsWith("!n"))
				return Answer.No;
			else
				return Answer.Unknown;
		}

		private static readonly Regex bbMatch = new Regex(@"\[URL\](.+?)\[\/URL\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		public static string ExtractUrlFromBB(string ts3link)
		{
			if (ts3link.Contains("[URL]"))
				return Regex.Match(ts3link, @"\[URL\](.+?)\[\/URL\]").Groups[1].Value;
			else
				return ts3link;
		}

		public static string RemoveUrlBB(string ts3link)
		{
			return bbMatch.Replace(ts3link, "$1");
		}

		public static string StripQuotes(string quotedString)
		{
			if (quotedString.Length <= 1 ||
				!quotedString.StartsWith("\"") ||
				!quotedString.EndsWith("\""))
				throw new ArgumentException("The string is not properly quoted");

			return quotedString.Substring(1, quotedString.Length - 2);
		}
	}

	public enum Answer
	{
		Unknown,
		Yes,
		No
	}
}
