// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper
{
	using System;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.RegularExpressions;

	[Serializable]
	public static class TextUtil
	{
		public static string[] SplitNoEmpty(this string value, char splitChar)
		{
			return value.Split(new[] { splitChar }, StringSplitOptions.RemoveEmptyEntries);
		}

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
	}

	public enum Answer
	{
		Unknown,
		Yes,
		No
	}
}
