// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Runtime.InteropServices;

namespace TSLib.Audio
{
	public static class AudioTools
	{
		public static bool TryMonoToStereo(byte[] pcm, ref int length)
		{
			if (length / 2 >= pcm.Length)
				return false;

			var shortArr = MemoryMarshal.Cast<byte, short>(pcm);

			for (int i = (length / 2) - 1; i >= 0; i--)
			{
				shortArr[i * 2 + 0] = shortArr[i];
				shortArr[i * 2 + 1] = shortArr[i];
			}

			length *= 2;

			return true;
		}
	}
}
