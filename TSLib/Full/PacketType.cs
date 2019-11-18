// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Full
{
	public enum PacketType : byte
	{
		Voice = 0x0,
		VoiceWhisper = 0x1,
		Command = 0x2,
		CommandLow = 0x3,
		Ping = 0x4,
		Pong = 0x5,
		Ack = 0x6,
		AckLow = 0x7,
		Init1 = 0x8,
	}

	[Flags]
	public enum PacketFlags : byte
	{
		None = 0x0,
		Fragmented = 0x10,
		Newprotocol = 0x20,
		Compressed = 0x40,
		Unencrypted = 0x80,
	}
}
