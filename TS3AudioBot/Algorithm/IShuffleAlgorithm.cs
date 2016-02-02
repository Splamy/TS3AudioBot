namespace TS3AudioBot.Algorithm
{
	public interface IShuffleAlgorithm
	{
		void SetData(int length);
		void SetData(int seed, int length);

		int NextIndex();
		int PrevIndex();
	}
}
