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
		private static Thread tickThread;
		private static readonly TimeSpan MinTick = TimeSpan.FromMilliseconds(1000);
		private static readonly List<TickWorker> workList;
		private static bool run;

		static TickPool()
		{
			run = false;
			Util.Init(ref workList);
			tickThread = new Thread(Tick) {Name = "TickPool"};
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
			if (worker == null) throw new ArgumentNullException(nameof(worker));
			lock (workList) { RemoveUnlocked(worker); }
		}

		private static void RemoveUnlocked(TickWorker worker)
		{
			workList.Remove(worker);
			worker.Timer.Stop();
		}

		private static void Tick()
		{
			while (run)
			{
				var curSleep = MinTick;

				lock (workList)
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
								RemoveUnlocked(worker);
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

				Thread.Sleep(curSleep);
			}
		}

		public static void Close()
		{
			run = false;
			Util.WaitForThreadEnd(tickThread, TimeSpan.FromMilliseconds(100));
			tickThread = null;
		}
	}

	public class TickWorker
	{
		public Action Method { get; }
		public TimeSpan Interval { get; }
		public Stopwatch Timer { get; set; } = new Stopwatch();
		public bool Active { get; set; } = false;
		public bool TickOnce { get; set; } = false;

		public TickWorker(Action method, TimeSpan interval) { Method = method; Interval = interval; }
	}
}
