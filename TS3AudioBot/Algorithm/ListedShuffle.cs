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
	using System.Linq;
	using Helper;

	public class ListedShuffle : IShuffleAlgorithm
	{
		private int[] permutation;

		private bool needsRecalc = true;
		private int index;
		private int seed;
		private int length;

		public int Seed
		{
			get => seed;
			set { needsRecalc = true; seed = value; }
		}
		public int Length
		{
			get => length;
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
