// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Runtime.InteropServices;

namespace TSLib.Audio
{
	public class VolumePipe : IAudioPipe
	{
		public bool Active => OutStream?.Active ?? false;
		private float volume;
		/// <summary>Values are between including 0 and 1.</summary>
		public float Volume
		{
			get => volume;
			set
			{
				value = Math.Abs(value);
				if (IsAbout(value, 1)) volume = 1;
				else if (IsAbout(value, 0)) volume = 0;
				else volume = value;
			}
		}
		public IAudioPassiveConsumer OutStream { get; set; }

		public static void AdjustVolume(Span<byte> audioSamples, float volume)
		{
			if (volume == 1) { /* Do nothing */ }
			else if (volume == 0)
			{
				audioSamples.Fill(0);
			}
			else if (volume < 1) // Clipping cannot occour on mult <1
			{
				var shortArr = MemoryMarshal.Cast<byte, short>(audioSamples);
				for (int i = 0; i < shortArr.Length; i++)
					shortArr[i] = (short)(shortArr[i] * volume);
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
