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

namespace TS3AudioBot.CommandSystem
{
	using System;
	using TS3Query.Messages;

	public class ExecutionInformation : MarshalByRefObject
	{
		public BotSession Session { get; }
		public TextMessage TextMessage { get; }
		public Lazy<bool> IsAdmin { get; }

		private ExecutionInformation() { Session = null; TextMessage = null; IsAdmin = new Lazy<bool>(() => true); }
		public ExecutionInformation(BotSession session, TextMessage textMessage, Lazy<bool> isAdmin)
		{
			Session = session;
			TextMessage = textMessage;
			IsAdmin = isAdmin;
		}

		public static readonly ExecutionInformation Debug = new ExecutionInformation();
	}
}
