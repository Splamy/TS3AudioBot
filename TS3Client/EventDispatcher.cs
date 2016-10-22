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
		EventDispatchType DispatcherType { get; }
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
		public EventDispatchType DispatcherType => EventDispatchType.CurrentThread;

		public void Init(Action eventLoop) => this.eventLoop = eventLoop;
		public void EnterEventLoop() => eventLoop();
		public void Invoke(Action eventAction) => eventAction();
		public void Dispose() { }
	}

	internal class DoubleThreadEventDispatcher : IEventDispatcher
	{
		public EventDispatchType DispatcherType => EventDispatchType.DoubleThread;

		private Thread readQueryThread;
		private readonly ConcurrentQueue<Action> eventQueue = new ConcurrentQueue<Action>();
		private readonly AutoResetEvent eventBlock = new AutoResetEvent(false);
		private bool run = true;

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

			if (readQueryThread != null)
			{
				for (int i = 0; i < 100 && readQueryThread.IsAlive; i++)
					Thread.Sleep(1);
				if (readQueryThread.IsAlive)
				{
					readQueryThread.Abort();
					readQueryThread = null;
				}
			}

			eventBlock.Dispose();
		}
	}

	internal class NoEventDispatcher : IEventDispatcher
	{
		public EventDispatchType DispatcherType => EventDispatchType.None;
		public void Init(Action eventLoop) { }
		public void EnterEventLoop() { }
		public void Invoke(Action eventAction) { }
		public void Dispose() { }
	}

	public enum EventDispatchType
	{
		None,
		CurrentThread,
		DoubleThread,
		AutoThreadPooled,
		NewThreadEach,
	}
}
