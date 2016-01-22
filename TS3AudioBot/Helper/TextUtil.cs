namespace TS3AudioBot.Helper
{
	using System;
	using System.Text.RegularExpressions;

	static class TextUtil
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
	}

	enum Answer
	{
		Unknown,
		Yes,
		No
	}
}
