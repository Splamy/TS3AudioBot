// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TSLib.Helper;

namespace TSLib.Scheduler
{
	public sealed class DedicatedTaskScheduler : TaskScheduler, IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly TimeSpan CombineTimerThreshold = TimeSpan.FromMilliseconds(10);
		private readonly TaskFactory factory;
		private readonly BlockingCollection<Task> queue = new BlockingCollection<Task>();
		private readonly HashSet<TickWorker> timers = new HashSet<TickWorker>();
		private readonly Thread thread;
		private readonly Id id;
		// This is not a duraration but an instant from ticks
		// The TimeSpan class is used for calculation convenience
		// The values is used as a cache so we don't need to recheck the list on each loop
		private TimeSpan nextTimerDue = TimeSpan.MaxValue;
		private bool IsOwnThread => Thread.CurrentThread == thread;
#if DEBUG
		private Stack<Task> taskStack = new Stack<Task>();
		private TimeSpan lastTaskCompleted = TimeSpan.MinValue;
		private Stopwatch overallWatch = new Stopwatch();
		private TimeSpan actualRunningTime;
#endif

		public DedicatedTaskScheduler(Id id)
		{
			factory = new TaskFactory(this);
			this.id = id;
			thread = new Thread(DoWork)
			{
				Name = DispatcherHelper.CreateDispatcherTitle(id)
			};
			thread.Start();
		}

		private void DoWork()
		{
			Tools.SetLogId(id);
			while (!queue.IsCompleted)
			{
				TimeSpan timeTillNextTimer = DispatchTimers();

				if (queue.TryTake(out var task, timeTillNextTimer))
				{
					if (task != Task.CompletedTask)
					{
						TryExecuteTaskInternal(task, false);
					}
				}
			}
			Log.Debug("Finalizing TaskScheduler");
			queue.Dispose();
			Log.Debug("TaskScheduler closed");
		}

		private TimeSpan DispatchTimers()
		{
			var now = GetTimestamp();
			if (timers.Count == 0)
			{
				Log.ConditionalTrace("Quick return 1");
				nextTimerDue = TimeSpan.MaxValue;
				return Timeout.InfiniteTimeSpan;
			}
			// [Huristic]
			// Problem: We don't want to recheck the list each time a new Task
			// is processed from the queue.
			// Idea: On very high load, when the queue is permanently full and
			//       the current cache is in 'inf' cache state starting timers
			//       will only add more load which won't be processed anyway.
			//       So we wait for that the queue is empty then we recheck.
			//       When we are not in 'inf' state we will just check like normal.
			if (queue.Count > 0 && now + CombineTimerThreshold < nextTimerDue && nextTimerDue != TimeSpan.MaxValue)
			{
				Log.ConditionalTrace("Quick return 2");
				return nextTimerDue - now;
			}

			Log.ConditionalTrace("Recalc");

			var timeTillNextTimer = TimeSpan.MaxValue;
			foreach (var timer in timers) // TODO might be modified
			{
				var due = timer.Timestamp + timer.Interval;
				TimeSpan wait;
				if (due < now)
				{
					this.Invoke(timer.Method);
					timer.Timestamp = GetTimestamp();
					wait = timer.Interval;
				}
				else
				{
					wait = due - now;
				}

				if (wait < timeTillNextTimer)
				{
					timeTillNextTimer = wait;
				}
			}

			Trace.Assert(timeTillNextTimer != TimeSpan.MaxValue);

			timeTillNextTimer = timeTillNextTimer.Max(CombineTimerThreshold);
			nextTimerDue = now + timeTillNextTimer;
			return timeTillNextTimer;
		}

		private bool TryExecuteTaskInternal(Task task, bool inline)
		{
#if DEBUG
			LogExecuteEnter(task, inline);
#endif
			bool ok = TryExecuteTask(task);
#if DEBUG
			LogExecuteExit(task);
#endif
			return ok;
		}

#if DEBUG
		private void LogExecuteEnter(Task task, bool inline)
		{
			if (taskStack.Count == 0)
			{
				overallWatch.Restart();
				actualRunningTime = TimeSpan.Zero;
				lastTaskCompleted = GetTimestamp();
			}

			Log.Trace("Processing Task {0} {1}", task.Id, inline ? "inline" : "from queue");
			taskStack.Push(task);
		}

		private void LogExecuteExit(Task task)
		{
			var now = GetTimestamp();
			var calcTime = (now - lastTaskCompleted);
			actualRunningTime += calcTime;
			Log.Trace("Task {0} took {1:F3}ms. Resulted {2}", task.Id, calcTime.TotalMilliseconds, task.Status);
			lastTaskCompleted = now;

			Trace.Assert(task == taskStack.Pop());
			if (taskStack.Count == 0)
			{
				var overallTime = overallWatch.Elapsed;
				Log.Trace("Overall call time: {0:F3} Task Time: {1:F3} Overhead: {2:F3}",
					overallTime.TotalMilliseconds,
					actualRunningTime.TotalMilliseconds,
					(overallTime - actualRunningTime).TotalMilliseconds);
				if (queue.Count == 0)
					Log.Trace("Eoq");
			}
		}
#endif

		protected override IEnumerable<Task>? GetScheduledTasks() => queue.ToArray();

		protected override void QueueTask(Task task)
			=> QueueTaskInternal(task);

		private void QueueTaskInternal(Task task)
		{
			if (task is null) throw new ArgumentNullException(nameof(task));

			try
			{
				queue.Add(task);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Dropping Task");
			}
		}

		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
			=> IsOwnThread && TryExecuteTaskInternal(task, true);

		public TickWorker CreateTimer(Action method, TimeSpan interval, bool active)
		{
			CheckOwnThread();
			if (method is null) throw new ArgumentNullException(nameof(method));
			var worker = new TickWorker(this, method, interval);
			// This will add the worker to the list (if it's enabled)
			worker.Active = active;
			return worker;
		}

		internal void EnableTimer(TickWorker timer)
		{
			CheckOwnThread();
			timers.Add(timer);
			BumpTimer();
		}

		internal void DisableTimer(TickWorker timer)
		{
			CheckOwnThread();
			timers.Remove(timer);
		}

		internal void BumpTimer() => QueueTaskInternal(Task.CompletedTask);

		private TimeSpan GetTimestamp() => new TimeSpan(Stopwatch.GetTimestamp());

		public void CheckOwnThread()
		{
			if (!IsOwnThread)
				throw new TaskSchedulerException("Cannot register a timer from an outside thread");
		}

		// Invokes

		public Task Invoke(Action action)
		{
			if (TaskScheduler.Current == this)
			{
				action();
				return Task.CompletedTask;
			}

			return factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this);
		}

		public Task<T> Invoke<T>(Func<T> action)
		{
			if (TaskScheduler.Current == this)
			{
				var t = action();
				return Task.FromResult(t);
			}

			return factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this);
		}

		public Task InvokeAsync(Func<Task> action)
		{
			if (TaskScheduler.Current == this)
			{
				var t = action();
				return t;
			}

			return factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this).Unwrap();
		}

		public Task<T> InvokeAsync<T>(Func<Task<T>> action)
		{
			if (TaskScheduler.Current == this)
			{
				var t = action();
				return t;
			}

			return factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this).Unwrap();
		}

		public void Dispose()
		{
			queue.CompleteAdding();
		}
	}
}
