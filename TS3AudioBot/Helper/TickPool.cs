namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	static class TickPool
	{
		private static Thread tickThread;
		private static int minTick = int.MaxValue;
		private static List<TickWorker> workList;
		private static bool run = false;

		static TickPool()
		{
			workList = new List<TickWorker>();
			tickThread = new Thread(Tick);
			tickThread.Name = "TickPool";
		}

		public static TickWorker RegisterTick(Action method, int interval, bool active)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (interval <= 0) throw new ArgumentException("The parameter must be at least '1'", nameof(interval));
			var worker = new TickWorker(method, interval);
			worker.Active = active;
			lock (workList)
			{
				workList.Add(worker);
				minTick = workList.Min(w => w.Interval);
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
					minTick = workList.Min(w => w.Interval);
				else
					minTick = 100;
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
						worker.IntervalRemain -= minTick;
						if (worker.IntervalRemain <= 0)
						{
							worker.IntervalRemain = worker.Interval;
							worker.Method.Invoke();
						}
					}
				}

				Thread.Sleep(minTick);
			}
		}

		public static void Close()
		{
			run = false;
		}
	}

	class TickWorker
	{
		public Action Method { get; }
		public int Interval { get; }
		public int IntervalRemain { get; set; }
		public bool Active { get; set; }

		public TickWorker(Action method, int interval) { Method = method; Interval = interval; }
	}
}
