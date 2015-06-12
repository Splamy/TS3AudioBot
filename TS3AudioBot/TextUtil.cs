using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
