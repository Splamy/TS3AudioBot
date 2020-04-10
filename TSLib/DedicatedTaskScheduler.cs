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
				Log.ConditionalDebug("Processing Task");
				TryExecuteTask(task);
			}
			Log.Debug("Finalizing TaskScheduler");
			queue.Dispose();
			Log.Info("TaskScheduler closed");
		}

		public Task Invoke(Action action)
		{
			if (Current == this)
			{
				action();
				return Task.CompletedTask;
			}

			return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this);
		}

		public Task<T> Invoke<T>(Func<T> action)
		{
			if (Current == this)
			{
				var t = action();
				return Task.FromResult(t);
			}

			return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this);
		}

		public Task InvokeAsync(Func<Task> action)
		{
			if (Current == this)
			{
				var t = action();
				return t;
			}

			return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this).Unwrap();
		}

		public Task<T> InvokeAsync<T>(Func<Task<T>> action)
		{
			if (Current == this)
			{
				var t = action();
				return t;
			}

			return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this).Unwrap();
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
}
