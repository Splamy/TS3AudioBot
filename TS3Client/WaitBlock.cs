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
		private AutoResetEvent waiter = new AutoResetEvent(false);
		private CommandError commandError = null;
		private string commandLine = null;

		public WaitBlock() { }

		public IEnumerable<T> WaitForMessage<T>() where T : IResponse, new()
		{
			waiter.WaitOne();
			if (commandError.Id != Ts3ErrorCode.ok)
				throw new Ts3CommandException(commandError);

			return CommandDeserializer.GenerateResponse<T>(commandLine);
		}

		public void SetAnswer(CommandError commandError, string commandLine = null)
		{
			if (commandError == null)
				throw new ArgumentNullException(nameof(commandError));
			this.commandError = commandError;
			this.commandLine = commandLine;
			waiter.Set();
		}

		public void Dispose()
		{
			if (waiter != null)
			{
				waiter.Set();
				waiter.Dispose();
				waiter = null;
			}
		}
	}
}
