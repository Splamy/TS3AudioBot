namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	[Serializable]
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

		public static void RegisterTickOnce(Action method)
		{
			AddWorker(new TickWorker(method, TimeSpan.Zero) { Active = true, TickOnce = true });
		}

		public static TickWorker RegisterTick(Action method, TimeSpan interval, bool active)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (interval <= TimeSpan.Zero) throw new ArgumentException("The parameter must be at least '1'", nameof(interval));
			var worker = new TickWorker(method, interval) { Active = active };
			AddWorker(worker);
			return worker;
		}

		private static void AddWorker(TickWorker worker)
		{
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
		}

		public static void UnregisterTicker(TickWorker worker)
		{
			if (worker == null) throw new ArgumentNullException(nameof(worker));
			lock (workList) { RemoveUnlocked(worker); }
		}

		private static void RemoveUnlocked(TickWorker worker)
		{
			workList.Remove(worker);
			if (workList.Count > 0)
				curTick = workList.Min(w => w.Interval);
			else
				curTick = minTick;
		}

		private static void Tick()
		{
			while (run)
			{
				lock (workList)
				{
					for (int i = 0; i < workList.Count; i++)
					{
						var worker = workList[i];
						if (!worker.Active) continue;
						worker.IntervalRemain -= curTick;
						if (worker.IntervalRemain <= TimeSpan.Zero)
						{
							worker.IntervalRemain = worker.Interval;
							worker.Method.Invoke();
						}
						if (worker.TickOnce)
						{
							RemoveUnlocked(worker);
							i--;
						}
					}
				}

				Thread.Sleep(curTick);
			}
		}

		public static void Close()
		{
			run = false;
			Util.WaitForThreadEnd(tickThread, TimeSpan.FromMilliseconds(100));
			tickThread = null;
		}
	}

	public class TickWorker : MarshalByRefObject
	{
		public Action Method { get; }
		public TimeSpan Interval { get; }
		public TimeSpan IntervalRemain { get; set; } = TimeSpan.Zero;
		public bool Active { get; set; } = false;
		public bool TickOnce { get; set; } = false;

		public TickWorker(Action method, TimeSpan interval) { Method = method; Interval = interval; }
	}
}
