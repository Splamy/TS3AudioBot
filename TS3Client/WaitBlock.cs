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
	using Commands;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Threading;

	internal class WaitBlock : IDisposable
	{
		private readonly ManualResetEvent answerWaiter;
		private readonly ManualResetEvent notificationWaiter;
		private CommandError commandError = null;
		private string commandLine = null;
		public NotificationType DependsOn { get; }
		private LazyNotification notification;
		public bool Closed { get; private set; }

		public WaitBlock(NotificationType dependsOn = NotificationType.Unknown)
		{
			Closed = false;
			answerWaiter = new ManualResetEvent(false);
			DependsOn = dependsOn;
			if (DependsOn != NotificationType.Unknown)
				notificationWaiter = new ManualResetEvent(false);
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
			if (DependsOn == NotificationType.Unknown)
				throw new InvalidOperationException("This waitblock has no dependent Notification");
			answerWaiter.WaitOne();
			if (commandError.Id != Ts3ErrorCode.ok)
				throw new Ts3CommandException(commandError);
			notificationWaiter.WaitOne();

			return notification;
		}

		public void SetAnswer(CommandError commandError, string commandLine = null)
		{
			if (commandError == null)
				throw new ArgumentNullException(nameof(commandError));
			this.commandError = commandError;
			this.commandLine = commandLine;
			answerWaiter.Set();
		}

		public void SetNotification(LazyNotification notification)
		{
			if (notification.NotifyType != DependsOn)
				throw new ArgumentException();
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
