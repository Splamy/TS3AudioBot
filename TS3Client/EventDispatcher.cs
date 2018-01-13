// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;
	using Full;

	using EvloopType = System.Action<object>;

	internal static class EventDispatcherHelper
	{
		public const string DispatcherTitle = "TS3 Dispatcher";
		public const string EventLoopTitle = "TS3 MessageLoop";

		public static IEventDispatcher Create(EventDispatchType dispatcherType)
		{
			IEventDispatcher dispatcher;
			switch (dispatcherType)
			{
			case EventDispatchType.None: dispatcher = new NoEventDispatcher(); break;
			case EventDispatchType.CurrentThread: dispatcher = new CurrentThreadEventDisptcher(); break;
			case EventDispatchType.ExtraDispatchThread: dispatcher = new ExtraThreadEventDispatcher(); break;
			case EventDispatchType.DoubleThread: dispatcher = new DoubleThreadEventDispatcher(); break;
			case EventDispatchType.AutoThreadPooled: dispatcher = new AutoThreadPooledEventDispatcher(); break;
			case EventDispatchType.NewThreadEach: dispatcher = new NewThreadEachEventDispatcher(); break;
			default: throw new NotSupportedException();
			}
			return dispatcher;
		}
	}

	/// <summary> Provides a function to run a receiving loop and asynchronously
	/// dispatch notifications.
	/// </summary>
	internal interface IEventDispatcher : IDisposable
	{
		/// <summary>Initializes the dispatcher.</summary>
		/// <param name="eventLoop">The main loop which will be receiving packets.</param>
		/// <param name="dispatcher">The method to call asynchronously when a new
		/// notification comes in.</param>
		void Init(EvloopType eventLoop, Action<LazyNotification> dispatcher, ConnectionContext ctx);
		/// <summary>Dispatches the notification.</summary>
		/// <param name="lazyNotification"></param>
		void Invoke(LazyNotification lazyNotification);
		/// <summary>Starts the eventLoop synchronously or asynchronous,
		/// depending on the dispatcher type.
		/// </summary>
		void EnterEventLoop();
		void DoWork();
	}

	internal sealed class CurrentThreadEventDisptcher : IEventDispatcher
	{
		private EvloopType eventLoop;
		private ConnectionContext ctx;
		private Action<LazyNotification> dispatcher;

		public void Init(EvloopType eventLoop, Action<LazyNotification> dispatcher, ConnectionContext ctx)
		{
			this.eventLoop = eventLoop;
			this.dispatcher = dispatcher;
			this.ctx = ctx;
		}
		public void EnterEventLoop() => eventLoop(ctx);
		public void Invoke(LazyNotification lazyNotification) => dispatcher.Invoke(lazyNotification);
		public void DoWork() { }
		public void Dispose() { }
	}

	internal sealed class ExtraThreadEventDispatcher : IEventDispatcher
	{
		private EvloopType eventLoop;
		private ConnectionContext ctx;
		private Action<LazyNotification> dispatcher;
		private Thread dispatchThread;
		private readonly ConcurrentQueue<LazyNotification> eventQueue = new ConcurrentQueue<LazyNotification>();
		private readonly AutoResetEvent eventBlock = new AutoResetEvent(false);
		private volatile bool run;

		public void Init(EvloopType eventLoop, Action<LazyNotification> dispatcher, ConnectionContext ctx)
		{
			run = true;
			this.eventLoop = eventLoop;
			this.dispatcher = dispatcher;
			this.ctx = ctx;
		}

		public void Invoke(LazyNotification lazyNotification)
		{
			eventQueue.Enqueue(lazyNotification);
			eventBlock.Set();
		}

		public void EnterEventLoop()
		{
			dispatchThread = new Thread(DispatchLoop) { Name = EventDispatcherHelper.DispatcherTitle };
			dispatchThread.Start();

			eventLoop(ctx);
		}

		private void DispatchLoop()
		{
			while (run)
			{
				eventBlock.WaitOne();
				while (!eventQueue.IsEmpty)
				{
					if (eventQueue.TryDequeue(out var lazyNotification))
						dispatcher.Invoke(lazyNotification);
				}
			}
		}

		public void DoWork()
		{
			if (Thread.CurrentThread.ManagedThreadId != dispatchThread.ManagedThreadId)
				return;
			if (eventQueue.TryDequeue(out var lazyNotification))
				dispatcher.Invoke(lazyNotification);
		}

		public void Dispose()
		{
			run = false;
			eventBlock.Set();
		}
	}

	internal sealed class DoubleThreadEventDispatcher : IEventDispatcher
	{
		private EvloopType eventLoop;
		private ConnectionContext ctx;
		private Action<LazyNotification> dispatcher;
		private Thread eventLoopThread;
		private Thread dispatchThread;
		private readonly ConcurrentQueue<LazyNotification> eventQueue = new ConcurrentQueue<LazyNotification>();
		private readonly AutoResetEvent eventBlock = new AutoResetEvent(false);
		private volatile bool run;

		public void Init(EvloopType eventLoop, Action<LazyNotification> dispatcher, ConnectionContext ctx)
		{
			run = true;
			this.eventLoop = eventLoop;
			this.dispatcher = dispatcher;
			this.ctx = ctx;
		}

		public void Invoke(LazyNotification lazyNotification)
		{
			eventQueue.Enqueue(lazyNotification);
			eventBlock.Set();
		}

		public void EnterEventLoop()
		{
			dispatchThread = new Thread(DispatchLoop) { Name = EventDispatcherHelper.DispatcherTitle };
			dispatchThread.Start();
			eventLoopThread = new Thread(() => eventLoop.Invoke(ctx)) { Name = EventDispatcherHelper.EventLoopTitle };
			eventLoopThread.Start();
		}

		private void DispatchLoop()
		{
			while (run)
			{
				eventBlock.WaitOne();
				while (!eventQueue.IsEmpty)
				{
					if (eventQueue.TryDequeue(out var lazyNotification))
						dispatcher.Invoke(lazyNotification);
				}
			}
		}

		public void DoWork()
		{
			if (Thread.CurrentThread.ManagedThreadId != dispatchThread.ManagedThreadId)
				return;
			if (eventQueue.TryDequeue(out var lazyNotification))
				dispatcher.Invoke(lazyNotification);
		}

		public void Dispose()
		{
			run = false;
			eventBlock.Set();
		}
	}

	internal sealed class NoEventDispatcher : IEventDispatcher
	{
		public void Init(EvloopType eventLoop, Action<LazyNotification> dispatcher, ConnectionContext ctx) { }
		public void EnterEventLoop() { }
		public void Invoke(LazyNotification lazyNotification) { }
		public void DoWork() { }
		public void Dispose() { }
	}

	internal sealed class AutoThreadPooledEventDispatcher : IEventDispatcher
	{
		private ConnectionContext ctx;
		private EvloopType eventLoop;
		private Action<LazyNotification> dispatcher;
		private Thread eventLoopThread;

		public void Init(EvloopType eventLoop, Action<LazyNotification> dispatcher, ConnectionContext ctx)
		{
			this.eventLoop = eventLoop;
			this.dispatcher = dispatcher;
			this.ctx = ctx;
		}

		public void EnterEventLoop()
		{
			eventLoopThread = new Thread(() => eventLoop.Invoke(ctx)) { Name = EventDispatcherHelper.EventLoopTitle };
			eventLoopThread.Start();
		}
		public void Invoke(LazyNotification lazyNotification) => ThreadPool.QueueUserWorkItem(Call, lazyNotification);
		private void Call(object obj) => dispatcher.Invoke((LazyNotification)obj);
		public void DoWork() { }
		public void Dispose() { }
	}

	internal sealed class NewThreadEachEventDispatcher : IEventDispatcher
	{
		private ConnectionContext ctx;
		private EvloopType eventLoop;
		private Action<LazyNotification> dispatcher;
		private Thread eventLoopThread;

		public void Init(EvloopType eventLoop, Action<LazyNotification> dispatcher, ConnectionContext ctx)
		{
			this.eventLoop = eventLoop;
			this.dispatcher = dispatcher;
			this.ctx = ctx;
		}

		public void EnterEventLoop()
		{
			eventLoopThread = new Thread(() => eventLoop.Invoke(ctx)) { Name = EventDispatcherHelper.EventLoopTitle };
			eventLoopThread.Start();
		}

		public void Invoke(LazyNotification lazyNotification)
		{
			eventLoopThread = new Thread(() => dispatcher.Invoke(lazyNotification)) { Name = "TS3 MessageDispatcher" };
			eventLoopThread.Start();
		}

		public void DoWork() { }
		public void Dispose() { }
	}

	public enum EventDispatchType
	{
		/// <summary>
		/// All events will be dropped.
		/// </summary>
		None,
		/// <summary>
		/// Will use the same thread that entered the <see cref="Ts3BaseFunctions.Connect"/>
		/// for receiving and invoking all events. This method is not recommended since it mostly
		/// only produces deadlocks. (Usually only for debugging)
		/// </summary>
		CurrentThread,
		/// <summary>
		/// Will use the thread that entered the <see cref="Ts3BaseFunctions.Connect"/> for
		/// receiving and starts a second thread for invoking all events. This is the best method for
		/// lightweight dipatching with no parallelization.
		/// </summary>
		ExtraDispatchThread,
		/// <summary>
		/// Will start one thread for receiving and a second thread for invoking all events.
		/// This is the best method for lightweight asynchronous dispatching with no parallelization.
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
