// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Threading;
using TSLib.Helper;

namespace TSLib.Audio
{
	public class PreciseTimedPipe : IAudioActiveConsumer, IAudioActiveProducer, IDisposable
	{
		public PreciseAudioTimer AudioTimer { get; }

		public IAudioPassiveProducer? InStream { get; set; }
		public IAudioPassiveConsumer? OutStream { get; set; }

		public TimeSpan AudioBufferLength { get; set; } = TimeSpan.FromMilliseconds(20);
		public TimeSpan SendCheckInterval { get; set; } = TimeSpan.FromMilliseconds(5);
		public int ReadBufferSize { get; set; } = 960 * 4;
		private byte[] readBuffer = Array.Empty<byte>();
		private readonly Thread tickThread;
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

		public PreciseTimedPipe(ISampleInfo info, Id id)
		{
			running = true;
			paused = true;

			AudioTimer = new PreciseAudioTimer(info.SampleRate, info.BitsPerSample, info.Channels);
			AudioTimer.Start();

			tickThread = new Thread(() => { Tools.SetLogId(id); ReadLoop(); }) { Name = $"AudioPipe[{id}]" };
			tickThread.Start();
		}

		public PreciseTimedPipe(ISampleInfo info, Id id, IAudioPassiveProducer inStream) : this(info, id)
		{
			InStream = inStream;
		}

		public PreciseTimedPipe(ISampleInfo info, Id id, IAudioPassiveConsumer outStream) : this(info, id)
		{
			OutStream = outStream;
		}

		public PreciseTimedPipe(ISampleInfo info, Id id, IAudioPassiveProducer inStream, IAudioPassiveConsumer outStream) : this(info, id)
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
					return;

				if (AudioTimer.RemainingBufferDuration < TimeSpan.Zero)
					AudioTimer.ResetRemoteBuffer();

				AudioTimer.PushBytes(read);

				OutStream?.Write(readBuffer.AsSpan(0, read), meta);
			}
		}

		public void Dispose()
		{
			if (!running)
				return;

			running = false;
			tickThread.Join();
		}
	}
}
