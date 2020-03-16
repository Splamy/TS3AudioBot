// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Buffers.Binary;

namespace TSLib.Audio
{
	public class AudioPacketReader : IAudioPipe
	{
		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer OutStream { get; set; }

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream is null)
				return;

			if (data.Length < 5) // Invalid packet
				return;

			// Skip [0,2) Voice Packet Id for now
			// TODO add packet id order checking
			// TODO add defragment start
			meta.In.Sender = (ClientId)BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2, 2));
			meta.Codec = (Codec)data[4];
			OutStream?.Write(data.Slice(5), meta);
		}
	}
}
