using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TS3AudioBot
{
	public class Log
	{
		public static bool Active { get; set; }
		public static int StackLevel { get; set; }
		public delegate void LogDelegate(Level lvl, string infoMessage, string detailedMessage);
		public static event LogDelegate OnLog;

		private static string[] spaceup;

		static Log()
		{
			StackLevel = 2;
			Active = true;

			CalcSpaceLength();
		}

		private static void CalcSpaceLength()
		{
			string[] earr = Enum.GetNames(typeof(Level));
			int longestelem = 0;
			for (int i = 0; i < earr.Length; i++)
				if (earr[i].Length > longestelem)
					longestelem = earr[i].Length;
			spaceup = new string[earr.Length];
			StringBuilder strb = new StringBuilder(longestelem + 1);
			for (int i = 0; i < earr.Length; i++)
			{
				strb.Append(' ', longestelem - earr[i].Length);
				strb.Append(earr[i]);
				strb.Append(": ");
				spaceup[i] = strb.ToString();
				strb.Clear();
			}
		}

		public static void Write(Level lvl, string errText, params object[] infos)
		{
			if (!Active || OnLog == null) return;

			string inputbuffer = string.Format(errText, infos);
			inputbuffer = Regex.Replace(inputbuffer, @"(\n|\r|\r\n)", (x) => x.Value + "   ");

			StringBuilder strb = new StringBuilder();
			strb.Append(spaceup[(int)lvl]);
			for (int i = StackLevel; i >= 1; i--)
			{
				StackFrame frame = new StackFrame(i);
				var method = frame.GetMethod();
				var type = method.DeclaringType;
				strb.Append(type.Name);
				strb.Append(".");
				strb.Append(method.Name);
				if (i > 1)
					strb.Append(">");
				else
					strb.Append(": ");
			}
			strb.Append(inputbuffer);
			strb.AppendLine();
			OnLog(lvl, inputbuffer, strb.ToString());
		}

		public enum Level : int
		{
			Info,
			Debug,
			Warning,
			Error,
		}
	}
}
