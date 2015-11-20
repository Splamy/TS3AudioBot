using System;

namespace TS3AudioBot.Helper
{
	static class TextUtil
	{
		public static int[] ToIntArray(this string value)
		{
			return Array.ConvertAll(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), int.Parse);
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
