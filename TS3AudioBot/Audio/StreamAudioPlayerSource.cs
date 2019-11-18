// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using TSLib.Audio;

namespace TS3AudioBot.Audio
{
	public class StreamAudioPlayerSource : IPlayerSource, IAudioActiveConsumer
	{
		private bool hasFired = false;

		public IAudioPassiveProducer InStream { get; set; }
		public TimeSpan Length => TimeSpan.Zero;
		public TimeSpan Position { get => TimeSpan.Zero; set { } }

		public event EventHandler OnSongEnd;
		event EventHandler<SongInfoChanged> IPlayerSource.OnSongUpdated { add { } remove { } }

		public StreamAudioPlayerSource() { }

		public StreamAudioPlayerSource(IAudioPassiveProducer stream) : this()
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

		public void Dispose() => InStream?.Dispose();
	}
}
