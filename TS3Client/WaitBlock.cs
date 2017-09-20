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
	using Commands;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Threading;

	internal class WaitBlock : IDisposable
	{
		private readonly ManualResetEvent answerWaiter;
		private readonly ManualResetEvent notificationWaiter;
		private CommandError commandError;
		private string commandLine;
		public NotificationType[] DependsOn { get; }
		private LazyNotification notification;
		public bool Closed { get; private set; }

		public WaitBlock(NotificationType[] dependsOn = null)
		{
			Closed = false;
			answerWaiter = new ManualResetEvent(false);
			DependsOn = dependsOn;
			if (DependsOn != null)
			{
				if (DependsOn.Length == 0)
					throw new InvalidOperationException("Depending notification array must not be empty");
				notificationWaiter = new ManualResetEvent(false);
			}
		}

		public IEnumerable<T> WaitForMessage<T>() where T : IResponse, new()
		{
			answerWaiter.WaitOne();
			if (commandError.Id != Ts3ErrorCode.ok)
				throw new Ts3CommandException(commandError);

			return CommandDeserializer.GenerateResponse<T>(commandLine);
		}

		public LazyNotification WaitForNotification()
		{
			if (DependsOn == null)
				throw new InvalidOperationException("This waitblock has no dependent Notification");
			answerWaiter.WaitOne();
			if (commandError.Id != Ts3ErrorCode.ok)
				throw new Ts3CommandException(commandError);
			notificationWaiter.WaitOne();

			return notification;
		}

		public void SetAnswer(CommandError commandError, string commandLine = null)
		{
			this.commandError = commandError ?? throw new ArgumentNullException(nameof(commandError));
			this.commandLine = commandLine;
			answerWaiter.Set();
		}

		public void SetNotification(LazyNotification notification)
		{
			if (DependsOn != null && Array.IndexOf(DependsOn, notification.NotifyType) < 0)
				throw new ArgumentException("The notification does not match this waitblock");
			this.notification = notification;
			notificationWaiter.Set();
		}

		public void Dispose()
		{
			Closed = true;

			answerWaiter.Set();
			answerWaiter.Dispose();

			if (notificationWaiter != null)
			{
				notificationWaiter.Set();
				notificationWaiter.Dispose();
			}
		}
	}
}
