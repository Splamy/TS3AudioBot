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

	internal sealed class S2CPacket : BasePacket
	{
		public const int HeaderLen = 3;

		public override bool FromServer { get; } = true;
		public override int HeaderLength { get; } = HeaderLen;

		public S2CPacket(byte[] raw)
		{
			Raw = raw;
			Header = new byte[HeaderLen];
		}

		public override void BuildHeader(Span<byte> into)
		{
			BinaryPrimitives.WriteUInt16BigEndian(into.Slice(0, 2), PacketId);
			into[2] = PacketTypeFlagged;
#if DEBUG
			into.CopyTo(Header.AsSpan());
#endif
		}
	}
}
