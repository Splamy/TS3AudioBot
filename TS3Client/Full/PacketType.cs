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

namespace TS3Client.Full
{
	using System;

	internal enum PacketType : byte
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
	internal enum PacketFlags : byte
	{
		None = 0x0,
		Fragmented = 0x10,
		Newprotocol = 0x20,
		Compressed = 0x40,
		Unencrypted = 0x80,
	}
}
