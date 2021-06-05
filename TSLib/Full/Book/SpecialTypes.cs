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
	public readonly struct MaxClients
	{
		public MaxClientsKind LimitKind { get; init; }
		public ushort Count { get; init; }

		public MaxClients(MaxClientsKind limitKind, ushort count) =>
			(LimitKind, Count) = (limitKind, count);

		public static readonly MaxClients Unlimited = new(MaxClientsKind.Unlimited, 0);
		public static readonly MaxClients Inherited = new(MaxClientsKind.Inherited, 0);
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

	public readonly struct TalkPowerRequest
	{
		public DateTime Time { get; init; }
		public string Message { get; init; }
	}
}
