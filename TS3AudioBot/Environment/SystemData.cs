// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using TS3AudioBot.Helper;
using TSLib.Helper;

namespace TS3AudioBot.Environment;

public static partial class SystemData
{
	[GeneratedRegex(@"(\w+)=(.*)", RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Multiline)]
	private static partial Regex PlatformRegex { get; }
	[GeneratedRegex(@"(\d+)(?:\.(\d+)){1,3}", RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Multiline)]
	private static partial Regex SemVerRegex { get; }

	public static BuildData AssemblyData { get; } = new();

	public static string PlatformData { get; } = GenPlatformDat();
	private static string GenPlatformDat()
	{
		string? platform = null;
		string? version = null;
		string bitness = System.Environment.Is64BitProcess ? "64bit" : "32bit";

		if (Tools.IsLinux)
		{
			var values = new Dictionary<string, string>();

			RunBash("cat /etc/*[_-][Rr]elease", x =>
			{
				var lines = x.ReadToEnd().Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
				foreach (var line in lines)
				{
					var match = PlatformRegex.Match(line);
					if (!match.Success)
						continue;

					values[match.Groups[1].Value.ToUpperInvariant()] = TextUtil.StripQuotes(match.Groups[2].Value);
				}

				if (values.Count > 0)
				{
					platform = values.TryGetValue("NAME", out string? value) ? value
							: values.TryGetValue("ID", out value) ? value
							: values.TryGetValue("DISTRIB_ID", out value) ? value
							: values.TryGetValue("PRETTY_NAME", out value) ? value
							: null;

					version = values.TryGetValue("VERSION", out value) ? value
							: values.TryGetValue("VERSION_ID", out value) ? value
							: values.TryGetValue("DISTRIB_RELEASE", out value) ? value
							: null;
				}

				if (platform is null && version is null)
				{
					foreach (var line in lines)
					{
						var match = SemVerRegex.Match(line);
						if (match.Success)
						{
							version = line;
							break;
						}
					}
				}

				platform ??= "Linux";
				version ??= "<?>";
			});
		}
		else
		{
			platform = "Windows";
			version = System.Environment.OSVersion.Version.ToString();
		}

		return $"{platform} {version} ({bitness})";
	}

	[SupportedOSPlatform("Linux")]
	private static void RunBash(string param, Action<StreamReader> action)
	{
		try
		{
			using var p = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "bash",
					Arguments = $"-c \"{param}\"",
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
				},
				EnableRaisingEvents = true,
			};
			p.Start();
			p.WaitForExit(200);

			action.Invoke(p.StandardOutput);
		}
		catch { }
	}

	public static PlatformVersion RuntimeData { get; } = GetNetVersion();

	private static PlatformVersion GetNetVersion()
	{
		var version = System.Environment.Version;
		return new PlatformVersion(Runtime.Core, $".NET ({version})", version);
	}
}

public enum Runtime
{
	Unknown,
	Net,
	Core,
	Mono,
}

public class BuildData
{
	public string Version = "<?>";
	public string CommitSha = "<?>";

	public string BuildConfiguration = "<?>";

	public BuildData()
	{
		var assembly = typeof(BuildData).Assembly;
		var informationalVersion = assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;

		if (!string.IsNullOrEmpty(informationalVersion))
		{
			var metadataSeparator = informationalVersion.IndexOf('+');
			if (metadataSeparator < 0)
			{
				Version = informationalVersion;
			}
			else
			{
				Version = informationalVersion[..metadataSeparator];
				CommitSha = informationalVersion[(metadataSeparator + 1)..];
			}
		}

		BuildConfiguration = assembly
			.GetCustomAttribute<AssemblyConfigurationAttribute>()
			?.Configuration ?? "<?>";
	}

	public string ToLongString() => $"\nVersion: {Version}\nCommitHash: {CommitSha}";
	public override string ToString() => $"{Version}/{CommitSha}";

}

public record PlatformVersion(Runtime Runtime, string FullName, Version? SemVer)
{
	public override string ToString() => FullName;
}

public static class SemVerExtension
{
	public static string AsSemVer(this Version version) => $"{version.Major}.{version.Minor}.{version.Build}" + (version.Revision != 0 ? $".{version.Revision}" : null);
}
