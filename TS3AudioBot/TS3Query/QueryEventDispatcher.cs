namespace TS3Query
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;

	interface IEventDispatcher : IDisposable
	{
		EventDispatchType DispatcherType { get; }
		void Init(Action eventLoop);
		/// <summary>Do NOT call this method manually (Unless you know what you do).
		/// Invokes an Action, when the EventLoop receives a new packet.</summary>
		/// <param name="eventAction"></param>
		void Invoke(Action eventAction);
		void EnterEventLoop();
	}

	class CurrentThreadEventDisptcher : IEventDispatcher
	{
		private Action eventLoop;
		public EventDispatchType DispatcherType => EventDispatchType.CurrentThread;

		public void Init(Action eventLoop) => this.eventLoop = eventLoop;
		public void EnterEventLoop() => eventLoop();
		public void Invoke(Action eventAction) => eventAction();
		public void Dispose() { }
	}

	class DoubleThreadEventDispatcher : IEventDispatcher
	{
		public EventDispatchType DispatcherType => EventDispatchType.DoubleThread;

		private Thread readQueryThread;
		private ConcurrentQueue<Action> eventQueue = new ConcurrentQueue<Action>();
		private AutoResetEvent eventBlock = new AutoResetEvent(false);
		private bool run = true;

		public DoubleThreadEventDispatcher() { }

		public void Init(Action eventLoop)
		{
			readQueryThread = new Thread(eventLoop.Invoke);
			readQueryThread.Name = "TS3Query MessageLoop";
			readQueryThread.Start();
		}

		public void Invoke(Action eventAction)
		{
			eventQueue.Enqueue(eventAction);
			eventBlock.Set();
		}

		public void EnterEventLoop()
		{
			while (run && eventBlock != null)
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
			// TODO: replace with thread close util call from webdev branch
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

			run = false;
			if (eventBlock != null)
			{
				eventBlock.Set();
				eventBlock.Dispose();
				eventBlock = null;
			}
		}
	}

	class NoEventDispatcher : IEventDispatcher
	{
		public EventDispatchType DispatcherType => EventDispatchType.None;
		public void Init(Action eventLoop) { }
		public void EnterEventLoop() { }
		public void Invoke(Action eventAction) { }
		public void Dispose() { }
	}

	enum EventDispatchType
	{
		None,
		CurrentThread,
		DoubleThread,
		AutoThreadPooled,
		NewThreadEach,
	}
}
