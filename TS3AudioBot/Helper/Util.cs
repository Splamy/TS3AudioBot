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

namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Reflection;
	using System.IO;
	using System.Security.Principal;
	using System.Web.Script.Serialization;
	using System.Text.RegularExpressions;

	[Serializable]
	public static class Util
	{
		public static bool IsLinux
		{
			get
			{
				int p = (int)Environment.OSVersion.Platform;
				return (p == 4) || (p == 6) || (p == 128);
			}
		}

		public static bool Execute(string path)
		{
			try
			{
				using (Process tmproc = new Process())
				{
					ProcessStartInfo psi = new ProcessStartInfo() { FileName = path, };
					tmproc.StartInfo = psi;
					tmproc.Start();
					// Test if it was started successfully
					// True if the process runs for more than 10 ms or the exit code is 0
					return !tmproc.WaitForExit(10) || tmproc.ExitCode == 0;
				}
			}
			catch (Win32Exception ex)
			{
				Log.Write(Log.Level.Error, "\"{0}\" couldn't be run/found ({1})", path, ex);
				return false;
			}
		}

		public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int amount)
		{
			return source.Skip(Math.Max(0, source.Count() - amount));
		}

		/// <summary>Blocks the thread while the predicate returns false or until the timeout runs out.</summary>
		/// <param name="predicate">Check function that will be called every millisecond.</param>
		/// <param name="msTimeout">Timeout in millisenconds.</param>
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

		public static Random RngInstance { get; } = new Random();

		public static JavaScriptSerializer Serializer { get; } = new JavaScriptSerializer();

		public static byte[] GetResource(string file)
		{
			using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(file))
			{
				if (stream == null)
					throw new InvalidOperationException("Resource not found");
				using (MemoryStream ms = new MemoryStream())
				{
					stream.CopyTo(ms);
					return ms.ToArray();
				}
			}
		}

		public static bool IsAdmin
		{
			get
			{
				try
				{
					using (WindowsIdentity user = WindowsIdentity.GetCurrent())
					{
						WindowsPrincipal principal = new WindowsPrincipal(user);
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

		public static bool RegisterFolderEvents(DirectoryInfo dir, FileSystemEventHandler callback)
		{
			if (!IsAdmin)
				return false;

			if (!dir.Exists)
				return false;

			var watcher = new FileSystemWatcher
			{
				Path = dir.FullName,
				NotifyFilter = NotifyFilters.LastWrite,
			};
			watcher.Changed += callback;
			watcher.EnableRaisingEvents = true;
			return true;
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
				if (plainseed > 26)
				{
					seedstr[i] = (char)((plainseed % 26) + 'a' - 1);
					plainseed /= 26;
				}
				else if (plainseed > 0)
				{
					seedstr[i] = (char)(plainseed + 'a' - 1);
					plainseed = 0;
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

		public const RegexOptions DefaultRegexConfig = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
	}
}
