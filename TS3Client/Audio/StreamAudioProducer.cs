// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.IO;

namespace TS3Client.Audio
{
	public class StreamAudioProducer : IAudioPassiveProducer
	{
		private readonly Stream stream;

		public StreamAudioProducer(Stream stream) { this.stream = stream; }

		public int Read(byte[] buffer, int offset, int length, out Meta meta)
		{
			meta = default;
			return stream.Read(buffer, offset, length);
		}

		public void Dispose() => stream.Dispose();
	}
}
