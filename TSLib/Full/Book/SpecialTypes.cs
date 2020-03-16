// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Full.Book
{
	public struct MaxClients
	{
		public ushort Count { get; internal set; }
		public MaxClientsKind LimitKind { get; internal set; }
	}

	public enum MaxClientsKind
	{
		Unlimited,
		Inherited,
		Limited,
	}

	public enum ChannelType
	{
		Temporary,
		SemiPermanent,
		Permanent,
	}

	public struct TalkPowerRequest
	{
		public DateTime Time { get; internal set; }
		public string Message { get; internal set; }
	}
}
