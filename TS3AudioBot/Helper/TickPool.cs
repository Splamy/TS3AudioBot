namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	public static class TickPool
	{
		private static Thread tickThread;
		private static TimeSpan curTick;
		private static readonly TimeSpan minTick;
		private static List<TickWorker> workList;
		private static bool run;

		static TickPool()
		{
			run = false;
			minTick = TimeSpan.FromMilliseconds(100);
			curTick = TimeSpan.MaxValue;
			workList = new List<TickWorker>();
			tickThread = new Thread(Tick);
			tickThread.Name = "TickPool";
		}

		public static TickWorker RegisterTick(Action method, TimeSpan interval, bool active)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (interval <= TimeSpan.Zero) throw new ArgumentException("The parameter must be at least '1'", nameof(interval));
			var worker = new TickWorker(method, interval);
			worker.Active = active;
			lock (workList)
			{
				workList.Add(worker);
				curTick = workList.Min(w => w.Interval);
				if (!run)
				{
					run = true;
					tickThread.Start();
				}
			}
			return worker;
		}

		public static void UnregisterTicker(TickWorker worker)
		{
			if (worker == null) throw new ArgumentNullException(nameof(worker));
			lock (workList)
			{
				workList.Remove(worker);
				if (workList.Count > 0)
					curTick = workList.Min(w => w.Interval);
				else
					curTick = minTick;
			}
		}

		private static void Tick()
		{
			while (run)
			{
				lock (workList)
				{
					foreach (var worker in workList)
					{
						if (!worker.Active) continue;
						worker.IntervalRemain -= curTick;
						if (worker.IntervalRemain <= TimeSpan.Zero)
						{
							worker.IntervalRemain = worker.Interval;
							worker.Method.Invoke();
						}
					}
				}

				Thread.Sleep(curTick);
			}
		}

		public static void Close()
		{
			run = false;
		}
	}

	public class TickWorker
	{
		public Action Method { get; }
		public TimeSpan Interval { get; }
		public TimeSpan IntervalRemain { get; set; }
		public bool Active { get; set; }

		public TickWorker(Action method, TimeSpan interval) { Method = method; Interval = interval; }
	}
}
