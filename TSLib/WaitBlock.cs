// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TSLib.Full;
using TSLib.Messages;

namespace TSLib
{
	/// <summary>
	/// Synchronizes data between the receiving packet Thread and the waiting dispatcher Thread.
	/// </summary>
	internal sealed class WaitBlock : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const string NotANotifyBlock = "This waitblock has no dependent notification";
		private const string NotifyListEmpty = "Depending notification array must not be empty";
		private const string NotifyDoesNotMatch = "The notification does not match this waitblock";
		private static readonly TimeSpan CommandTimeout = PacketHandlerConst.PacketTimeout.Divide(2);

		private bool isDisposed;
		private readonly Deserializer deserializer;
		private readonly TaskCompletionSource<R<ReadOnlyMemory<byte>, CommandError>> answerWaiterAsync;
		private readonly TaskCompletionSource<LazyNotification>? notificationWaiterAsync;

		public NotificationType[]? DependsOn { get; }

		public WaitBlock(Deserializer deserializer, NotificationType[]? dependsOn = null)
		{
			this.deserializer = deserializer;
			isDisposed = false;
			DependsOn = dependsOn;

			answerWaiterAsync = new TaskCompletionSource<R<ReadOnlyMemory<byte>, CommandError>>();
			if (dependsOn != null)
			{
				if (dependsOn.Length == 0)
					throw new InvalidOperationException(NotifyListEmpty);
				notificationWaiterAsync = new TaskCompletionSource<LazyNotification>();
			}
		}

		public void SetError(CommandError commandError)
		{
			if (commandError.Id == TsErrorCode.ok)
				throw new ArgumentException("Passed explicit error without error code", nameof(commandError));
			SetAnswerAuto(commandError, null);
		}

		public void SetAnswer(ReadOnlyMemory<byte> commandLine) => SetAnswerAuto(null, commandLine);

		public void SetAnswerAuto(CommandError? commandError, ReadOnlyMemory<byte>? commandLine)
		{
			if (isDisposed)
				return;

			if (commandError != null && commandError.Id != TsErrorCode.ok)
			{
				answerWaiterAsync.SetResult(commandError);
			}
			else if (commandLine != null)
			{
				answerWaiterAsync.SetResult(commandLine.Value);
			}
			else
			{
				answerWaiterAsync.SetResult(ReadOnlyMemory<byte>.Empty);
			}
		}

		public void SetNotification(LazyNotification notification)
		{
			if (isDisposed)
				return;
			if (notificationWaiterAsync is null || DependsOn is null)
				throw new InvalidOperationException(NotANotifyBlock);
			if (Array.IndexOf(DependsOn, notification.NotifyType) < 0)
				throw new ArgumentException(NotifyDoesNotMatch);
			notificationWaiterAsync.SetResult(notification);
		}

		public async Task<R<T[], CommandError>> WaitForMessageAsync<T>() where T : IResponse, new()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(WaitBlock));

			var timeOut = Task.Delay(CommandTimeout);
			var res = await Task.WhenAny(answerWaiterAsync.Task, timeOut);
			if (res == timeOut)
				return CommandError.CommandTimeout;
			Trace.Assert(answerWaiterAsync.Task.IsCompleted);

			if (!(await answerWaiterAsync.Task).Get(out var value, out var error))
				return error;

			var response = deserializer.GenerateResponse<T>(value.Span);
			if (response is null)
				return CommandError.Parser;
			else
				return response;
		}

		public async Task<R<LazyNotification, CommandError>> WaitForNotificationAsync()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(WaitBlock));
			if (notificationWaiterAsync is null)
				throw new InvalidOperationException(NotANotifyBlock);

			var timeOut = Task.Delay(CommandTimeout);
			if (await Task.WhenAny(answerWaiterAsync.Task, timeOut) == timeOut)
				return CommandError.CommandTimeout;
			Trace.Assert(answerWaiterAsync.Task.IsCompleted);

			if (!(await answerWaiterAsync.Task).OnlyError().GetOk(out var error))
				return error;

			if (await Task.WhenAny(notificationWaiterAsync.Task, timeOut) == timeOut)
				return CommandError.CommandTimeout;
			Trace.Assert(notificationWaiterAsync.Task.IsCompleted);

			return await notificationWaiterAsync.Task;
		}

		public void Dispose()
		{
			if (isDisposed)
				return;

			answerWaiterAsync.TrySetResult(CommandError.ConnectionClosed);
			notificationWaiterAsync?.TrySetCanceled();

			isDisposed = true;
		}
	}
}
