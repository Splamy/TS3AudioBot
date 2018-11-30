// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using System.Text.RegularExpressions;

	public readonly struct Ts3Version
	{
		private static readonly Regex versionMatch = new Regex(@"([\d\.]+) \[Build: (\d)+\]", RegexOptions.ECMAScript | RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public static readonly Ts3Version Empty = new Ts3Version("", 0);

		public string VersionName { get; }
		public int BuildNumber { get; }

		private Ts3Version(string versionName, int buildNumber)
		{
			VersionName = versionName;
			BuildNumber = buildNumber;
		}

		public static Ts3Version? Parse(string versionString)
		{
			var match = versionMatch.Match(versionString);
			if (!match.Success)
				return null;
			if (!int.TryParse(match.Groups[2].Value, out var buildNumber))
				return null;
			return new Ts3Version(match.Groups[1].Value, buildNumber);
		}
	}
}
