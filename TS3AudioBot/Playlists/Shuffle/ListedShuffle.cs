// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Linq;
using TSLib.Helper;

namespace TS3AudioBot.Playlists.Shuffle
{
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
			set { needsRecalc |= seed != value; seed = value; }
		}
		public int Length
		{
			get => length;
			set { needsRecalc |= length != value; length = value; }
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
				index = Array.IndexOf(permutation, Tools.MathMod(value, permutation.Length));
			}
		}

		private void GenList()
		{
			if (!needsRecalc) return;
			needsRecalc = false;

			if (Length <= 0) return;

			var rngeesus = new Random(seed);
			permutation = Enumerable.Range(0, length).Select(i => i).OrderBy(_ => rngeesus.Next()).ToArray();
			index %= Length;
		}

		public bool Next()
		{
			if (Length <= 0)
				return false;
			GenList();
			index = (index + 1) % permutation.Length;
			return index == 0;
		}
		public bool Prev()
		{
			if (Length <= 0)
				return false;
			GenList();
			index = ((index - 1) % permutation.Length + permutation.Length) % permutation.Length;
			return index == permutation.Length - 1;
		}
	}
}
