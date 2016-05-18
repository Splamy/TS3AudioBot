namespace TS3AudioBot.Algorithm
{
	using System;
	using System.Linq;

	class ListedShuffle : IShuffleAlgorithm
	{
		private int[] permutation;

		private int seed = 0;
		private int length = 0;

		public int Seed => seed;

		public void SetData(int length)
		{
			Random rngeesus = new Random();
			SetData(rngeesus.Next(), length);
		}
		public void SetData(int seed, int length)
		{
			this.seed = seed;
			this.length = length;

			if (length != 0)
				GenList();
		}

		private void GenList()
		{
			Random rngeesus = new Random(seed);
			permutation = Enumerable.Range(0, length).Select(i => i).OrderBy(x => rngeesus.Next()).ToArray();
		}

		public int Get(int i) => permutation[i % permutation.Length];
	}
}
