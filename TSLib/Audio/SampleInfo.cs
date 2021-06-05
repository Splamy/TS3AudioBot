// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Audio
{
	public readonly struct SampleInfo
	{
		public int SampleRate { get; }
		public int Channels { get; }
		public int BitsPerSample { get; }

		public static readonly SampleInfo OpusMusic = new SampleInfo(48_000, 2, 16);
		public static readonly SampleInfo OpusVoice = new SampleInfo(48_000, 1, 16);

		public SampleInfo(int sampleRate, int channels, int bitsPerSample)
		{
			SampleRate = sampleRate;
			Channels = channels;
			BitsPerSample = bitsPerSample;
		}

		public readonly int GetBytesPerSecond() => SampleRate * (BitsPerSample / 8) * Channels;

		public readonly int TimeToByteCount(TimeSpan duration)
			=> (int)(GetBytesPerSecond() * duration.Ticks / TimeSpan.TicksPerSecond);
	}
}
