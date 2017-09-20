// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Algorithm
{
	using System;
	using Helper;

	public class LinearFeedbackShiftRegister : IShuffleAlgorithm
	{
		private int register = 1; // aka index
		private int mask;
		private int seed;
		private int length;
		private bool needsRefresh = true;

		public int Seed { get => seed; set { needsRefresh = true; seed = value; } }
		public int Length { get => length; set { needsRefresh = true; length = value; } }
		public int Index
		{
			get { if (Length <= 0) return -1; return Util.MathMod(register + Seed, Length); }
			set { if (Length <= 0) return; Recalc(); register = Util.MathMod(value - Seed, Length); }
		}

		private void Recalc()
		{
			if (!needsRefresh) return;
			needsRefresh = false;

			if (Length <= 0) return;
			register = (register % Length) + 1;

			// get the highest set bit (+1) to hold at least all values with a power of 2
			int maxPow = 31;
			while (((1 << maxPow) & Length) == 0 && maxPow >= 0) maxPow--;
			mask = GenerateGaloisMask(maxPow + 1, seed);
		}

		public void Next()
		{
			if (Length <= 0) return;
			Recalc();
			do
			{
				register = NextOf(register);
			} while ((uint)register > Length);
		}

		public void Prev()
		{
			if (Length <= 0) return;
			Recalc();
			for (int i = 0; i < Length; i++)
				if (NextOf(i) == register)
				{
					register = i;
					return;
				}
			throw new InvalidOperationException();
		}

		private int NextOf(int val)
		{
			var lsb = val & 1;
			val >>= 1;
			val ^= -lsb & mask;
			return val;
		}

		private static int GenerateGaloisMask(int bits, int seedOffset)
		{
			if (bits == 1) return 1;
			if (bits == 2) return 3;

			int start = 1 << (bits - 1);
			int end = 1 << (bits);
			int diff = end - start;

			for (int i = 0; i < diff; i++)
			{
				int checkMask = Util.MathMod(i + seedOffset, diff) + start;
				if (NumberOfSetBits(checkMask) % 2 != 0) continue;

				if (TestLfsr(checkMask, end))
					return checkMask;
			}
			throw new InvalidOperationException();
		}

		private static bool TestLfsr(int mask, int max)
		{
			const int start = 1;
			int field = start;

			for (int i = 2; i < max; i++)
			{
				int lsb = field & 1;
				field >>= 1;
				field ^= -lsb & mask;
				if (field == start) return false;
			}
			return true;
		}

		private static int NumberOfSetBits(int i)
		{
			i = i - ((i >> 1) & 0x55555555);
			i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
			return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
		}
	}
}
