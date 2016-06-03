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
	using System.Linq;
	using Helper;

	public class ListedShuffle : IShuffleAlgorithm
	{
		private int[] permutation;

		private int index = 0;
		private int seed = 0;
		private int length = 0;

		public int Seed => seed;
		public int Length => length;

		public void Set(int seed, int length)
		{
			if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

			this.seed = seed;
			this.length = length;
			index %= length;

			GenList();
		}

		private void GenList()
		{
			Random rngeesus = new Random(seed);
			permutation = Enumerable.Range(0, length).Select(i => i).OrderBy(x => rngeesus.Next()).ToArray();
		}

		public int Next() => permutation[(index++) % permutation.Length];
		public int Prev() => permutation[(index--) % permutation.Length];
	}
}
