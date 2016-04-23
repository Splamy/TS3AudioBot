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
	using System.Security.Permissions;
	using System.Web.Script.Serialization;

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

		//[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
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
	}
}
