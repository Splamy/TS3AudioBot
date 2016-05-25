// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

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

	public class TickWorker : MarshalByRefObject
	{
		public Action Method { get; }
		public TimeSpan Interval { get; }
		public TimeSpan IntervalRemain { get; set; }
		public bool Active { get; set; }

		public TickWorker(Action method, TimeSpan interval) { Method = method; Interval = interval; }
	}
}
