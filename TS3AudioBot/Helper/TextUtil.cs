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
	using System.Globalization;
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
			string lowAnswer = answer.ToLower(CultureInfo.InvariantCulture);
			if (lowAnswer.StartsWith("!y", StringComparison.Ordinal))
				return Answer.Yes;
			else if (lowAnswer.StartsWith("!n", StringComparison.Ordinal))
				return Answer.No;
			else
				return Answer.Unknown;
		}

		private static readonly Regex bbMatch = new Regex(@"\[URL\](.+?)\[\/URL\]", Util.DefaultRegexConfig);
		public static string ExtractUrlFromBB(string ts3link)
		{
			if (ts3link.Contains("[URL]"))
			{
				var match = bbMatch.Match(ts3link);
				if (match.Success)
					return match.Groups[1].Value;
			}

			return ts3link;
		}

		public static string RemoveUrlBB(string ts3link)
		{
			return bbMatch.Replace(ts3link, "$1");
		}

		public static string StripQuotes(string quotedString)
		{
			if (quotedString.Length <= 1 ||
				!quotedString.StartsWith("\"", StringComparison.Ordinal) ||
				!quotedString.EndsWith("\"", StringComparison.Ordinal))
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
