// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full.Audio
{
	using System;

	public class VolumePipe : IAudioPipe
	{
		public float Volume { get; set; } = 1;
		public IAudioPassiveConsumer OutStream { get; set; }

		public static void AdjustVolume(Span<byte> audioSamples, float volume)
		{
			if (IsAbout(volume, 1)) { /* Do nothing */ }
			else if (IsAbout(volume, 0))
			{
				audioSamples.Fill(0);
			}
			else if (IsAbout(volume, 0.5f))
			{
				// fast calculation for *0.5 volume
				for (int i = 0; i < audioSamples.Length; i += 2)
				{
					short value = unchecked((short)((audioSamples[i + 1] << 8) | audioSamples[i]));
					var tmpshort = value >> 1;
					audioSamples[i + 0] = unchecked((byte)(tmpshort >> 0));
					audioSamples[i + 1] = unchecked((byte)(tmpshort >> 8));
				}
			}
			else
			{
				for (int i = 0; i < audioSamples.Length; i += 2)
				{
					short value = unchecked((short)((audioSamples[i + 1] << 8) | audioSamples[i]));
					var tmpshort = (short)Math.Max(Math.Min(value * volume, short.MaxValue), short.MinValue);
					audioSamples[i + 0] = unchecked((byte)(tmpshort >> 0));
					audioSamples[i + 1] = unchecked((byte)(tmpshort >> 8));
				}
			}
		}

		private static bool IsAbout(float value, float compare) => Math.Abs(value - compare) < 1E-04f;

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream == null) return;

			AdjustVolume(data, Volume);

			OutStream?.Write(data, meta);
		}
	}
}
