// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;

	[Serializable]
	public static class TickPool
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static bool run;
		private static readonly Thread tickThread;
		private static readonly object tickLock = new object();
		private static readonly TimeSpan MinTick = TimeSpan.FromMilliseconds(1000);
		private static readonly List<TickWorker> workList;
		private static readonly AutoResetEvent tickLoopPulse = new AutoResetEvent(false);

		static TickPool()
		{
			run = false;
			Util.Init(out workList);
			tickThread = new Thread(Tick) { Name = "TickPool" };
		}

		public static TickWorker RegisterTickOnce(Action method, TimeSpan? delay = null)
		{
			if (method is null) throw new ArgumentNullException(nameof(method));
			if (delay.HasValue && delay.Value <= TimeSpan.Zero) throw new ArgumentException("The parameter must be at least '1'", nameof(delay));
			var worker = new TickWorker(method, delay ?? TimeSpan.Zero) { Active = true, TickOnce = true };
			AddWorker(worker);
			return worker;
		}

		public static TickWorker RegisterTick(Action method, TimeSpan interval, bool active)
		{
			if (method is null) throw new ArgumentNullException(nameof(method));
			if (interval <= TimeSpan.Zero) throw new ArgumentException("The parameter must be at least '1'", nameof(interval));
			var worker = new TickWorker(method, interval) { Active = active };
			AddWorker(worker);
			return worker;
		}

		private static void AddWorker(TickWorker worker)
		{
			lock (tickLock)
			{
				workList.Add(worker);
				worker.Timer.Start();
				if (!run)
				{
					run = true;
					tickThread.Start();
				}
			}
		}

		public static void UnregisterTicker(TickWorker worker)
		{
			if (worker is null) throw new ArgumentNullException(nameof(worker));
			lock (tickLock)
			{
				workList.Remove(worker);
				worker.Timer.Stop();
			}
		}

		private static void Tick()
		{
			while (run)
			{
				var curSleep = MinTick;

				lock (tickLock)
				{
					for (int i = 0; i < workList.Count; i++)
					{
						var worker = workList[i];
						if (!worker.Active) continue;

						var remaining = worker.Interval - worker.Timer.Elapsed;
						if (remaining <= TimeSpan.Zero)
						{
							worker.Method.Invoke();

							if (worker.TickOnce)
							{
								UnregisterTicker(worker);
								i--;
							}
							else
							{
								worker.Timer.Restart();
								remaining = worker.Interval;
							}
						}
						if (remaining < curSleep)
							curSleep = remaining;
					}
				}

				if (curSleep >= TimeSpan.Zero)
					tickLoopPulse.WaitOne(curSleep);
			}
		}

		public static void Close()
		{
			run = false;
			tickLoopPulse.Set();
			bool lockTaken = false;
			Monitor.TryEnter(workList, TimeSpan.FromSeconds(1), ref lockTaken);
			if (lockTaken)
			{
				workList.Clear();
				Monitor.Exit(workList);
			}
			else
			{
				Log.Warn("TickPool could not close correctly.");
			}
			tickLoopPulse.Set();
			Util.WaitForThreadEnd(tickThread, MinTick);
		}
	}

	public class TickWorker
	{
		public Action Method { get; }
		public TimeSpan Interval { get; set; }
		public Stopwatch Timer { get; set; } = new Stopwatch();
		public bool Active { get; set; } = false;
		public bool TickOnce { get; set; } = false;

		public TickWorker(Action method, TimeSpan interval) { Method = method; Interval = interval; }
	}
}
