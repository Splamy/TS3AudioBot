namespace TS3AudioBot.Algorithm
{
	interface IShuffleAlgorithm
	{
		void SetData(int length);
		void SetData(int seed, int length);

		int Next();
		int Prev();
	}
}
