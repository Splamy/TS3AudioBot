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
	using System.Threading;

	public sealed class WaitEventBlock<T> : MarshalByRefObject, IDisposable
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
