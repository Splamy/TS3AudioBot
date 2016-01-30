namespace TS3AudioBot.Helper
{
	using System;
	using System.Threading;

	public sealed class WaitEventBlock<T> : IDisposable
	{
		private T response;
		private AutoResetEvent blocker;
		private DateTime timeOutPoint;
		private bool timedOut;
		private TickWorker timeOutTicker;
		private static readonly TimeSpan tickSpan = TimeSpan.FromMilliseconds(100);

		public WaitEventBlock()
		{
			blocker = new AutoResetEvent(false);
			timeOutTicker = TickPool.RegisterTick(RunTimeout, tickSpan, false);
		}

		/// <summary>Will block the current thread and wait until notified with <see cref="WaitEventBlock{T}.Notify(T)"/>.</summary>
		/// <returns>The received object from the notification.</returns>
		public T Wait() => Wait(Timeout.InfiniteTimeSpan);

		/// <summary>Will block the current thread and wait until notified with <see cref="WaitEventBlock{T}.Notify(T)"/>
		/// or throws when the timeout ran out.</summary>
		/// <returns>The received object from the notification.</returns>
		/// <exception cref="TimeoutException"></exception>
		public T Wait(TimeSpan timeout)
		{
			if (timeout != Timeout.InfiniteTimeSpan)
			{
				timedOut = false;
				timeOutPoint = Util.GetNow().Add(timeout);
				timeOutTicker.Active = true;
			}

			blocker.WaitOne();
			timeOutTicker.Active = false;
			if (timedOut)
				throw new TimeoutException();
			else
				return response;
		}

		public void Notify(T data)
		{
			response = data;
			blocker.Set();
		}

		private void RunTimeout()
		{
			if (Util.GetNow() >= timeOutPoint)
			{
				timeOutTicker.Active = false;
				timedOut = true;
				blocker.Set();
			}
		}

		public void Dispose()
		{
			if (blocker != null)
			{
				blocker.Set();
				blocker.Dispose();
				blocker = null;
			}
		}
	}
}
