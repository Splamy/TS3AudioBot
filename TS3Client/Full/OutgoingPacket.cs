// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full
{
	using System;

	public sealed class OutgoingPacket : BasePacket
	{
		public ushort ClientId { get; set; }

		public DateTime FirstSendTime { get; set; }
		public DateTime LastSendTime { get; set; }

		public OutgoingPacket(byte[] data, PacketType type)
		{
			Data = data;
			PacketType = type;
			Header = new byte[5];
		}

		public void BuildHeader()
		{
			NetUtil.H2N(PacketId, Header, 0);
			NetUtil.H2N(ClientId, Header, 2);
			Header[4] = PacketTypeFlagged;
		}
	}
}
