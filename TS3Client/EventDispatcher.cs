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

namespace TS3Client
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;

	public interface IEventDispatcher : IDisposable
	{
		void Init(Action eventLoop);
		/// <summary>Do NOT call this method manually (Unless you know what you do).
		/// Invokes an Action, when the EventLoop receives a new packet.</summary>
		/// <param name="eventAction"></param>
		void Invoke(Action eventAction);
		void EnterEventLoop();
	}

	internal class CurrentThreadEventDisptcher : IEventDispatcher
	{
		private Action eventLoop;

		public void Init(Action eventLoop) => this.eventLoop = eventLoop;
		public void EnterEventLoop() => eventLoop();
		public void Invoke(Action eventAction) => eventAction();
		public void Dispose() { }
	}

	internal class ExtraThreadEventDispatcher : IEventDispatcher
	{
		private Thread readQueryThread;
		private readonly ConcurrentQueue<Action> eventQueue = new ConcurrentQueue<Action>();
		private readonly AutoResetEvent eventBlock = new AutoResetEvent(false);
		private volatile bool run = true;

		public void Init(Action eventLoop)
		{
			readQueryThread = new Thread(eventLoop.Invoke) { Name = "TS3Query MessageLoop" };
			readQueryThread.Start();
		}

		public void Invoke(Action eventAction)
		{
			eventQueue.Enqueue(eventAction);
			eventBlock.Set();
		}

		public void EnterEventLoop()
		{
			while (run)
			{
				eventBlock.WaitOne();
				while (!eventQueue.IsEmpty)
				{
					Action callData;
					if (eventQueue.TryDequeue(out callData))
						callData.Invoke();
				}
			}
		}

		public void Dispose()
		{
			run = false;
			eventBlock.Set();
			eventBlock.Close();
			eventBlock.Dispose();
		}
	}

	internal class NoEventDispatcher : IEventDispatcher
	{
		public void Init(Action eventLoop) { }
		public void EnterEventLoop() { }
		public void Invoke(Action eventAction) { }
		public void Dispose() { }
	}

	internal class AutoThreadPooledEventDispatcher : IEventDispatcher
	{
		private Thread readQueryThread;

		public void Init(Action eventLoop)
		{
			readQueryThread = new Thread(eventLoop.Invoke) { Name = "TS3Query MessageLoop" };
			readQueryThread.Start();
		}
		public void EnterEventLoop() { }
		public void Invoke(Action eventAction) => ThreadPool.QueueUserWorkItem(Call, eventAction);
		private static void Call(object obj) => ((Action)obj)();
		public void Dispose() { }
	}

	// TODO change used method when mving evdisp start to connect
	public enum EventDispatchType
	{
		/// <summary>
		/// All events will be dropped.
		/// </summary>
		None,
		/// <summary>
		/// Will use the same thread that entered the <see cref="Ts3BaseClient.EnterEventLoop"/>
		/// for receiving and invoking all events. This method is not recommended since it mostly
		/// only produces deadlocks. (Usually only for debugging)
		/// </summary>
		CurrentThread,
		/// <summary>
		/// Will use the thread that entered the <see cref="Ts3BaseClient.EnterEventLoop"/> for
		/// receiving and starts a second thread for invoking all events. This is the best method for
		/// lightweight dipatching with no parallelization.
		/// </summary>
		ExtraDispatchThread,
		/// <summary>
		/// Will start one thread for receiving and a second thread for invoking all events.
		/// This is the best method for lightweight asynchronous dipatching with no parallelization.
		/// </summary>
		DoubleThread,
		/// <summary>
		/// This method will use the <see cref="ThreadPool"/> from .NET to dispatch all events.
		/// This is the best method for high parallelization with low overhead when using many instances.
		/// </summary>
		AutoThreadPooled,
		/// <summary>
		/// This method will create a new Thread for each event. This method is not recommended
		/// due to high overhead and resource consumption. Only try it when all else fails.
		/// </summary>
		NewThreadEach,
	}
}
