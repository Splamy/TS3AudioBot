using System;

namespace TS3AudioBot.Helper
{
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
	}

	enum Answer
	{
		Unknown,
		Yes,
		No
	}
}
