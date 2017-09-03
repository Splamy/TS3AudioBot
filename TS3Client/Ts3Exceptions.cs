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
