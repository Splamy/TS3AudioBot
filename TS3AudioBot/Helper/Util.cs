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
	using CommandSystem;
	using System;
	using System.IO;
	using System.Reflection;
	using System.Security.Principal;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Web.Script.Serialization;

	[Serializable]
	public static class Util
	{
		public const RegexOptions DefaultRegexConfig = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ECMAScript;

		public static bool IsLinux
		{
			get
			{
				int p = (int)Environment.OSVersion.Platform;
				return (p == 4) || (p == 6) || (p == 128);
			}
		}

		/// <summary>Blocks the thread while the predicate returns false or until the timeout runs out.</summary>
		/// <param name="predicate">Check function that will be called every millisecond.</param>
		/// <param name="timeout">Timeout in milliseconds.</param>
		public static void WaitOrTimeout(Func<bool> predicate, TimeSpan timeout)
		{
			int msTimeout = (int)timeout.TotalSeconds;
			while (!predicate() && msTimeout-- > 0)
				Thread.Sleep(1);
		}

		public static void WaitForThreadEnd(Thread thread, TimeSpan timeout)
		{
			if (thread != null && thread.IsAlive)
			{
				WaitOrTimeout(() => thread.IsAlive, timeout);
				if (thread.IsAlive)
				{
					thread.Abort();
				}
			}
		}

		public static DateTime GetNow() => DateTime.Now;

		public static void Init<T>(ref T obj) where T : new() => obj = new T();

		public static Random Random { get; } = new Random();

		public static JavaScriptSerializer Serializer { get; } = new JavaScriptSerializer();

		public static Encoding Utf8Encoder { get; } = new UTF8Encoding(false, false);

		public static bool IsAdmin
		{
			get
			{
				try
				{
					using (var user = WindowsIdentity.GetCurrent())
					{
						var principal = new WindowsPrincipal(user);
						return principal.IsInRole(WindowsBuiltInRole.Administrator);
					}
				}
				catch (UnauthorizedAccessException) { return false; }
				catch (Exception)
				{
					Log.Write(Log.Level.Warning, "Uncatched admin check.");
					return false;
				}
			}
		}

		public static int MathMod(int x, int mod) => ((x % mod) + mod) % mod;

		private static long Pow(long b, int pow)
		{
			long ret = 1;
			while (pow != 0)
			{
				if ((pow & 1) == 1)
					ret *= b;
				b *= b;
				pow >>= 1;
			}
			return ret;
		}

		public static string FromSeed(int seed)
		{
			char[] seedstr = new char[7];
			uint plainseed = unchecked((uint)seed);
			for (int i = 0; i < 7; i++)
			{
				if (plainseed > 0)
				{
					plainseed--;
					var remainder = plainseed % 26;
					char digit = (char)(remainder + 'a');
					seedstr[i] = digit;
					plainseed = (plainseed - remainder) / 26;
				}
				else
					seedstr[i] = '\0';
			}
			return new string(seedstr).TrimEnd('\0');
		}

		public static int ToSeed(string seed)
		{
			long finalValue = 0;

			for (int i = 0; i < seed.Length; i++)
			{
				long powVal = (seed[i] - 'a' + 1) * Pow(26, i % 7);
				finalValue += powVal;
				finalValue %= ((long)uint.MaxValue + 1);
			}
			uint uval = (uint)finalValue;
			return unchecked((int)uval);
		}

		public static void UnwrapThrow(this R r)
		{
			if (!r.Ok)
				throw new CommandException(r.Message, CommandExceptionReason.CommandError);
		}

		public static T UnwrapThrow<T>(this R<T> r)
		{
			if (r.Ok)
				return r.Value;
			else
				throw new CommandException(r.Message, CommandExceptionReason.CommandError);
		}

		public static string UnrollException(this Exception ex)
		{
			var strb = new StringBuilder();
			while (ex != null)
			{
				strb.AppendFormat("MSG: {0}\nTYPE:{1}\nSTACK:{2}\n", ex.Message, ex.GetType().Name, ex.StackTrace);
				ex = ex.InnerException;
			}
			return strb.ToString();
		}

		public static Exception UnhandledDefault<T>(T value) where T : struct { return new MissingEnumCaseException(typeof(T).Name, value.ToString()); }

		public static Stream GetEmbeddedFile(string name)
		{
			var assembly = Assembly.GetExecutingAssembly();
			return assembly.GetManifestResourceStream(name);
		}

		private static BuildData buildData;
		public static BuildData GetAssemblyData()
		{
			if (buildData == null)
			{
				var gitInfoType = Assembly.GetExecutingAssembly().GetType("TS3AudioBot.GitVersionInformation");
				if (gitInfoType == null)
				{
					buildData = new BuildData();
				}
				else
				{
					buildData = new BuildData
					{
						Version = (string)gitInfoType.GetField("SemVer", BindingFlags.Static | BindingFlags.Public).GetValue(null),
						Branch = (string)gitInfoType.GetField("BranchName", BindingFlags.Static | BindingFlags.Public).GetValue(null),
						CommitSha = (string)gitInfoType.GetField("Sha", BindingFlags.Static | BindingFlags.Public).GetValue(null),
					};
				}
			}
			return buildData;
		}

		public class BuildData
		{
			public string Version = "<?>";
			public string Branch = "<?>";
			public string CommitSha = "<?>";

			public string ToLongString() => $"\nVersion: {Version}\nBranch: {Branch}\nCommitHash: {CommitSha}";
			public override string ToString() => $"{Version}/{Branch}/{(CommitSha.Length > 8 ? CommitSha.Substring(0, 8) : CommitSha)}";
		}
	}

	public class MissingEnumCaseException : Exception
	{
	public MissingEnumCaseException(string enumTypeName, string valueName) : base($"The switch does not handle the value \"{valueName}\" from \"{enumTypeName}\".") { }
	public MissingEnumCaseException(string message, Exception inner) : base(message, inner) { }
	}
}
