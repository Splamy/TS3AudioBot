// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Audio
{
	using System;

	internal static class AudioModifier
	{
		public static void AdjustVolume(byte[] audioSamples, int length, float volume)
		{
			if (volume.IsAbout(1))
				return;
			else if (volume.IsAbout(0))
			{
				Array.Clear(audioSamples, 0, length);
				return;
			}

			if (BitConverter.IsLittleEndian)
			{
				for (int i = 0; i < length; i += 2)
				{
					short value = (short)((audioSamples[i + 1]) << 8 | audioSamples[i]);
					var tmpshort = (short)(value * volume);
					audioSamples[i + 0] = (byte)((tmpshort & 0x00FF) >> 0);
					audioSamples[i + 1] = (byte)((tmpshort & 0xFF00) >> 8);
				}
			}
			else
			{
				for (int i = 0; i < length; i += 2)
				{
					short value = (short)((audioSamples[i + 1]) | (audioSamples[i] << 8));
					var tmpshort = (short)(value * volume);
					audioSamples[i + 0] = (byte)((tmpshort & 0xFF00) >> 8);
					audioSamples[i + 1] = (byte)((tmpshort & 0x00FF) >> 0);
				}
			}
		}

		private static bool IsAbout(this float value, float compare) => Math.Abs(value - compare) < 1E-03f;
	}
}
