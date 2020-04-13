using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TSLib.Helper;

namespace TSLib
{
	internal static class DispatcherHelper
	{
		public const string DispatcherTitle = "TS Dispatcher";

		internal static string CreateLogThreadName(string threadName, Id id) => threadName + (id == Id.Null ? "" : $"[{id}]");

		internal static string CreateDispatcherTitle(Id id) => CreateLogThreadName(DispatcherTitle, id);
	}

	public sealed class DedicatedTaskScheduler : TaskScheduler, IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly BlockingCollection<Task> queue = new BlockingCollection<Task>();
		private readonly Thread thread;
		private readonly Id id;

		public DedicatedTaskScheduler(Id id)
		{
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
			foreach (var task in queue.GetConsumingEnumerable())
			{
#if DEBUG
				var sw = new System.Diagnostics.Stopwatch();
				Log.Debug("Processing Task {0}", task.Id);
#endif
				TryExecuteTask(task);
#if DEBUG
				var time = sw.Elapsed;
				Log.Debug("Task {0} took {1}. Resulted {2}", task.Id, time, task.Status);
				if (queue.Count == 0)
					Log.Debug("Eoq");
#endif
			}
			Log.Debug("Finalizing TaskScheduler");
			queue.Dispose();
			Log.Info("TaskScheduler closed");
		}

		protected override IEnumerable<Task>? GetScheduledTasks() => queue.ToArray();

		protected override void QueueTask(Task task)
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
			=> Thread.CurrentThread == thread && TryExecuteTask(task);

		public void Dispose()
		{
			queue.CompleteAdding();
		}
	}

	public static class TaskSchedulerExtensions
	{
		public static Task Invoke(this TaskScheduler scheduler, Action action)
		{
			if (TaskScheduler.Current == scheduler)
			{
				action();
				return Task.CompletedTask;
			}

			return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, scheduler);
		}

		public static Task<T> Invoke<T>(this TaskScheduler scheduler, Func<T> action)
		{
			if (TaskScheduler.Current == scheduler)
			{
				var t = action();
				return Task.FromResult(t);
			}

			return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, scheduler);
		}

		public static Task InvokeAsync(this TaskScheduler scheduler, Func<Task> action)
		{
			if (TaskScheduler.Current == scheduler)
			{
				var t = action();
				return t;
			}

			return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, scheduler).Unwrap();
		}

		public static Task<T> InvokeAsync<T>(this TaskScheduler scheduler, Func<Task<T>> action)
		{
			if (TaskScheduler.Current == scheduler)
			{
				var t = action();
				return t;
			}

			return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, scheduler).Unwrap();
		}
	}
}
