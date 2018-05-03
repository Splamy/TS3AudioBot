// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Audio
{
	using System;
	using System.IO;

	public class StreamAudioProducer : IAudioPassiveProducer
	{
		private readonly Stream stream;
		public event EventHandler HitEnd;

		public int Read(byte[] buffer, int offset, int length, out Meta meta)
		{
			meta = default;
			int read = stream.Read(buffer, offset, length);
			if (read < length)
				HitEnd?.Invoke(this, EventArgs.Empty);
			return read;
		}
		public StreamAudioProducer(Stream stream) { this.stream = stream; }
	}
}
