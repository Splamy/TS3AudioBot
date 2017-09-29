// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TS3AudioBot.Helper;

namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Reflection;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;

	[Serializable]
	public static class Log
	{
		private static readonly Regex LinebreakRegex = new Regex(@"(\r\n?|\n)", Util.DefaultRegexConfig);
		private static readonly object WriteLock;
		private static readonly List<LogAction> LoggerFormats;

		private static string[] spaceup;
		private static string[] formatCache;

		private const string PlaceLogLevelSpaced = "{0}";
		private const string PlaceErrorTextFormatted = "{1}";
		private const string PlaceDateFormatted = "{2}";
		private const string PlaceStackTraceFormatted = "{3}";

		static Log()
		{
			WriteLock = new object();
			LoggerFormats = new List<LogAction>();

			Active = true;
			formatCache = new string[0];

			CalcSpaceLength();
		}

		public static bool Active { get; set; }

		private static void CalcSpaceLength()
		{
			string[] earr = Enum.GetNames(typeof(Level));

			int longestelem = 0;
			for (int i = 0; i < earr.Length; i++)
				if (earr[i].Length > longestelem)
					longestelem = earr[i].Length;
			spaceup = new string[earr.Length];
			var strb = new StringBuilder(longestelem + 1);
			for (int i = 0; i < earr.Length; i++)
			{
				strb.Append(' ', longestelem - earr[i].Length);
				strb.Append(earr[i]);
				spaceup[i] = strb.ToString();
				strb.Clear();
			}
		}

		public static void RegisterLogger(string format, int linebreakIndent, CallbackActionDelegate callback)
			=> RegisterLogger(format, linebreakIndent, callback, 0);

		public static void RegisterLogger(string format, int linebreakIndent, CallbackActionDelegate callback, Level logLevel)
		{
			var tokenList = ParseAndValidate(format);
			RegisterLogger(tokenList, linebreakIndent, callback, logLevel);
		}

		private static List<ParseToken> ParseAndValidate(string format)
		{
			var validator = new List<ParseToken>();

			int starttext = 0;
			for (int c = 0; c < format.Length; c++)
			{
				bool specSymbol = format[c] == '%';
				bool endoftext = c >= format.Length - 1;

				if ((specSymbol && c - starttext > 0) || endoftext) // static format text
				{
					if (endoftext) c++;
					validator.Add(new ParseToken(MethodBuildToken.Text, format.Substring(starttext, c - starttext)));
				}

				if (specSymbol)
				{
					if (c + 1 < format.Length)
					{
						switch (format[c + 1])
						{
						case 'L': // Level
							validator.Add(new ParseToken(MethodBuildToken.LogLevelSpaced, null));
							break;
						case 'M': // Message
							validator.Add(new ParseToken(MethodBuildToken.ErrorTextFormatted, null));
							break;
						case 'T': // Time
							validator.Add(new ParseToken(MethodBuildToken.DateFormatted, null));
							break;
						case 'S': // Stack
							validator.Add(new ParseToken(MethodBuildToken.StackFormatted, null));
							break;
						case '%': // %-Escape
							validator.Add(new ParseToken(MethodBuildToken.Text, "%"));
							break;
						default:
							throw new ArgumentException("Unrecognized variable name after '%' at char: " + c);
						}
						c++;
						starttext = c + 1;
					}
					else
					{
						throw new ArgumentException("Missing variable name after '%' at char: " + c);
					}
				}
			}

			return validator;
		}

		private static void RegisterLogger(List<ParseToken> tokenList, int linebreakIndent, CallbackActionDelegate callback, Level logLevel)
		{
			bool usesLls = false, usesEtf = false, usesDf = false, usesStf = false;
			var formatString = new StringBuilder();
			foreach (var part in tokenList)
			{
				switch (part.TokenType)
				{
				case MethodBuildToken.Text:
					formatString.Append(part.Value);
					break;
				case MethodBuildToken.LogLevelSpaced:
					formatString.Append(PlaceLogLevelSpaced);
					usesLls = true;
					break;
				case MethodBuildToken.ErrorTextFormatted:
					formatString.Append(PlaceErrorTextFormatted);
					usesEtf = true;
					break;
				case MethodBuildToken.DateFormatted:
					formatString.Append(PlaceDateFormatted);
					usesDf = true;
					break;
				case MethodBuildToken.StackFormatted:
					formatString.Append(PlaceStackTraceFormatted);
					usesStf = true;
					break;
				default:
					throw Util.UnhandledDefault(part.TokenType);
				}
			}

			lock (WriteLock)
			{
				LoggerFormats.Add(new LogAction(
						formatString.ToString(),
						linebreakIndent,
						callback,
						logLevel,
						usesLls, usesEtf, usesDf, usesStf));

				if (formatCache.Length <= linebreakIndent)
					formatCache = new string[linebreakIndent + 1];
			}
		}

		public static void Write(Level lvl, string errText, params object[] infos)
		{
			if (!Active)
				return;

			var stack = new StackTrace(1);
			lock (WriteLock)
			{
				string logLevelSpaced = null;
				string dateFormatted = null;
				string stackTraceFormatted = null;

				foreach (var callbackProc in LoggerFormats)
				{
					if (lvl < callbackProc.LogLevel)
						continue;

					if (callbackProc.UsesLogLevelSpaced && logLevelSpaced == null)
						logLevelSpaced = GenLogLevelRaw(lvl);
					if (callbackProc.UsesErrorTextFormatted && formatCache[callbackProc.FormatIndent] == null)
						formatCache[callbackProc.FormatIndent] = GenErrorTextFormatted(callbackProc.FormatIndent, errText, infos);
					if (callbackProc.UsesDateFormatted && dateFormatted == null)
						dateFormatted = GenDateFormatted();
					if (callbackProc.UsesStackTraceFormatted && stackTraceFormatted == null)
						stackTraceFormatted = GenStackTraceFormatted(stack);

					callbackProc.Callback.Invoke(
						string.Format(callbackProc.FormatString,
							logLevelSpaced,
							formatCache[callbackProc.FormatIndent],
							dateFormatted,
							stackTraceFormatted),
						lvl);
				}

				Array.Clear(formatCache, 0, formatCache.Length);
			}
		}

		private static string GenLogLevelRaw(Level lvl) => spaceup[(int)lvl];

		private static string GenErrorTextFormatted(int linebreakIndent, string errorTextRaw, object[] infos)
		{
			var text = string.Format(CultureInfo.InvariantCulture, errorTextRaw, infos);
			if (linebreakIndent > 0)
			{
				string indentedSpace = Environment.NewLine + new string(' ', linebreakIndent);
				text = LinebreakRegex.Replace(text, x => indentedSpace);
			}
			return text;
		}

		private static string GenDateFormatted() => DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

		private static string GenStackTraceFormatted(StackTrace stackTrace)
		{
			var strb = new StringBuilder();
			foreach (var frames in stackTrace.GetFrames())
			{
				var method = frames.GetMethod();
				bool endOfIl = method.MethodImplementationFlags == MethodImplAttributes.InternalCall;
				if (endOfIl)
					strb.Append("$internal");
				else
					strb.Append(method).Append('@').Append(frames.GetFileLineNumber()); // TODO Add classname
				strb.Append('\n');
				if (endOfIl) break;
			}

			if (strb.Length > 0)
				strb.Length--; // remove last char
			strb.Append(" T:").Append(Thread.CurrentThread.Name);
			return strb.ToString();
		}

		private enum MethodBuildToken
		{
			Text,
			LogLevelSpaced,
			ErrorTextFormatted,
			DateFormatted,
			StackFormatted,
		}

		public enum Level
		{
			Debug,
			Info,
			Warning,
			Error,
		}

		private class ParseToken
		{
			public readonly MethodBuildToken TokenType;
			public readonly object Value;

			public ParseToken(MethodBuildToken tokenType, object value)
			{
				TokenType = tokenType;
				Value = value;
			}
		}

		private class LogAction
		{
			public string FormatString { get; }
			public int FormatIndent { get; }
			public CallbackActionDelegate Callback { get; }
			public Level LogLevel { get; }

			public bool UsesLogLevelSpaced { get; }
			public bool UsesErrorTextFormatted { get; }
			public bool UsesDateFormatted { get; }
			public bool UsesStackTraceFormatted { get; }

			public LogAction(string formatString, int formatIndent,
				CallbackActionDelegate callback, Level logLevel,
				bool usesLls, bool usesEtf, bool usesDf, bool usesStf)
			{
				FormatString = formatString;
				FormatIndent = formatIndent;
				Callback = callback;
				LogLevel = logLevel;

				UsesLogLevelSpaced = usesLls;
				UsesErrorTextFormatted = usesEtf;
				UsesDateFormatted = usesDf;
				UsesStackTraceFormatted = usesStf;
			}
		}
	}

	[Serializable]
	public delegate void CallbackActionDelegate(string result, Log.Level level);
}
