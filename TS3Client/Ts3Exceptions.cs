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
	using Messages;
	using System;
	
	public class Ts3Exception : Exception
	{
		public Ts3Exception(string message) : base(message) { }
		public Ts3Exception(string message, Exception innerException) : base(message, innerException) { }
	}
	
	public class Ts3CommandException : Ts3Exception
	{
		public CommandError ErrorStatus { get; private set; }

		internal Ts3CommandException(CommandError message) : base(message.ErrorFormat()) { ErrorStatus = message; }
		internal Ts3CommandException(CommandError message, Exception inner) : base(message.ErrorFormat(), inner) { ErrorStatus = message; }
	}
}
