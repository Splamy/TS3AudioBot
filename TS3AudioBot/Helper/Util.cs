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

		public static void WaitOrTimeout(Func<bool> predicate, int msTimeout)
		{
			while (!predicate() && msTimeout-- > 0)
				Thread.Sleep(1);
		}

		public static DateTime GetNow() => DateTime.Now;

		public static void Init<T>(ref T obj) where T : new() => obj = new T();

		public static Random RngInstance { get; } = new Random();

		public static int MathMod(int x, int mod) => ((x % mod) + mod) % mod;
	}
}
