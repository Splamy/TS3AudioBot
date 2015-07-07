using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Linq.Expressions;

namespace TS3AudioBot
{
	static class Util
	{
		private static Dictionary<FilePath, string> filePathDict;

		static Util()
		{
			filePathDict = new Dictionary<FilePath, string>();
			filePathDict.Add(FilePath.VLC, IsLinux ? "vlc" : @"D:\VideoLAN\VLC\vlc.exe");
			filePathDict.Add(FilePath.StartTsBot, IsLinux ? "StartTsBot.sh" : "ping");
			filePathDict.Add(FilePath.ConfigFile, "configTS3AudioBot.cfg");
			filePathDict.Add(FilePath.HistoryFile, "audioLog.sqlite");
		}

		public static bool IsLinux
		{
			get
			{
				int p = (int)Environment.OSVersion.Platform;
				return (p == 4) || (p == 6) || (p == 128);
			}
		}

		public static bool Execute(FilePath filePath)
		{
			try
			{
				string name = GetFilePath(filePath);
				Process tmproc = new Process();
				ProcessStartInfo psi = new ProcessStartInfo()
				{
					FileName = name,
				};
				tmproc.StartInfo = psi;
				tmproc.Start();
				// Test if it was started successfully
				// True if the process runs for more than 10 ms or the exit code is 0
				return !tmproc.WaitForExit(10) || tmproc.ExitCode == 0;
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Error, "{0} couldn't be run/found ({1})", filePath, ex);
				return false;
			}
		}

		public static string GetFilePath(FilePath filePath)
		{
			if (filePathDict.ContainsKey(filePath))
				return filePathDict[filePath];
			return null;
		}
	}

	internal class AsyncLazy<T>
	{
		protected T result;
		protected Func<Task<T>> lazyMethod;
		public bool Evaluated { get; protected set; }

		private AsyncLazy(Func<Task<T>> method)
		{
			lazyMethod = method;
		}

		public static AsyncLazy<T> CreateAsyncLazy(Func<Task<T>> method)
		{
			return new AsyncLazy<T>(method);
		}

		public static AsyncLazy<T> CreateAsyncLazy<TIn1>(Func<TIn1, Task<T>> method, TIn1 param1)
		{
			return new AsyncLazy<T>(() => method(param1));
		}

		public async Task<T> GetValue()
		{
			if (Evaluated)
			{
				return result;
			}
			else
			{
				result = await lazyMethod();
				Evaluated = true;
				return result;
			}
		}
	}

	public enum FilePath
	{
		VLC,
		StartTsBot,
		ConfigFile,
		HistoryFile,
	}
}
