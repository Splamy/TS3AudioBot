// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Threading;
using System.Threading.Tasks;
using TSLib.Messages;

namespace TSLib
{
	// TODO check maybe splittable into 'WaitBlockSync' and 'WaitBlockAsync'
	internal sealed class WaitBlock : IDisposable
	{
		private readonly TaskCompletionSource<bool> answerWaiterAsync;
		private readonly ManualResetEvent answerWaiter;
		private readonly ManualResetEvent notificationWaiter;
		private readonly Deserializer deserializer;
		private CommandError commandError;
		private ReadOnlyMemory<byte>? commandLine;
		public NotificationType[] DependsOn { get; }
		private LazyNotification notification;
		private bool isDisposed;
		private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);
		private readonly bool async;

		public WaitBlock(Deserializer deserializer, bool async, NotificationType[] dependsOn = null)
		{
			this.deserializer = deserializer;
			this.async = async;
			isDisposed = false;
			if (async)
				answerWaiterAsync = new TaskCompletionSource<bool>();
			else
				answerWaiter = new ManualResetEvent(false);
			DependsOn = dependsOn;
			if (DependsOn != null)
			{
				if (DependsOn.Length == 0)
					throw new InvalidOperationException("Depending notification array must not be empty");
				notificationWaiter = new ManualResetEvent(false);
			}
		}

		public R<T[], CommandError> WaitForMessage<T>() where T : IResponse, new()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(WaitBlock));
			if (!answerWaiter.WaitOne(CommandTimeout))
				return CommandError.TimeOut;
			if (commandError.Id != TsErrorCode.ok)
				return commandError;

			var result = deserializer.GenerateResponse<T>(commandLine.Value.Span);
			if (result.Ok)
				return result.Value;
			else
				return CommandError.Parser;
		}

		public async Task<R<T[], CommandError>> WaitForMessageAsync<T>() where T : IResponse, new()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(WaitBlock));
			var timeOut = Task.Delay(CommandTimeout);
			var res = await Task.WhenAny(answerWaiterAsync.Task, timeOut).ConfigureAwait(false);
			if (res == timeOut)
				return CommandError.TimeOut;
			if (commandError.Id != TsErrorCode.ok)
				return commandError;

			var result = deserializer.GenerateResponse<T>(commandLine.Value.Span);
			if (result.Ok)
				return result.Value;
			else
				return CommandError.Parser;
		}

		public R<LazyNotification, CommandError> WaitForNotification()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(WaitBlock));
			if (DependsOn is null)
				throw new InvalidOperationException("This waitblock has no dependent Notification");
			if (!answerWaiter.WaitOne(CommandTimeout))
				return CommandError.TimeOut;
			if (commandError.Id != TsErrorCode.ok)
				return commandError;
			if (!notificationWaiter.WaitOne(CommandTimeout))
				return CommandError.TimeOut;

			return notification;
		}

		public void SetAnswer(CommandError commandError, ReadOnlyMemory<byte>? commandLine = null)
		{
			if (isDisposed)
				return;
			this.commandError = commandError ?? throw new ArgumentNullException(nameof(commandError));
			this.commandLine = commandLine;
			if (async)
				answerWaiterAsync.SetResult(true);
			else
				answerWaiter.Set();
		}

		public void SetNotification(LazyNotification notification)
		{
			if (isDisposed)
				return;
			if (DependsOn != null && Array.IndexOf(DependsOn, notification.NotifyType) < 0)
				throw new ArgumentException("The notification does not match this waitblock");
			this.notification = notification;
			notificationWaiter.Set();
		}

		public void Dispose()
		{
			if (isDisposed)
				return;
			isDisposed = true;

			if (!async)
			{
				answerWaiter.Set();
				answerWaiter.Dispose();
			}

			if (notificationWaiter != null)
			{
				notificationWaiter.Set();
				notificationWaiter.Dispose();
			}
		}
	}
}
