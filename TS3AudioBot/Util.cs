using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TS3AudioBot
{
	class Util
	{
		private static Dictionary<SubTask, string> subTaskDict;

		static Util()
		{
			subTaskDict = new Dictionary<SubTask, string>();
			subTaskDict.Add(SubTask.VLC, IsLinux ? "vlc" : @"D:\VideoLAN\VLC\vlc.exe");
			subTaskDict.Add(SubTask.StartTsBot, IsLinux ? "StartTsBot.sh" : "");
		}

		public static bool IsLinux
		{
			get
			{
				int p = (int)Environment.OSVersion.Platform;
				return (p == 4) || (p == 6) || (p == 128);
			}
		}

		public static bool Execute(SubTask subTask)
		{
			try
			{
				string name = GetSubTaskPath(subTask);
				Process tmproc = new Process();
				ProcessStartInfo psi = new ProcessStartInfo()
				{
					FileName = name,
				};
				tmproc.StartInfo = psi;
				tmproc.Start();
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error! {0} couldn't be run/found ({1})", subTask, ex);
				return false;
			}
		}

		public static string GetSubTaskPath(SubTask subTask)
		{
			if (subTaskDict.ContainsKey(subTask))
				return subTaskDict[subTask];
			return null;
		}
	}

	public enum SubTask
	{
		VLC,
		StartTsBot,
	}
}
