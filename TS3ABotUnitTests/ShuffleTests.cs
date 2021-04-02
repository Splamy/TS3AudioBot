using NUnit.Framework;
using System.Collections;
using TS3AudioBot.Playlists.Shuffle;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class ShuffleTests
	{
		[Test]
		public void NormalOrderTest()
		{
			TestShuffleAlgorithmBiDir(new NormalOrder());
		}

		[Test]
		public void ListedShuffleTest()
		{
			TestShuffleAlgorithmBiDir(new ListedShuffle());
		}

		[Test]
		public void LinearFeedbackShiftRegisterTest()
		{
			TestShuffleAlgorithmBiDir(new LinearFeedbackShiftRegister());
		}

		private static void TestShuffleAlgorithmBiDir(IShuffleAlgorithm algo)
		{
			TestShuffleAlgorithm(algo, true);
			TestShuffleAlgorithm(algo, false);
		}

		private static void TestShuffleAlgorithm(IShuffleAlgorithm algo, bool forward)
		{
			for (int i = 1; i < 1000; i++)
			{
				var checkNumbers = new BitArray(i, false);

				algo.Length = i;
				algo.Seed = i;

				for (int j = 0; j < i; j++)
				{
					if (forward) algo.Next();
					else algo.Prev();
					int shufNum = algo.Index;
					if (checkNumbers.Get(shufNum))
						Assert.Fail("Duplicate number");
					checkNumbers.Set(shufNum, true);
				}
			}
		}
	}
}
