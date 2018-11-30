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
	using System.Runtime.InteropServices;

	public class VolumePipe : IAudioPipe
	{
		public bool Active => OutStream?.Active ?? false;
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
				var shortArr = MemoryMarshal.Cast<byte, short>(audioSamples);
				for (int i = 0; i < shortArr.Length; i++)
					shortArr[i] = (short)(shortArr[i] >> 1);
			}
			else
			{
				var shortArr = MemoryMarshal.Cast<byte, short>(audioSamples);
				for (int i = 0; i < shortArr.Length; i++)
					shortArr[i] = (short)Math.Max(Math.Min(shortArr[i] * volume, short.MaxValue), short.MinValue);
			}
		}

		private static bool IsAbout(float value, float compare) => Math.Abs(value - compare) < 1E-04f;

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream is null) return;

			AdjustVolume(data, Volume);

			OutStream?.Write(data, meta);
		}
	}
}
