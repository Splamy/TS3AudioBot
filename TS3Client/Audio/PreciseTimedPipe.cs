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
	using System.Threading;

	public class PreciseTimedPipe : IAudioActiveConsumer, IAudioActiveProducer, IDisposable
	{
		public PreciseAudioTimer AudioTimer { get; private set; }

		public IAudioPassiveProducer InStream { get; set; }
		public IAudioPassiveConsumer OutStream { get; set; }

		public TimeSpan AudioBufferLength { get; set; } = TimeSpan.FromMilliseconds(20);
		public TimeSpan SendCheckInterval { get; set; } = TimeSpan.FromMilliseconds(5);
		public int ReadBufferSize { get; set; } = 960 * 4;
		private byte[] readBuffer = Array.Empty<byte>();
		private readonly object lockObject = new object();
		private Thread tickThread;
		private bool running;

		private bool paused;
		public bool Paused
		{
			get => paused;
			set
			{
				if (paused != value)
				{
					paused = value;
					if (value)
					{
						AudioTimer.SongPositionOffset = AudioTimer.SongPosition;
						AudioTimer.Stop();
					}
					else
						AudioTimer.Start();
				}
			}
		}

		public PreciseTimedPipe() { }

		public PreciseTimedPipe(IAudioPassiveProducer inStream) : this()
		{
			InStream = inStream;
		}

		public PreciseTimedPipe(IAudioPassiveConsumer outStream) : this()
		{
			OutStream = outStream;
		}

		public PreciseTimedPipe(IAudioPassiveProducer inStream, IAudioPassiveConsumer outStream) : this()
		{
			InStream = inStream;
			OutStream = outStream;
		}

		private void ReadLoop()
		{
			while (running)
			{
				if (!Paused)
					ReadTick();
				Thread.Sleep(SendCheckInterval);
			}
		}

		private void ReadTick()
		{
			var inStream = InStream;
			if (inStream is null)
				return;

			if (readBuffer.Length < ReadBufferSize)
				readBuffer = new byte[ReadBufferSize];

			while (AudioTimer.RemainingBufferDuration < AudioBufferLength)
			{
				int read = inStream.Read(readBuffer, 0, readBuffer.Length, out var meta);
				if (read == 0)
				{
					AudioTimer.Start();
					return;
				}

				AudioTimer.PushBytes(read);

				OutStream?.Write(new Span<byte>(readBuffer, 0, read), meta);
			}
		}

		public void Initialize(ISampleInfo info)
		{
			lock (lockObject)
			{
				AudioTimer = new PreciseAudioTimer(info.SampleRate, info.BitsPerSample, info.Channels);
				AudioTimer.Start();

				if (running)
					return;

				running = true;
				tickThread = new Thread(ReadLoop);
				tickThread.Start();
			}
		}

		public void Dispose()
		{
			lock (lockObject)
			{
				if (!running)
					return;

				running = false;
				tickThread.Join();
			}
		}
	}
}
