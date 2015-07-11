using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Reflection.Emit;

namespace TS3AudioBot
{
	public class Log
	{
		public static bool Active { get; set; }
		public static int StackLevel { get; set; }
		public delegate void cbDelegate(string result);
		private delegate void LogGenerate(LogHelper lh);
		private static LogGenerate[] callbacks;

		private static int longestelem = 0;
		private static string[] spaceup;

		static Log()
		{
			StackLevel = 10;
			Active = true;

			CalcSpaceLength();
		}

		private static void CalcSpaceLength()
		{
			string[] earr = Enum.GetNames(typeof(Level));

			for (int i = 0; i < earr.Length; i++)
				if (earr[i].Length > longestelem)
					longestelem = earr[i].Length;
			spaceup = new string[earr.Length];
			StringBuilder strb = new StringBuilder(longestelem + 1);
			for (int i = 0; i < earr.Length; i++)
			{
				strb.Append(' ', longestelem - earr[i].Length);
				strb.Append(earr[i]);
				spaceup[i] = strb.ToString();
				strb.Clear();
			}
		}

		public static void RegisterLogger(string format, string linebreakIndent, cbDelegate callback)
		{
			DynamicMethod dynLog = new DynamicMethod("LogWrite", typeof(void), new[] { typeof(LogHelper) }, typeof(Log), true);
			var ilGen = dynLog.GetILGenerator();
			var localStrb = ilGen.DeclareLocal(typeof(StringBuilder));

			// common type arrays
			Type[] argsString = { typeof(string) };
			Type[] argsInt = { typeof(int) };

			// common InfosTypes
			MethodInfo miStringBuilder_Append_String = typeof(StringBuilder).GetMethod("Append", argsString);


			// Load stringbuilder
			ilGen.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(Type.EmptyTypes));
			ilGen.Emit(OpCodes.Stloc, localStrb);

			// Append LogLevelSpaced
			ilGen.Emit(OpCodes.Ldloc, localStrb);
			ilGen.Emit(OpCodes.Ldarg_0);
			ilGen.EmitCall(OpCodes.Callvirt, typeof(LogHelper).GetMethod("GenLogLevelSpaced"), null);
			ilGen.EmitCall(OpCodes.Callvirt, miStringBuilder_Append_String, null);

			// add a little seperator
			//ilGen.Emit(OpCodes.Ldloc, localStrb); // strb after Append on stack
			ilGen.Emit(OpCodes.Ldstr, ": ");
			ilGen.EmitCall(OpCodes.Callvirt, miStringBuilder_Append_String, null);

			// add the message
			//ilGen.Emit(OpCodes.Ldloc, localStrb); // strb after Append on stack
			ilGen.Emit(OpCodes.Ldarg_0);
			ilGen.Emit(OpCodes.Ldc_I4_0);
			ilGen.EmitCall(OpCodes.Callvirt, typeof(LogHelper).GetMethod("GenErrorTextFormatted", argsInt), null);
			ilGen.EmitCall(OpCodes.Callvirt, miStringBuilder_Append_String, null);

			// call the callback method
			//ilGen.Emit(OpCodes.Ldloc, localStrb); // strb after Append on stack
			ilGen.EmitCall(OpCodes.Callvirt, typeof(StringBuilder).GetMethod("ToString", Type.EmptyTypes), null);
			ilGen.EmitCall(OpCodes.Callvirt, callback.Method, null);

			ilGen.Emit(OpCodes.Ret);

			// Redim the calllist array
			if (callbacks == null)
			{
				callbacks = new LogGenerate[1];
			}
			else
			{
				LogGenerate[] tmpcopy = new LogGenerate[callbacks.Length + 1];
				Array.Copy(callbacks, tmpcopy, callbacks.Length);
				callbacks = tmpcopy;
			}

			//Store event call in the calllist
			callbacks[callbacks.Length - 1] = (LogGenerate)dynLog.CreateDelegate(typeof(LogGenerate));
		}

		private static void DefaultTest(LogHelper lh, cbDelegate callback)
		{
			StringBuilder strb = new StringBuilder();
			callback(strb.Append(lh.GenLogLevelSpaced())
			.Append(": ")
			.Append(lh.GenErrorTextFormatted(0)).ToString());
		}

		public static void Write(Level lvl, string errText, params object[] infos)
		{
			if (!Active)
				return;

			LogHelper lh = new LogHelper(lvl, new StackTrace(1), errText, infos);
			foreach (var callback in callbacks)
				callback(lh);
		}

		public enum Level : int
		{
			Info,
			Debug,
			Warning,
			Error,
		}

		private class LogHelper
		{
			private StackTrace stackTrace;
			private string errorTextRaw;
			private object[] infos;
			private Level level;

			public LogHelper(Level level, StackTrace stackTrace, string errorTextRaw, object[] infos)
			{
				this.stackTrace = stackTrace;
				this.errorTextRaw = errorTextRaw;
				this.infos = infos;
				this.level = level;
				StackTraceFormatted = new string[stackTrace.FrameCount * 2];
			}

			public string LogLevelRaw = null;
			public string LogLevelSpaced = null;
			public string ErrorTextFormatted = null;
			public string DateFormatted = null;
			public string[] StackTraceFormatted = null;

			public string GenLogLevelRaw()
			{
				if (LogLevelRaw == null)
				{
					LogLevelRaw = level.ToString();
				}
				return LogLevelRaw;
			}

			public string GenLogLevelSpaced()
			{
				if (LogLevelSpaced == null)
				{
					LogLevelSpaced = spaceup[(int)level];
				}
				return LogLevelSpaced;
			}

			public string GenErrorTextFormatted(int linebreakIndent)
			{
				if (ErrorTextFormatted == null)
				{
					string inputbuffer = string.Format(errorTextRaw, infos);
					if (linebreakIndent > 0)
					{
						string spaces = new string(' ', linebreakIndent);
						inputbuffer = Regex.Replace(inputbuffer, @"(\r\n?|\n)", (x) => x.Value + spaces);
					}
					ErrorTextFormatted = inputbuffer;
				}
				return ErrorTextFormatted;
			}

			public string GenDateFormatted()
			{
				if (DateFormatted == null)
				{
					DateFormatted = DateTime.Now.ToString("HH:mm:ss");
				}
				return DateFormatted;
			}

			public static string ExtractAnonymous(string name)
			{
				int startName = name.IndexOf('<');
				int endName = name.IndexOf('>');

				if (startName < 0 || endName < 0)
					return string.Empty;

				return name.Substring(startName, endName - startName);
			}
		}
	}
}
