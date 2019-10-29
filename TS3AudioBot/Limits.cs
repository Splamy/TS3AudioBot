using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot
{
	public static class Limits
	{
		/// <summary>Max stream size to download before aborting.</summary>
		public static long MaxImageStreamSize { get; } = 10_000_000; // 10MB
		/// <summary>Max image size which is allowed to be resized from.</summary>
		public static long MaxImageDimension { get; } = 10_000;
	}
}
