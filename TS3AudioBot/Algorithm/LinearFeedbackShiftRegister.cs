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
	using System.Collections;

	public class LinearFeedbackShiftRegister : IShuffleAlgorithm
	{
		private int register = 1;
		private int mask = 0;
		public int Seed { get; private set; }
		public int Length { get; private set; }

		public void Set(int seed, int length)
		{
			if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

			Seed = seed;
			Length = length;
			register = (register % Length) + 1;

			int maxPow = 31;
			while (((1 << maxPow) & Length) == 0 && maxPow >= 0) maxPow--;
			mask = GenerateGaloisMask(maxPow + 1, seed);
		}

		public int Next()
		{
			do
			{
				register = NextOf(register);
			} while ((uint)register > Length);
			return (register + Seed) % Length;
		}

		public int Prev()
		{
			for (int i = 0; i < Length; i++)
				if (NextOf(i) == register)
				{
					register = i;
					return (register + Seed) % Length;
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

		private static readonly int[] GauloiseLFSRs = new int[] { 0001, 0001, 0002, 0002, 0006, 0006, 0018, 0016, 0048, 0060, 0176, 0144, 0630, 0756, 1800, 2048 };
		private static int GenerateGaloisMask(int bits, int seedOffset)
		{
			int start = 1 << (bits - 1);
			int end = 1 << (bits);
			int skipCnt = 0;

			int maxOff = seedOffset % GauloiseLFSRs[Math.Min(bits, GauloiseLFSRs.Length) - 1];
			var bitOks = new BitArray(end, false);
			for (int i = start; i < end; i++)
			{
				if (TestLFSR(bitOks, i, end))
				{
					skipCnt++;
					if (skipCnt >= maxOff)
						return i;
				}
				bitOks.SetAll(false);
			}
			throw new InvalidOperationException();
		}

		private static bool TestLFSR(BitArray bitOks, int mask, int max)
		{
			int field = 1;
			for (uint i = 1; i < max; i++)
			{
				var lsb = field & 1;
				field >>= 1;
				field ^= -lsb & mask;
				if (bitOks.Get(field)) return false;
				bitOks.Set(field, true);
			}
			return true;
		}
	}
}
