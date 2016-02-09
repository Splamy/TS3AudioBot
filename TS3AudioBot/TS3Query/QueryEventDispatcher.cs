namespace TS3Query
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;

	interface IEventDispatcher : IDisposable
	{
		EventDispatchType DispatcherType { get; }
		/// <summary>Do NOT call this method manually (Unless you know what you do).
		/// Invokes an Action, when the EventLoop receives a new packet.</summary>
		/// <param name="eventAction"></param>
		void Invoke(Action eventAction);
		/// <summary>Use this method to enter the read loop with the current Thread.</summary>
		void EnterEventLoop();
	}

	class ManualEventDispatcher : IEventDispatcher
	{
		public EventDispatchType DispatcherType => EventDispatchType.Manual;

		private ConcurrentQueue<Action> eventQueue = new ConcurrentQueue<Action>();
		private AutoResetEvent eventBlock = new AutoResetEvent(false);
		private bool run = true;

		public ManualEventDispatcher() { }

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
		public void EnterEventLoop() { throw new NotSupportedException(); }
		public void Invoke(Action eventAction) { }
		public void Dispose() { }
	}

	enum EventDispatchType
	{
		None,
		CurrentThread,
		Manual,
		AutoThreadPooled,
		NewThreadEach,
	}
}
