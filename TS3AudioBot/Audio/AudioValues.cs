// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using TSLib.Helper;

namespace TS3AudioBot.Audio
{
	public static class AudioValues
	{
		public const float MinVolume = 0;
		public const float MaxVolume = 100;

		// Reference explanation for the logarithmic scale
		// https://www.dr-lex.be/info-stuff/volumecontrols.html#table1
		// Adjusted values for 40dB

		private const float fact_a = 1e-2f;
		private const float fact_b = 4.61512f;

		public static float HumanVolumeToFactor(float value)
		{
			if (value < MinVolume) return 0;
			if (value > MaxVolume) return 1;

			// Map input values from [MinVolume, MaxVolume] to [0, 1]
			value = (value - MinVolume) / (MaxVolume - MinVolume);

			// Scale the value logarithmically
			return Tools.Clamp((float)(fact_a * Math.Exp(fact_b * value)) - fact_a, 0, 1);
		}

		public static float FactorToHumanVolume(float value)
		{
			if (value < 0) return MinVolume;
			if (value > 1) return MaxVolume;

			// Undo logarithmical scale
			value = Tools.Clamp((float)(Math.Log((value + fact_a) / fact_a) / fact_b), 0, 1);

			// Map input values from [0, 1] to [MinVolume, MaxVolume]
			return (value * (MaxVolume - MinVolume)) + MinVolume;
		}
	}
}
