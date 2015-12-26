using System;
using System.Threading.Tasks;

namespace TS3AudioBot.Helper
{
	static class Sync
	{
		private static readonly object lockObj = new object();

		public static void Run(Func<Task> work)
		{
			lock (lockObj)
			{
				Func<Task, Task> deleg = async (t) => await t;
				var ar = deleg.BeginInvoke(work(), null, null);
				ar.AsyncWaitHandle.WaitOne();
			}
		}

		public static T Run<T>(Func<Task<T>> work)
		{
			lock (lockObj)
			{
				T result = default(T);
				Action<Task<T>> deleg = async (t) => result = await t;
				var ar = deleg.BeginInvoke(work(), null, null);
				ar.AsyncWaitHandle.WaitOne();
				return result;
			}
		}
	}
}