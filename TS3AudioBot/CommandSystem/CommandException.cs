// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Runtime.Serialization;

namespace TS3AudioBot.CommandSystem
{
	[Serializable]
	public class CommandException : Exception
	{
		public CommandExceptionReason Reason { get; }

		protected CommandException()
			: this(CommandExceptionReason.Unknown)
		{ }

		protected CommandException(CommandExceptionReason reason)
		{ Reason = reason; }

		public CommandException(string message, CommandExceptionReason reason)
			: base(message)
		{ Reason = reason; }

		public CommandException(string message, Exception inner, CommandExceptionReason reason)
			: base(message, inner)
		{ Reason = reason; }

		protected CommandException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{ }
	}

	[Serializable]
	public class MissingContextCommandException : CommandException
	{
		public Type MissingType { get; }

		protected MissingContextCommandException(Type missingType)
			: base(CommandExceptionReason.MissingContext)
		{ MissingType = missingType; }

		public MissingContextCommandException(string message, Type missingType)
			: base(message, CommandExceptionReason.MissingContext)
		{ MissingType = missingType; }

		public MissingContextCommandException(string message, Exception inner, Type missingType)
			: base(message, inner, CommandExceptionReason.MissingContext)
		{ MissingType = missingType; }
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
		MissingContext,
	}
}
