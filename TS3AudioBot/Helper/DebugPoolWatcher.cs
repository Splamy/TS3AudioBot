using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;

namespace TS3AudioBot.Helper
{
	static class DebugPoolWatcher
	{
		static Assembly mscorlib = typeof(string).Assembly;
		static TypeInfo ThreadPoolGlobalsType = mscorlib.DefinedTypes.FirstOrDefault(t => t.Name == "ThreadPoolGlobals");
		static FieldInfo workQueueField = ThreadPoolGlobalsType.GetField("workQueue");
		static object workQueue = workQueueField.GetValue(null);
		static Type workQueueType = workQueue.GetType();
		static FieldInfo queueTailField = workQueueType.GetField("queueTail", BindingFlags.NonPublic | BindingFlags.Instance);

		static Type ThreadPoolType = typeof(ThreadPool);
		static MethodInfo met = ThreadPoolType.GetMethod("EnumerateQueuedWorkItems", BindingFlags.NonPublic | BindingFlags.Static);

		public static void Check()
		{
			var queueTail = queueTailField.GetValue(workQueue);
			var queueData = met.Invoke(null, new object[] { null, queueTail });
		}
	}
}
