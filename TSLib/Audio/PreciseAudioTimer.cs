// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Diagnostics;

namespace TSLib.Audio
{
	/// <summary>Provides a precise way to measure a playbackbuffer by tracking
	/// sent bytes and elapsed time.</summary>
	public class PreciseAudioTimer : ISampleInfo
	{
		public int SampleRate { get; }
		public int Channels { get; }
		public int BitsPerSample { get; }
		public int BytesPerSecond { get; }

		private readonly Stopwatch stopwatch;

		/// <summary>How many bytes have added to the playback buffer.</summary>
		private long AbsoluteBufferLength { get; set; }
		/// <summary>The playback duration equivalent of the pushed buffer byte length.</summary>
		private TimeSpan AbsoluteBufferDuration => TimeSpan.FromSeconds(AbsoluteBufferLength / (double)BytesPerSecond);
		/// <summary>How many bytes (should) have been processed while playback was running.</summary>
		private long ElapsedBufferLength => (stopwatch.ElapsedMilliseconds * BytesPerSecond) / 1000;
		/// <summary>How many bytes are currently not processed in buffer for playback.</summary>
		private long RemainingBufferLength => AbsoluteBufferLength - ElapsedBufferLength;
		/// <summary>The playback duration equivalent of the currently not processed buffer byte length.</summary>
		public TimeSpan RemainingBufferDuration => TimeSpan.FromSeconds(RemainingBufferLength / (double)BytesPerSecond);
		/// <summary>Defines the song position base offset from which the SongPosition should be counted from.</summary>
		public TimeSpan SongPositionOffset { get; set; }
		/// <summary>The current playback position.</summary>
		public TimeSpan SongPosition => AbsoluteBufferDuration + SongPositionOffset;

		public PreciseAudioTimer(ISampleInfo sampleInfo)
			: this(sampleInfo.SampleRate, sampleInfo.BitsPerSample, sampleInfo.Channels) { }

		public PreciseAudioTimer(int sampleRate, int bits, int channel)
		{
			if (bits != 8 && bits != 16 && bits != 24 && bits != 32) throw new ArgumentException(nameof(bits));
			if (channel != 1 && channel != 2) throw new ArgumentException(nameof(channel));
			stopwatch = new Stopwatch();

			SampleRate = sampleRate;
			BitsPerSample = bits;
			Channels = channel;
			BytesPerSecond = SampleRate * (BitsPerSample / 8) * Channels;
		}

		public void Start()
		{
			AbsoluteBufferLength = 0;
			stopwatch.Restart();
		}

		public void Stop() => stopwatch.Stop();

		public void PushBytes(int count) => AbsoluteBufferLength += count;

		public void ResetRemoteBuffer()
		{
			SongPositionOffset = SongPosition;
			Start();
		}
	}
}
