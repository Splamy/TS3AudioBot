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
	using System.Buffers.Binary;

	internal sealed class C2SPacket : BasePacket
	{
		public const int HeaderLen = 5;

		public ushort ClientId { get; set; }
		public override bool FromServer { get; } = false;
		public override int HeaderLength { get; } = HeaderLen;

		public DateTime FirstSendTime { get; set; }
		public DateTime LastSendTime { get; set; }

		public C2SPacket(byte[] data, PacketType type)
		{
			Data = data;
			PacketType = type;
			Header = new byte[HeaderLen];
		}

		public override void BuildHeader(Span<byte> into)
		{
			BinaryPrimitives.WriteUInt16BigEndian(into.Slice(0, 2), PacketId);
			BinaryPrimitives.WriteUInt16BigEndian(into.Slice(2, 2), ClientId);
			into[4] = PacketTypeFlagged;
#if DEBUG
			into.CopyTo(Header.AsSpan());
#endif
		}

		public override string ToString()
		{
			BuildHeader();
			return base.ToString();
		}
	}
}
