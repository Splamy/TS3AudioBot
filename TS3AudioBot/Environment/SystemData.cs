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
using System.Text.RegularExpressions;
using TS3AudioBot.Helper;
using TSLib.Helper;

namespace TS3AudioBot.Environment
{
	public static class SystemData
	{
		private static readonly Regex PlatformRegex = new(@"(\w+)=(.*)", RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Multiline);
		private static readonly Regex SemVerRegex = new(@"(\d+)(?:\.(\d+)){1,3}", RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.Multiline);

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
					var lines = x.ReadToEnd().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
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

		private static PlatformVersion UnknownRuntime { get; } = new PlatformVersion(Runtime.Unknown, "? (?)", null);
		public static PlatformVersion RuntimeData { get; } = GetNetVersion() ?? UnknownRuntime;

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

	public partial class BuildData
	{
		public string Version = "<?>";
		public string Branch = "<?>";
		public string CommitSha = "<?>";

		public string BuildConfiguration = "<?>";

		public BuildData()
		{
			GetDataInternal();
		}

		public string ToLongString() => $"\nVersion: {Version}\nBranch: {Branch}\nCommitHash: {CommitSha}";
		public override string ToString() => $"{Version}/{Branch}/{(CommitSha.Length > 8 ? CommitSha.Substring(0, 8) : CommitSha)}";

		partial void GetDataInternal();
	}

	public class PlatformVersion
	{
		public Runtime Runtime { get; }
		public string FullName { get; }
		public Version? SemVer { get; }

		public PlatformVersion(Runtime runtime, string fullName, Version? semVer)
		{
			Runtime = runtime;
			FullName = fullName;
			SemVer = semVer;
		}

		public override string ToString() => FullName;
	}

	public static class SemVerExtension
	{
		public static string AsSemVer(this Version version) => $"{version.Major}.{version.Minor}.{version.Build}" + (version.Revision != 0 ? $".{version.Revision}" : null);
	}
}
