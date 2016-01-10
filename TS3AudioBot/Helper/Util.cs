using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace TS3AudioBot.Helper
{
	static class Util
	{
		static Util() { }

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
				Process tmproc = new Process();
				ProcessStartInfo psi = new ProcessStartInfo() { FileName = path, };
				tmproc.StartInfo = psi;
				tmproc.Start();
				// Test if it was started successfully
				// True if the process runs for more than 10 ms or the exit code is 0
				return !tmproc.WaitForExit(10) || tmproc.ExitCode == 0;
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
	}
}
