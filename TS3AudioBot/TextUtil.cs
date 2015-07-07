using System;

namespace TS3AudioBot
{
	static class TextUtil
	{
		public static int[] ToIntArray(this string value)
		{
			return Array.ConvertAll(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), int.Parse);
		}
	}
}
