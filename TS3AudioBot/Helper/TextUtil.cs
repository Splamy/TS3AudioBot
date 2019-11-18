// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace TS3AudioBot.Helper
{
	public static class TextUtil
	{
		public static Answer GetAnswer(string answer)
		{
			if (string.IsNullOrEmpty(answer))
				return Answer.Unknown;
			if (answer.StartsWith("!y", StringComparison.OrdinalIgnoreCase))
				return Answer.Yes;
			else if (answer.StartsWith("!n", StringComparison.OrdinalIgnoreCase))
				return Answer.No;
			else
				return Answer.Unknown;
		}

		private static readonly Regex BbMatch = new Regex(@"\[URL\](.+?)\[\/URL\]", Util.DefaultRegexConfig);
		public static string ExtractUrlFromBb(string ts3Link)
		{
			if (ts3Link.Contains("[URL]"))
			{
				var match = BbMatch.Match(ts3Link);
				if (match.Success)
					return match.Groups[1].Value;
			}

			return ts3Link;
		}

		public static string StripQuotes(string quotedString, bool throwWhenIncorrect = false)
		{
			if (quotedString.Length <= 1
				|| !quotedString.StartsWith("\"", StringComparison.Ordinal)
				|| !quotedString.EndsWith("\"", StringComparison.Ordinal))
			{
				if (throwWhenIncorrect)
					throw new ArgumentException("The string is not properly quoted");
				else
					return quotedString;
			}

			return quotedString.Substring(1, quotedString.Length - 2);
		}

		public static string GenToken(int length)
		{
			const string tokenChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			using (var rng = RandomNumberGenerator.Create())
			{
				var buffer = new byte[length];
				rng.GetBytes(buffer);
				var strb = new StringBuilder(buffer.Length);
				for (int i = 0; i < buffer.Length; i++)
					strb.Append(tokenChars[((tokenChars.Length - 1) * buffer[i]) / 255]);
				return strb.ToString();
			}
		}

		private static readonly Regex TimeReg = new Regex(@"^(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?(?:(\d+)ms)?$", Util.DefaultRegexConfig);

		public static TimeSpan? ParseTime(string value)
		{
			if (value is null) return null;
			return ParseTimeAsSimple(value)
				?? ParseTimeAsDigital(value)
				?? ParseTimeAsXml(value);
		}

		private static TimeSpan? ParseTimeAsSimple(string value)
		{
			int AsNum(string svalue)
			{
				if (string.IsNullOrEmpty(svalue))
					return 0;
				return int.TryParse(svalue, out var num) ? num : 0;
			}

			var match = TimeReg.Match(value);
			if (match.Success)
			{
				try
				{
					return new TimeSpan(
						AsNum(match.Groups[1].Value),
						AsNum(match.Groups[2].Value),
						AsNum(match.Groups[3].Value),
						AsNum(match.Groups[4].Value),
						AsNum(match.Groups[5].Value));
				}
				catch { }
			}
			return null;
		}

		private static TimeSpan? ParseTimeAsDigital(string value)
		{
			if (value.Contains(":"))
			{
				string[] splittime = value.Split(':');

				if (splittime.Length == 2
					&& int.TryParse(splittime[0], out var minutes)
					&& double.TryParse(splittime[1], NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds))
				{
					return TimeSpan.FromSeconds(seconds) + TimeSpan.FromMinutes(minutes);
				}
			}
			else
			{
				if (double.TryParse(value, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds))
					return TimeSpan.FromSeconds(seconds);
			}
			return null;
		}

		private static TimeSpan? ParseTimeAsXml(string value)
		{
			try { return XmlConvert.ToTimeSpan(value); }
			catch (FormatException) { return null; }
		}

		public static E<string> ValidateTime(string value)
		{
			if (ParseTime(value) != null)
				return R.Ok;
			return $"Value '{value}' is not a valid time.";
		}
	}

	public enum Answer
	{
		Unknown,
		Yes,
		No
	}
}
