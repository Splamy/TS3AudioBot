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
	using System.Runtime.Serialization;

	[Serializable]
	public class CommandException : Exception
	{
		public CommandExceptionReason Reason { get; }

		protected CommandException() : this(CommandExceptionReason.Unknown) { }
		protected CommandException(CommandExceptionReason reason) { Reason = reason; }

		protected CommandException(string message) : this(message, CommandExceptionReason.Unknown) { }
		public CommandException(string message, CommandExceptionReason reason) : base(message) { Reason = reason; }

		protected CommandException(string message, Exception inner) : this(message, inner, CommandExceptionReason.Unknown) { }
		public CommandException(string message, Exception inner, CommandExceptionReason reason) : base(message, inner) { Reason = reason; }

		protected CommandException(
		  SerializationInfo info,
		  StreamingContext context) : base(info, context)
		{ }
	}

	public enum CommandExceptionReason
	{
		Unknown = 0,
		InternalError,
		Unauthorized,

		CommandError = 10,
		MissingRights,
		AmbiguousCall,
		MissingParameter,
		NoReturnMatch,
		FunctionNotFound,
		NotSupported,
	}
}
