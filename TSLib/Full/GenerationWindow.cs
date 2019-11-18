// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Full
{
	public sealed class GenerationWindow
	{
		public int MappedBaseOffset { get; set; }
		public uint Generation { get; set; }
		public int Mod { get; }
		public int ReceiveWindow { get; }

		public GenerationWindow(int mod, int? windowSize = null)
		{
			Mod = mod;
			ReceiveWindow = windowSize ?? (Mod / 2);
		}

		public bool SetAndDrag(int mappedValue)
		{
			var inWindow = IsInWindow(mappedValue);
			if (inWindow)
				AdvanceToExcluded(mappedValue);
			return inWindow;
		}

		public void Advance(int amount)
		{
			if (amount > Mod)
				throw new Exception("Cannot advance more than one generation");
			if (amount < 0)
				throw new Exception("Cannot advance backwards");
			if (amount == 0)
				return;
			int newBaseOffset = MappedBaseOffset + amount;
			if (newBaseOffset >= Mod)
			{
				Generation += (uint)(newBaseOffset / Mod);
				newBaseOffset %= Mod;
			}
			MappedBaseOffset = newBaseOffset;
		}

		public void AdvanceToExcluded(int mappedValue)
		{
			var moveDist = (mappedValue - MappedBaseOffset) + 1;
			if (moveDist <= 0)
				return;
			Advance(moveDist);
		}

		public bool IsInWindow(int mappedValue)
		{
			int maxOffset = MappedBaseOffset + ReceiveWindow;
			if (maxOffset < Mod)
			{
				return mappedValue >= MappedBaseOffset && mappedValue < maxOffset;
			}
			else
			{
				return mappedValue >= MappedBaseOffset || mappedValue < maxOffset - Mod;
			}
		}

		public bool IsNextGen(int mappedValue) =>
			   MappedBaseOffset > (Mod - ReceiveWindow)
			&& mappedValue < (MappedBaseOffset + ReceiveWindow) - Mod;

		public uint GetGeneration(int mappedValue) => (uint)(Generation + (IsNextGen(mappedValue) ? 1 : 0));

		public int MappedToIndex(int mappedValue)
		{
			if (mappedValue >= Mod)
				throw new ArgumentOutOfRangeException(nameof(mappedValue));

			if (IsNextGen(mappedValue))
			{
				// | XX             X>    | <= The part from BaseOffset to MappedMod is small enough to consider packets with wrapped numbers again
				//   /\ NewValue    /\ BaseOffset
				return (mappedValue + Mod) - MappedBaseOffset;
			}
			else
			{
				// |  X>             XX   |
				//    /\ BaseOffset  /\ NewValue    // normal case
				return mappedValue - MappedBaseOffset;
			}
		}

		public void Reset()
		{
			MappedBaseOffset = 0;
			Generation = 0;
		}
	}
}
