// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

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

		private static readonly object writeLock;
		private static List<LogAction> loggerFormats;

		private static string[] spaceup;

		private const string placeLogLevelSpaced = "{0}";
		private const string placeErrorTextFormatted = "{1}";
		private const string placeDateFormatted = "{2}";
		private const string placeStackTraceFormatted = "{3}";

		static Log()
		{
			writeLock = new object();
			loggerFormats = new List<LogAction>();

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
			bool usesLLS = false, usesETF = false, usesDF = false, usesSTF = false;
			var formatString = new StringBuilder();
			foreach (var part in tokenList)
			{
				switch (part.TokenType)
				{
				case MethodBuildToken.Text:
					formatString.Append(part.Value);
					break;
				case MethodBuildToken.LogLevelSpaced:
					formatString.Append(placeLogLevelSpaced);
					usesLLS = true;
					break;
				case MethodBuildToken.ErrorTextFormatted:
					formatString.Append(placeErrorTextFormatted);
					usesETF = true;
					break;
				case MethodBuildToken.DateFormatted:
					formatString.Append(placeDateFormatted);
					usesDF = true;
					break;
				case MethodBuildToken.StackFormatted:
					formatString.Append(placeStackTraceFormatted);
					usesSTF = true;
					break;
				default:
					throw new InvalidProgramException("Undefined MethodBuildToken occoured");
				}
			}

			lock (writeLock)
			{
				loggerFormats.Add(new LogAction(
						formatString.ToString(),
						callback,
						logLevel,
						usesLLS, usesETF, usesDF, usesSTF));
			}
		}

		public static void Write(Level lvl, string errText, params object[] infos)
		{
			if (!Active)
				return;

			var stack = new StackTrace(1);
			lock (writeLock)
			{
				string logLevelSpaced = null;
				string errorTextFormatted = null;
				string dateFormatted = null;
				string stackTraceFormatted = null;

				foreach (var callbackProc in loggerFormats)
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
					strb.Append(method.ToString()).Append('@').Append(frames.GetFileLineNumber());
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
				bool usesLLS, bool usesETF, bool usesDF, bool usesSTF)
			{
				FormatString = formatString;
				Callback = callback;
				LogLevel = logLevel;

				UsesLogLevelSpaced = usesLLS;
				UsesErrorTextFormatted = usesETF;
				UsesDateFormatted = usesDF;
				UsesStackTraceFormatted = usesSTF;
			}
		}
	}

	[Serializable]
	public delegate void CallbackActionDelegate(string result);
}
