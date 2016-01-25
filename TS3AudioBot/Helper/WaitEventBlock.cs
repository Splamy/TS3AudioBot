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

		public T Wait() => Wait(Timeout.InfiniteTimeSpan);

		public T Wait(TimeSpan timeout)
		{
			if (timeout != Timeout.InfiniteTimeSpan)
			{
				timedOut = false;
				timeOutPoint = DateTime.Now.Add(timeout);
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
			if (DateTime.Now >= timeOutPoint)
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

	[Serializable]
	public class TimeoutException : Exception
	{
		public TimeoutException() { }
		public TimeoutException(string message) : base(message) { }
		public TimeoutException(string message, Exception inner) : base(message, inner) { }
	}
}
