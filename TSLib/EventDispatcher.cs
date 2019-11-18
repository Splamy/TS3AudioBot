// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Concurrent;
using System.Threading;
using TSLib.Helper;

namespace TSLib
{
	internal static class EventDispatcherHelper
	{
		public const string DispatcherTitle = "TS Dispatcher";
		public const string EventLoopTitle = "TS MessageLoop";

		internal static string CreateLogThreadName(string threadName, Id id) => threadName + (id == Id.Null ? "" : $"[{id}]");

		internal static string CreateDispatcherTitle(Id id) => CreateLogThreadName(DispatcherTitle, id);
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
		/// <param name="ctx">The current connection context.</param>
		void Init(Action<LazyNotification> dispatcher, Id id);
		/// <summary>Dispatches the notification.</summary>
		/// <param name="lazyNotification"></param>
		void Invoke(LazyNotification lazyNotification);
		void DoWork();
	}

	internal sealed class ExtraThreadEventDispatcher : IEventDispatcher
	{
		private Action<LazyNotification> dispatcher;
		private Thread dispatchThread;
		private readonly ConcurrentQueue<LazyNotification> eventQueue = new ConcurrentQueue<LazyNotification>();
		private readonly AutoResetEvent eventBlock = new AutoResetEvent(false);
		private volatile bool run;

		public void Init(Action<LazyNotification> dispatcher, Id id)
		{
			run = true;
			this.dispatcher = dispatcher;

			dispatchThread = new Thread(() =>
			{
				Tools.SetLogId(id);
				DispatchLoop();
			})
			{ Name = EventDispatcherHelper.CreateDispatcherTitle(id) };
			dispatchThread.Start();
		}

		public void Invoke(LazyNotification lazyNotification)
		{
			eventQueue.Enqueue(lazyNotification);
			eventBlock.Set();
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
		public void Init(Action<LazyNotification> dispatcher, Id id) { }
		public void Invoke(LazyNotification lazyNotification) { }
		public void DoWork() { }
		public void Dispose() { }
	}

	internal sealed class AutoThreadPooledEventDispatcher : IEventDispatcher
	{
		private Action<LazyNotification> dispatcher;
		private Id id;

		public void Init(Action<LazyNotification> dispatcher, Id id)
		{
			this.dispatcher = dispatcher;
			this.id = id;
		}
		public void Invoke(LazyNotification lazyNotification) => ThreadPool.QueueUserWorkItem(Call, lazyNotification);
		private void Call(object obj)
		{
			using (NLog.MappedDiagnosticsContext.SetScoped("BotId", id))
				dispatcher.Invoke((LazyNotification)obj);
		}
		public void DoWork() { }
		public void Dispose() { }
	}
}
