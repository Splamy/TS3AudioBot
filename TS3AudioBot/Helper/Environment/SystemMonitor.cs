namespace TS3AudioBot.Helper.Environment
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	public class SystemMonitor
	{
		private static readonly Process CurrentProcess = Process.GetCurrentProcess();
		private readonly Queue<SystemMonitorSnapshot> history = new Queue<SystemMonitorSnapshot>();
		private TickWorker ticker = null;

		private DateTime lastSnapshotTime = DateTime.MinValue;
		private TimeSpan lastCpuTime = TimeSpan.Zero;

		public void StartTimedSnapshots()
		{
			if (ticker != null)
				throw new InvalidOperationException("Ticker already running");
			ticker = TickPool.RegisterTick(CreateSnapshot, TimeSpan.FromSeconds(1), true);
		}

		public void CreateSnapshot()
		{
			CurrentProcess.Refresh();

			var currentSnapshotTime = Util.GetNow();
			var currentCpuTime = CurrentProcess.TotalProcessorTime;

			var timeDiff = currentSnapshotTime - lastSnapshotTime;
			var cpuDiff = currentCpuTime - lastCpuTime;
			var cpu = (cpuDiff.Ticks / (float)timeDiff.Ticks);

			history.Enqueue(new SystemMonitorSnapshot
			{
				Memory = CurrentProcess.WorkingSet64,
				Cpu = cpu,
			});

			lastSnapshotTime = currentSnapshotTime;
			lastCpuTime = currentCpuTime;

			while (history.Count > 60)
				history.Dequeue();
		}

		public SystemMonitorReport GetReport()
		{
			return new SystemMonitorReport
			{
				Memory = history.Select(x => x.Memory).ToArray(),
				Cpu = history.Select(x => x.Cpu).ToArray(),
			};
		}
	}

	public class SystemMonitorReport
	{
		public long[] Memory { get; set; }
		public float[] Cpu { get; set; }
	}

	public struct SystemMonitorSnapshot
	{
		public float Cpu { get; set; }
		public long Memory { get; set; }
	}
}
