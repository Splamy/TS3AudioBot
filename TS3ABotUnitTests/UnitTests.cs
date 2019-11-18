using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using TS3AudioBot;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.History;
using TS3AudioBot.Playlists.Shuffle;
using TS3AudioBot.ResourceFactories;
using TSLib;
using TSLib.Full;
using TSLib.Messages;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class UnitTests
	{
		// ReSharper disable PossibleMultipleEnumeration

		/* ======================= General Tests ==========================*/

		[Test]
		public void HistoryFileIntergrityTest()
		{
			string testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.test");
			if (File.Exists(testFile)) File.Delete(testFile);

			var inv1 = new ClientList { ClientId = (ClientId)10, Uid = (Uid)"Uid1", Name = "Invoker1" };
			var inv2 = new ClientList { ClientId = (ClientId)20, Uid = (Uid)"Uid2", Name = "Invoker2" };

			var ar1 = new AudioResource("asdf", "sc_ar1", "soundcloud");
			var ar2 = new AudioResource("./File.mp3", "me_ar2", "media");
			var ar3 = new AudioResource("kitty", "tw_ar3", "twitch");

			var data1 = new HistorySaveData(ar1, inv1.Uid);
			var data2 = new HistorySaveData(ar2, inv2.Uid);
			var data3 = new HistorySaveData(ar3, (Uid)"Uid3");

			var confHistory = ConfigTable.CreateRoot<ConfHistory>();
			confHistory.FillDeletedIds.Value = false;
			var confDb = ConfigTable.CreateRoot<ConfDb>();
			confDb.Path.Value = testFile;

			DbStore db;
			HistoryManager hf;

			void CreateDbStore()
			{
				db = new DbStore(confDb);
				hf = new HistoryManager(confHistory, db);
			}

			CreateDbStore();

			hf.LogAudioResourceDelayed(data1);

			var lastXEntries = hf.GetLastXEntrys(1);
			Assert.True(lastXEntries.Any());
			var lastEntry = lastXEntries.First();
			Assert.AreEqual(ar1, lastEntry.AudioResource);

			db.Dispose();

			CreateDbStore();
			lastXEntries = hf.GetLastXEntrys(1);
			Assert.True(lastXEntries.Any());
			lastEntry = lastXEntries.First();
			Assert.AreEqual(ar1, lastEntry.AudioResource);

			hf.LogAudioResourceDelayed(data1);
			hf.LogAudioResourceDelayed(data2);

			lastXEntries = hf.GetLastXEntrys(1);
			Assert.True(lastXEntries.Any());
			lastEntry = lastXEntries.First();
			Assert.AreEqual(ar2, lastEntry.AudioResource);

			db.Dispose();

			// store and order check
			CreateDbStore();
			var lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar2, lastXEntriesArray[0].AudioResource);
			Assert.AreEqual(ar1, lastXEntriesArray[1].AudioResource);

			var ale1 = hf.FindEntryByResource(ar1);
			hf.RenameEntry(ale1, "sc_ar1X");
			hf.LogAudioResourceDelayed(new HistorySaveData(ale1.AudioResource, (Uid)"Uid4"));


			db.Dispose();

			// check entry renaming
			CreateDbStore();
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar1, lastXEntriesArray[0].AudioResource);
			Assert.AreEqual(ar2, lastXEntriesArray[1].AudioResource);

			var ale2 = hf.FindEntryByResource(ar2);
			hf.RenameEntry(ale2, "me_ar2_loong1");
			hf.LogAudioResourceDelayed(new HistorySaveData(ale2.AudioResource, (Uid)"Uid4"));

			ale1 = hf.FindEntryByResource(ar1);
			hf.RenameEntry(ale1, "sc_ar1X_loong1");
			hf.LogAudioResourceDelayed(new HistorySaveData(ale1.AudioResource, (Uid)"Uid4"));

			hf.RenameEntry(ale2, "me_ar2_exxxxxtra_loong1");
			hf.LogAudioResourceDelayed(new HistorySaveData(ale2.AudioResource, (Uid)"Uid4"));

			db.Dispose();

			// recheck order
			CreateDbStore();
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar2, lastXEntriesArray[0].AudioResource);
			Assert.AreEqual(ar1, lastXEntriesArray[1].AudioResource);
			db.Dispose();

			// delete entry 1
			CreateDbStore();
			hf.RemoveEntry(hf.FindEntryByResource(ar1));

			lastXEntriesArray = hf.GetLastXEntrys(3).ToArray();
			Assert.AreEqual(1, lastXEntriesArray.Length);

			// .. store new entry to check correct stream position writes
			hf.LogAudioResourceDelayed(data3);

			lastXEntriesArray = hf.GetLastXEntrys(3).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			db.Dispose();

			// delete entry 2
			CreateDbStore();
			// .. check integrity from previous store
			lastXEntriesArray = hf.GetLastXEntrys(3).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);

			// .. delete and recheck
			hf.RemoveEntry(hf.FindEntryByResource(ar2));

			lastXEntriesArray = hf.GetLastXEntrys(3).ToArray();
			Assert.AreEqual(1, lastXEntriesArray.Length);
			Assert.AreEqual(ar3, lastXEntriesArray[0].AudioResource);
			db.Dispose();


			File.Delete(testFile);
		}

		[Test]
		public void UtilSeedTest()
		{
			var lowCaseRegex = new Regex("^[a-z]*$", Util.DefaultRegexConfig & ~RegexOptions.IgnoreCase);
			for (int i = 0; i < 100000; i++)
			{
				var str = Util.FromSeed(i);
				Assert.IsTrue(lowCaseRegex.IsMatch(str), "For seed: " + i);
				var roundtrip = Util.ToSeed(str);
				Assert.AreEqual(i, roundtrip);
			}
		}

		/* ====================== Algorithm Tests =========================*/

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

		/* =================== ResourceFactories Tests ====================*/

		[Test]
		public void Factory_YoutubeFactoryTest()
		{
			using (IResourceResolver rfac = new YoutubeResolver())
			{
				// matching links
				Assert.AreEqual(rfac.MatchResource(@"https://www.youtube.com/watch?v=robqdGEhQWo"), MatchCertainty.Always);
				Assert.AreEqual(rfac.MatchResource(@"https://youtu.be/robqdGEhQWo"), MatchCertainty.Always);
				Assert.AreEqual(rfac.MatchResource(@"https://discarded-ideas.org/sites/discarded-ideas.org/files/music/darkforestkeep_symphonic.mp3"), MatchCertainty.Never);
				Assert.AreNotEqual(rfac.MatchResource(@"http://splamy.de/youtube.com/youtu.be/fake.mp3"), MatchCertainty.Always);

				// restoring links
				Assert.AreEqual("https://youtu.be/robqdGEhQWo", rfac.RestoreLink(new AudioResource { ResourceId = "robqdGEhQWo" }));
			}
		}

		/* ======================= TSLib Tests ========================*/

		[Test]
		public void VersionSelfCheck()
		{
			TsCrypt.VersionSelfCheck();
		}
	}

	internal static class Extensions
	{
		public static IEnumerable<AudioLogEntry> GetLastXEntrys(this HistoryManager hf, int num)
		{
			return hf.Search(new SeachQuery { MaxResults = num });
		}

		public static void LogAudioResourceDelayed(this HistoryManager hf, HistorySaveData data)
		{
			Thread.Sleep(1);
			hf.LogAudioResource(data);
		}
	}
}
