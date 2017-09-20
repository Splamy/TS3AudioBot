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
		public static bool Active { get; set; }
		public static int StackLevel { get; set; }

		private static readonly object WriteLock;
		private static readonly List<LogAction> LoggerFormats;

		private static string[] spaceup;

		private const string PlaceLogLevelSpaced = "{0}";
		private const string PlaceErrorTextFormatted = "{1}";
		private const string PlaceDateFormatted = "{2}";
		private const string PlaceStackTraceFormatted = "{3}";

		static Log()
		{
			WriteLock = new object();
			LoggerFormats = new List<LogAction>();

			StackLevel = 10;
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
			var strb = new StringBuilder(longestelem + 1);
			for (int i = 0; i < earr.Length; i++)
			{
				strb.Append(' ', longestelem - earr[i].Length);
				strb.Append(earr[i]);
				spaceup[i] = strb.ToString();
				strb.Clear();
			}
		}

		public static void RegisterLogger(string format, string linebreakIndent, CallbackActionDelegate callback)
			=> RegisterLogger(format, linebreakIndent, callback, 0);

		public static void RegisterLogger(string format, string linebreakIndent, CallbackActionDelegate callback, Level logLevel)
		{
			try
			{
				var tokenList = ParseAndValidate(format);
				RegisterLoggerUnsafe(tokenList, linebreakIndent, callback, logLevel);
			}
			catch (ArgumentException) { throw; }
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

		private static void RegisterLoggerUnsafe(List<ParseToken> tokenList, string linebreakIndent, CallbackActionDelegate callback, Level logLevel)
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
						callback,
						logLevel,
						usesLls, usesEtf, usesDf, usesStf));
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
				string errorTextFormatted = null;
				string dateFormatted = null;
				string stackTraceFormatted = null;

				foreach (var callbackProc in LoggerFormats)
				{
					if (lvl < callbackProc.LogLevel)
						continue;

					if (callbackProc.UsesLogLevelSpaced && logLevelSpaced == null)
						logLevelSpaced = GenLogLevelRaw(lvl);
					if (callbackProc.UsesErrorTextFormatted && errorTextFormatted == null)
						errorTextFormatted = GenErrorTextFormatted(0, errText, infos);
					if (callbackProc.UsesDateFormatted && dateFormatted == null)
						dateFormatted = GenDateFormatted();
					if (callbackProc.UsesStackTraceFormatted && stackTraceFormatted == null)
						stackTraceFormatted = GenStackTraceFormatted(stack);

					callbackProc.Callback.Invoke(
						string.Format(callbackProc.FormatString,
							logLevelSpaced,
							errorTextFormatted,
							dateFormatted,
							stackTraceFormatted));
				}
			}
		}

		private static string GenLogLevelRaw(Level lvl) => spaceup[(int)lvl];

		private static string GenErrorTextFormatted(int linebreakIndent, string errorTextRaw, object[] infos)
			 => string.Format(CultureInfo.InvariantCulture, errorTextRaw, infos);

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

		private static string ExtractAnonymous(string name)
		{
			int startName = name.IndexOf('<');
			int endName = name.IndexOf('>');

			if (startName < 0 || endName < 0)
				return string.Empty;

			return name.Substring(startName, endName - startName);
		}

		private static string AdjustIndentation(int indentation, string text)
		{
			string spaces = new string(' ', indentation);
			return Regex.Replace(text, @"(\r\n?|\n)", (x) => x.Value + spaces);
		}

		private enum MethodBuildToken
		{
			Text,
			LogLevelSpaced,
			ErrorTextFormatted,
			DateFormatted,
			StackFormatted,
		}

		public enum Level : int
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
			public CallbackActionDelegate Callback { get; }
			public Level LogLevel { get; }

			public bool UsesLogLevelSpaced { get; }
			public bool UsesErrorTextFormatted { get; }
			public bool UsesDateFormatted { get; }
			public bool UsesStackTraceFormatted { get; }

			public LogAction(string formatString, CallbackActionDelegate callback, Level logLevel,
				bool usesLls, bool usesEtf, bool usesDf, bool usesStf)
			{
				FormatString = formatString;
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
	public delegate void CallbackActionDelegate(string result);
}
