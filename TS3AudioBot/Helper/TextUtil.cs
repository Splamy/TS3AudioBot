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
	using System.Globalization;
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
			string lowAnswer = answer.ToLower(CultureInfo.InvariantCulture);
			if (lowAnswer.StartsWith("!y", StringComparison.Ordinal))
				return Answer.Yes;
			else if (lowAnswer.StartsWith("!n", StringComparison.Ordinal))
				return Answer.No;
			else
				return Answer.Unknown;
		}

		private static readonly Regex bbMatch = new Regex(@"\[URL\](.+?)\[\/URL\]", Util.DefaultRegexConfig);
		public static string ExtractUrlFromBB(string ts3link)
		{
			if (ts3link.Contains("[URL]"))
			{
				var match = bbMatch.Match(ts3link);
				if (match.Success)
					return match.Groups[1].Value;
			}

			return ts3link;
		}

		public static string StripQuotes(string quotedString)
		{
			if (quotedString.Length <= 1 ||
				!quotedString.StartsWith("\"", StringComparison.Ordinal) ||
				!quotedString.EndsWith("\"", StringComparison.Ordinal))
				throw new ArgumentException("The string is not properly quoted");

			return quotedString.Substring(1, quotedString.Length - 2);
		}

		public static string GenToken(int len)
		{
			const string alph = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

			var arr = new char[len];
			for (int i = 0; i < arr.Length; i++)
				arr[i] = alph[Util.Random.Next(0, alph.Length)];
			return new string(arr);
		}
	}

	public enum Answer
	{
		Unknown,
		Yes,
		No
	}
}
