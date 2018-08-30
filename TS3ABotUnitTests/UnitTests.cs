// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3ABotUnitTests
{
	using NUnit.Framework;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using TS3AudioBot;
	using TS3AudioBot.Config;
	using TS3AudioBot.Helper;
	using TS3AudioBot.History;
	using TS3AudioBot.Playlists.Shuffle;
	using TS3AudioBot.ResourceFactories;
	using TS3Client.Full;
	using TS3Client.Messages;

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

			var inv1 = new ClientData { ClientId = 10, Uid = "Uid1", Name = "Invoker1" };
			var inv2 = new ClientData { ClientId = 20, Uid = "Uid2", Name = "Invoker2" };

			var ar1 = new AudioResource("asdf", "sc_ar1", "soundcloud");
			var ar2 = new AudioResource("./File.mp3", "me_ar2", "media");
			var ar3 = new AudioResource("kitty", "tw_ar3", "twitch");

			var data1 = new HistorySaveData(ar1, inv1.Uid);
			var data2 = new HistorySaveData(ar2, inv2.Uid);
			var data3 = new HistorySaveData(ar3, "Uid3");

			var confHistory = ConfigTable.CreateRoot<ConfHistory>();
			confHistory.FillDeletedIds.Value = false;
			var confDb = ConfigTable.CreateRoot<ConfDb>();
			confDb.Path.Value = testFile;

			DbStore db;
			HistoryManager hf;

			void CreateDbStore()
			{
				db = new DbStore(confDb);
				hf = new HistoryManager(confHistory) { Database = db };
				hf.Initialize();
			}

			CreateDbStore();

			hf.LogAudioResource(data1);

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

			hf.LogAudioResource(data1);
			hf.LogAudioResource(data2);

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
			hf.LogAudioResource(new HistorySaveData(ale1.AudioResource, "Uid4"));


			db.Dispose();

			// check entry renaming
			CreateDbStore();
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar1, lastXEntriesArray[0].AudioResource);
			Assert.AreEqual(ar2, lastXEntriesArray[1].AudioResource);

			var ale2 = hf.FindEntryByResource(ar2);
			hf.RenameEntry(ale2, "me_ar2_loong1");
			hf.LogAudioResource(new HistorySaveData(ale2.AudioResource, "Uid4"));

			ale1 = hf.FindEntryByResource(ar1);
			hf.RenameEntry(ale1, "sc_ar1X_loong1");
			hf.LogAudioResource(new HistorySaveData(ale1.AudioResource, "Uid4"));

			hf.RenameEntry(ale2, "me_ar2_exxxxxtra_loong1");
			hf.LogAudioResource(new HistorySaveData(ale2.AudioResource, "Uid4"));

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
			hf.LogAudioResource(data3);

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
			TestShuffleAlgorithm(new NormalOrder());
		}

		[Test]
		public void ListedShuffleTest()
		{
			TestShuffleAlgorithm(new ListedShuffle());
		}

		[Test]
		public void LinearFeedbackShiftRegisterTest()
		{
			TestShuffleAlgorithm(new LinearFeedbackShiftRegister());
		}

		private static void TestShuffleAlgorithm(IShuffleAlgorithm algo)
		{
			for (int i = 1; i < 1000; i++)
			{
				var checkNumbers = new BitArray(i, false);

				algo.Length = i;
				algo.Seed = i;

				for (int j = 0; j < i; j++)
				{
					algo.Next();
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
			using (IResourceFactory rfac = new YoutubeFactory())
			{
				// matching links
				Assert.AreEqual(rfac.MatchResource(@"https://www.youtube.com/watch?v=robqdGEhQWo"), MatchCertainty.Always);
				Assert.AreEqual(rfac.MatchResource(@"https://youtu.be/robqdGEhQWo"), MatchCertainty.Always);
				Assert.AreEqual(rfac.MatchResource(@"https://discarded-ideas.org/sites/discarded-ideas.org/files/music/darkforestkeep_symphonic.mp3"), MatchCertainty.Never);
				Assert.AreNotEqual(rfac.MatchResource(@"http://splamy.de/youtube.com/youtu.be/fake.mp3"), MatchCertainty.Always);

				// restoring links
				Assert.AreEqual("https://youtu.be/robqdGEhQWo", rfac.RestoreLink("robqdGEhQWo"));
			}
		}

		/* ======================= TS3Client Tests ========================*/

		[Test]
		public void VersionSelfCheck()
		{
			Ts3Crypt.VersionSelfCheck();
		}
	}

	internal static class Extensions
	{
		public static IEnumerable<AudioLogEntry> GetLastXEntrys(this HistoryManager hf, int num)
		{
			return hf.Search(new SeachQuery { MaxResults = num });
		}
	}
}
