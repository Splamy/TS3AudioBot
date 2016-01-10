using System;
using System.Collections.Generic;
using System.Threading;

namespace TS3AudioBot.Helper
{
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
		}

		public static TickWorker RegisterTick(Action method, int interval, bool active)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (interval <= 0) throw new ArgumentException("The parameter must be at least '1'", nameof(interval));
			var worker = new TickWorker(method, interval);
			worker.Active = active;
			workList.Add(worker);
			CheckTicker(worker);
			return worker;
		}

		private static void CheckTicker(TickWorker worker)
		{
			minTick = Math.Min(minTick, worker.Interval);
			if (!run)
			{
				run = true;
				tickThread.Start();
			}
		}

		private static void Tick()
		{
			while (run)
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
