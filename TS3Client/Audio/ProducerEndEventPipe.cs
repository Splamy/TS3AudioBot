// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TS3Client.Audio
{
	public class ProducerEndEventPipe : IAudioPassiveProducerEvent, IAudioActiveConsumer
	{
		private bool hasFired = false;

		public IAudioPassiveProducer InStream { get; set; }

		public event EventHandler OnSongEnd;

		public ProducerEndEventPipe() { }

		public ProducerEndEventPipe(IAudioPassiveProducer stream) : this()
		{
			InStream = stream;
		}

		public int Read(byte[] buffer, int offset, int length, out Meta meta)
		{
			var stream = InStream;
			if (stream is null)
			{
				meta = default;
				return 0;
			}

			var read = stream.Read(buffer, offset, length, out meta);
			if (read == 0 && !hasFired)
			{
				hasFired = true;
				OnSongEnd?.Invoke(this, EventArgs.Empty);
				return 0;
			}
			return read;
		}

		public void Reset() => hasFired = false;
	}
}
