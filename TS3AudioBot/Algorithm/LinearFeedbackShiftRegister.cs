// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.Algorithm
{
	using System;
	using Helper;

	public class LinearFeedbackShiftRegister : IShuffleAlgorithm
	{
		private int register = 1;
		private int mask = 0;
		public int Seed { get; private set; }
		public int Length { get; private set; }
		public int Index
		{
			get { return Util.MathMod(register + Seed, Length); }
			set { register = Util.MathMod(value - Seed, Length); }
		}

		public void Set(int seed, int length)
		{
			if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

			Seed = seed;
			Length = length;
			register = (register % Length) + 1;

			// get the highest set bit (+1) to hold at least all values with a power of 2
			int maxPow = 31;
			while (((1 << maxPow) & Length) == 0 && maxPow >= 0) maxPow--;
			mask = GenerateGaloisMask(maxPow + 1, seed);
		}

		public void Next()
		{
			do
			{
				register = NextOf(register);
			} while ((uint)register > Length);
		}

		public void Prev()
		{
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

				if (TestLFSR(checkMask, end))
					return checkMask;
			}
			throw new InvalidOperationException();
		}

		private static bool TestLFSR(int mask, int max)
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
