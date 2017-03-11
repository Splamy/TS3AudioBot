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

		private bool needsRecalc = true;
		private int index = 0;
		private int seed = 0;
		private int length = 0;

		public int Seed
		{
			get { return seed; }
			set { needsRecalc = true; seed = value; }
		}
		public int Length
		{
			get { return length; }
			set { needsRecalc = true; length = value; }
		}
		public int Index
		{
			get
			{
				if (Length <= 0) return -1;
				GenList();
				return permutation[index];
			}
			set
			{
				if (Length <= 0) return;
				GenList();
				index = Array.IndexOf(permutation, Util.MathMod(value, permutation.Length));
			}
		}

		private void GenList()
		{
			if (!needsRecalc) return;
			needsRecalc = false;

			if (Length <= 0) return;

			var rngeesus = new Random(seed);
			permutation = Enumerable.Range(0, length).Select(i => i).OrderBy(x => rngeesus.Next()).ToArray();
			index %= Length;
		}

		public void Next() { if (Length <= 0) return; GenList(); index = (index + 1) % permutation.Length; }
		public void Prev() { if (Length <= 0) return; GenList(); index = (index - 1) % permutation.Length; }
	}
}
