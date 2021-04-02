// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using TSLib.Full;

namespace TSLib
{
	/// <summary>Describes a version tuple of version and platform.</summary>
	public class TsVersion
	{
		protected static readonly Regex VersionPattern = new Regex(@"([^ ])* \[Build: (\d+)\]", RegexOptions.ECMAScript | RegexOptions.Compiled);

		private static readonly Dictionary<string, ClientPlatform> Platforms = new Dictionary<string, ClientPlatform> {
			{ "Windows", ClientPlatform.Windows },
			{ "Linux", ClientPlatform.Linux },
			{ "OS X", ClientPlatform.MacOs },
			{ "macOS", ClientPlatform.MacOs },
			{ "Android", ClientPlatform.Android },
			{ "iOS", ClientPlatform.Ios },
		};

		protected static ClientPlatform GetPlatform(string platform)
			=> Platforms.TryGetValue(platform, out var enu) ? enu : ClientPlatform.Other;

		public string Version { get; }
		public string Platform { get; }
		public ClientPlatform PlatformType { get; }
		public ulong Build { get; }

		public TsVersion(string rawVersion, string platform, ulong build)
				: this(rawVersion, platform, GetPlatform(platform), build) { }

		public TsVersion(string rawVersion, string platform, ClientPlatform platformType, ulong build)
		{
			Version = rawVersion;
			Platform = platform;
			PlatformType = platformType;
			Build = build;
		}

		public static TsVersion? TryParse(string version, string platform)
		{
			var match = VersionPattern.Match(version);
			if (!match.Success)
				return null;
			if (!ulong.TryParse(match.Groups[2].Value, out var build))
				return null;
			return new TsVersion(version, platform, build);
		}
	}

	/// <summary>
	/// Describes a triple of version, platform and a cryptographical signature (usually distributed by "TeamSpeak Systems").
	/// Each triple has to match and is not interchangeable with other triple parts.
	/// </summary>
	public sealed partial class TsVersionSigned : TsVersion
	{
		public string Sign { get; }

		public TsVersionSigned(string rawVersion, string platform, ulong build, string sign)
			: this(rawVersion, platform, GetPlatform(platform), build, sign) { }

		public TsVersionSigned(string rawVersion, string platform, ClientPlatform platformType, ulong build, string sign)
			: base(rawVersion, platform, platformType, build)
		{
			Sign = sign;
		}

		public static TsVersionSigned? TryParse(string version, string platform, string sign)
		{
			var match = VersionPattern.Match(version);
			if (!match.Success)
				return null;
			if (!ulong.TryParse(match.Groups[2].Value, out var build))
				return null;
			var prelim = new TsVersionSigned(version, platform, build, sign);
			if (!prelim.CheckValid())
				return null;
			return prelim;
		}

		public bool CheckValid() => TsCrypt.EdCheck(this);
	}

	public enum ClientPlatform
	{
		Other = 0,
		Windows,
		Linux,
		MacOs,
		Android,
		Ios,
	}
}
