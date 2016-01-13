using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TS3AudioBot.Helper
{
	class WaitEventBlock<T>
	{
		private T response;
		private AutoResetEvent blocker;
		private TimeSpan timeOut;
		private TimeSpan timeOutRemaining;
		private TickWorker timeOutTicker;
		private int tickSpanMs = 100;

		private static readonly TimeSpan NoTimeout = TimeSpan.MinValue;

		public WaitEventBlock()
		{
			blocker = new AutoResetEvent(false);
			timeOutTicker = TickPool.RegisterTick(RunTimeout, tickSpanMs, false);
		}

		public T Wait() => Wait(NoTimeout);

		public T Wait(TimeSpan timeout)
		{
			if (timeout != NoTimeout)
			{
				timeOut = timeout;
				timeOutRemaining = timeout;
				timeOutTicker.Active = true;
			}

			blocker.WaitOne();
			timeOutTicker.Active = false;
			return response;
		}

		public void Notify(T data)
		{
			response = data;
			blocker.Set();
		}

		private void RunTimeout()
		{
			timeOutRemaining = timeOutRemaining.Subtract(TimeSpan.FromMilliseconds(tickSpanMs));
			if (timeOutRemaining < TimeSpan.Zero)
			{
				timeOutTicker.Active = false;
				response = default(T); // alt throw timeoutexception
				blocker.Set();
			}
		}
	}
}
