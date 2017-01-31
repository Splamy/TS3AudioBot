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
		private IEnumerable<IResponse> answer = null;
		private CommandError errorStatus = null;
		public Type AnswerType { get; }

		public WaitBlock(Type answerType)
		{
			AnswerType = answerType;
		}

		public IEnumerable<IResponse> WaitForMessage()
		{
			waiter.WaitOne();
			if (!errorStatus.Ok)
				throw new Ts3CommandException(errorStatus);
			return answer;
		}

		public void SetAnswer(CommandError error, IEnumerable<IResponse> answer)
		{
			this.answer = answer;
			SetAnswer(error);
		}

		public void SetAnswer(CommandError error)
		{
			if (error == null)
				throw new ArgumentNullException(nameof(error));
			errorStatus = error;
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
