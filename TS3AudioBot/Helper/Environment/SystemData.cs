// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper.Environment
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using PlatformVersion = System.ValueTuple<Runtime, string, System.Version>;

	public static class SystemData
	{
		private static readonly Regex PlattformRegex = new Regex(@"(\w+)=(.*)", RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Multiline);
		private static readonly Regex SemVerRegex = new Regex(@"(\d+)(?:\.(\d+)){2,3}", RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Multiline);

		public static bool IsLinux { get; }
			= Environment.OSVersion.Platform == PlatformID.Unix
			|| Environment.OSVersion.Platform == PlatformID.MacOSX
			|| ((int)Environment.OSVersion.Platform == 128);

		public static BuildData AssemblyData { get; } = GenAssemblyData();
		private static BuildData GenAssemblyData()
		{
			var gitInfoType = Assembly.GetExecutingAssembly().GetType(nameof(TS3AudioBot) + ".GitVersionInformation");
			if (gitInfoType == null)
				return new BuildData();

			return new BuildData
			{
				Version = (string)gitInfoType.GetField("SemVer", BindingFlags.Static | BindingFlags.Public)?.GetValue(null),
				Branch = (string)gitInfoType.GetField("BranchName", BindingFlags.Static | BindingFlags.Public)?.GetValue(null),
				CommitSha = (string)gitInfoType.GetField("Sha", BindingFlags.Static | BindingFlags.Public)?.GetValue(null),
			};
		}

		public static string PlattformData { get; } = GenPlattformDat();
		private static string GenPlattformDat()
		{
			string plattform = null;
			string version = "<?>";
			string bitness = Environment.Is64BitProcess ? "64bit" : "32bit";

			if (IsLinux)
			{
				var values = new Dictionary<string, string>();

				try
				{
					var p = new Process()
					{
						StartInfo = new ProcessStartInfo()
						{
							FileName = "bash",
							Arguments = "-c \"cat /etc/*[_-]release\"",
							CreateNoWindow = true,
							UseShellExecute = false,
							RedirectStandardOutput = true,
						}
					};
					p.Start();
					p.WaitForExit(100);

					while (p.StandardOutput.Peek() > -1)
					{
						var infoLine = p.StandardOutput.ReadLine();
						if (string.IsNullOrEmpty(infoLine))
							continue;
						var match = PlattformRegex.Match(infoLine);
						if (!match.Success)
							continue;

						values[match.Groups[1].Value.ToUpper()] = TextUtil.StripQuotes(match.Groups[2].Value);
					}
				}
				catch { }

				string value;
				plattform = values.TryGetValue("NAME", out value) ? value
						: values.TryGetValue("ID", out value) ? value
						: values.TryGetValue("DISTRIB_ID", out value) ? value
						: values.TryGetValue("PRETTY_NAME", out value) ? value
						: "Linux";
				version = values.TryGetValue("VERSION", out value) ? value
						: values.TryGetValue("VERSION_ID", out value) ? value
						: values.TryGetValue("DISTRIB_RELEASE", out value) ? value
						: "<?>";
			}
			else
			{
				plattform = "Windows";
				version = Environment.OSVersion.Version.ToString();
			}

			return $"{plattform} {version} ({bitness})";
		}

		public static (Runtime Runtime, string FullName, Version SemVer) RuntimeData { get; } = GenRuntimeData();
		private static PlatformVersion GenRuntimeData()
		{
			var ver = GetNetCoreVersion();
			if (ver.HasValue)
				return ver.Value;

			ver = GetMonoVersion();
			if (ver.HasValue)
				return ver.Value;

			ver = GetNetFrameworkVersion();
			if (ver.HasValue)
				return ver.Value;

			return (Runtime.Unknown, "? (?)", null);
		}

		private static PlatformVersion? GetNetCoreVersion()
		{
			var assembly = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly;
			var assemblyPath = assembly.CodeBase.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			int netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
			if (netCoreAppIndex <= 0 || netCoreAppIndex >= assemblyPath.Length - 2)
				return null;
			var version = assemblyPath[netCoreAppIndex + 1];
			var semVer = ParseToSemVer(version);
			return (Runtime.Core, $".NET Core ({version})", semVer);
		}

		private static PlatformVersion? GetMonoVersion()
		{
			var type = Type.GetType("Mono.Runtime");
			if (type == null)
				return null;
			var displayName = type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
			if (displayName == null)
				return (Runtime.Mono, "Mono (?)", null);
			var version = displayName.Invoke(null, null) as string;
			var semVer = ParseToSemVer(version);
			return (Runtime.Mono, $"Mono ({version})", semVer);
		}

		private static PlatformVersion? GetNetFrameworkVersion()
		{
			var version = Environment.Version.ToString();
			var semVer = ParseToSemVer(version);
			return (Runtime.Net, $".NET Framework {version}", semVer);
		}

		private static Version ParseToSemVer(string version)
		{
			var semMatch = SemVerRegex.Match(version);
			if (!semMatch.Success)
				return null;

			if (!int.TryParse(semMatch.Groups[1].Value, out var major)) major = 0;
			if (!int.TryParse(semMatch.Groups[2].Captures[0].Value, out var minor)) minor = 0;
			if (!int.TryParse(semMatch.Groups[2].Captures[1].Value, out var patch)) patch = 0;
			if (!(semMatch.Groups[2].Captures.Count > 2
				&& int.TryParse(semMatch.Groups[2].Captures[2].Value, out var revision))) revision = 0;
			return new Version(major, minor, patch, revision);
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
		public string Branch = "<?>";
		public string CommitSha = "<?>";

		public string ToLongString() => $"\nVersion: {Version}\nBranch: {Branch}\nCommitHash: {CommitSha}";
		public override string ToString() => $"{Version}/{Branch}/{(CommitSha.Length > 8 ? CommitSha.Substring(0, 8) : CommitSha)}";
	}

	public static class SemVerExtension
	{
		public static string AsSemVer(this Version version) => $"{version.Major}.{version.Minor}.{version.Build}" + (version.Revision != 0 ? $".{version.Revision}" : null);
	}
}
