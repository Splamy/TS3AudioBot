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

namespace TS3ABotUnitTests
{
	using LockCheck;
	using NUnit.Framework;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using TS3AudioBot;
	using TS3AudioBot.Algorithm;
	using TS3AudioBot.CommandSystem;
	using TS3AudioBot.Helper;
	using TS3AudioBot.History;
	using TS3AudioBot.ResourceFactories;
	using TS3Client.Messages;
	using TS3Client.Full;

	[TestFixture]
	public class UnitTests
	{
		/* ======================= General Tests ==========================*/

		[Test]
		public void DeadLockCheck()
		{
			var warnings = LockChecker.CheckAll<MainBot>(true);
			Assert.IsTrue(warnings.Count == 0, "At least one possible deadlock detected");
		}

		[Test]
		public void HistoryFileIntergrityTest()
		{
			string testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.test");
			if (File.Exists(testFile)) File.Delete(testFile);


			var inv1 = new ClientData { ClientId = 10, DatabaseId = 101, NickName = "Invoker1" };
			var inv2 = new ClientData { ClientId = 20, DatabaseId = 102, NickName = "Invoker2" };

			var ar1 = new AudioResource("asdf", "sc_ar1", "soundcloud");
			var ar2 = new AudioResource("./File.mp3", "me_ar2", "media");
			var ar3 = new AudioResource("kitty", "tw_ar3", "twitch");

			var data1 = new HistorySaveData(ar1, inv1.DatabaseId);
			var data2 = new HistorySaveData(ar2, inv2.DatabaseId);
			var data3 = new HistorySaveData(ar3, 103);


			var hf = new HistoryManager(new HistoryManagerData() { HistoryFile = testFile, FillDeletedIds = false });

			hf.LogAudioResource(data1);

			var lastXEntries = hf.GetLastXEntrys(1);
			Assert.True(lastXEntries.Any());
			var lastEntry = lastXEntries.First();
			Assert.AreEqual(ar1, lastEntry.AudioResource);

			hf.Dispose();

			hf = new HistoryManager(new HistoryManagerData() { HistoryFile = testFile, FillDeletedIds = false });
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

			hf.Dispose();

			// store and order check
			hf = new HistoryManager(new HistoryManagerData() { HistoryFile = testFile, FillDeletedIds = false });
			var lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar2, lastXEntriesArray[0].AudioResource);
			Assert.AreEqual(ar1, lastXEntriesArray[1].AudioResource);

			var ale1 = hf.FindEntryByResource(ar1);
			hf.RenameEntry(ale1, "sc_ar1X");
			hf.LogAudioResource(new HistorySaveData(ale1.AudioResource, 42));


			hf.Dispose();

			// check entry renaming
			hf = new HistoryManager(new HistoryManagerData() { HistoryFile = testFile, FillDeletedIds = false });
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar1, lastXEntriesArray[0].AudioResource);
			Assert.AreEqual(ar2, lastXEntriesArray[1].AudioResource);

			var ale2 = hf.FindEntryByResource(ar2);
			hf.RenameEntry(ale2, "me_ar2_loong1");
			hf.LogAudioResource(new HistorySaveData(ale2.AudioResource, 42));

			ale1 = hf.FindEntryByResource(ar1);
			hf.RenameEntry(ale1, "sc_ar1X_loong1");
			hf.LogAudioResource(new HistorySaveData(ale1.AudioResource, 42));

			hf.RenameEntry(ale2, "me_ar2_exxxxxtra_loong1");
			hf.LogAudioResource(new HistorySaveData(ale2.AudioResource, 42));

			hf.Dispose();

			// recheck order
			hf = new HistoryManager(new HistoryManagerData() { HistoryFile = testFile, FillDeletedIds = false });
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar2, lastXEntriesArray[0].AudioResource);
			Assert.AreEqual(ar1, lastXEntriesArray[1].AudioResource);
			hf.Dispose();

			// delete entry 1
			hf = new HistoryManager(new HistoryManagerData() { HistoryFile = testFile, FillDeletedIds = false });
			hf.RemoveEntry(hf.FindEntryByResource(ar1));

			lastXEntriesArray = hf.GetLastXEntrys(3).ToArray();
			Assert.AreEqual(1, lastXEntriesArray.Length);

			// .. store new entry to check correct stream position writes
			hf.LogAudioResource(data3);

			lastXEntriesArray = hf.GetLastXEntrys(3).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			hf.Dispose();

			// delete entry 2
			hf = new HistoryManager(new HistoryManagerData() { HistoryFile = testFile, FillDeletedIds = false });
			// .. check integrity from previous store
			lastXEntriesArray = hf.GetLastXEntrys(3).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);

			// .. delete and recheck
			hf.RemoveEntry(hf.FindEntryByResource(ar2));

			lastXEntriesArray = hf.GetLastXEntrys(3).ToArray();
			Assert.AreEqual(1, lastXEntriesArray.Length);
			Assert.AreEqual(ar3, lastXEntriesArray[0].AudioResource);
			hf.Dispose();


			File.Delete(testFile);
		}

		[Test]
		public void PositionedStreamReaderLineEndings()
		{
			using (var memStream = new MemoryStream())
			{
				// Setting streams up
				var writer = new StreamWriter(memStream);
				string[] values = {
					"11\n",
					"22\n",
					"33\n",
					"44\r",
					"55\r",
					"66\r",
					"77\r\n",
					"88\r\n",
					"99\r\n",
					"xx\n","\r",
					"yy\n","\r",
					"zz\n","\r",
					"a\r",
					"b\n",
					"c\r\n",
					"d\n","\r",
					"e" };
				foreach (var val in values)
					writer.Write(val);
				writer.Flush();

				memStream.Seek(0, SeekOrigin.Begin);
				var reader = new PositionedStreamReader(memStream);

				int pos = 0;
				foreach (var val in values)
				{
					var line = reader.ReadLine();
					pos += val.Length;

					Assert.AreEqual(val.TrimEnd(new[] { '\r', '\n' }), line);
					Assert.AreEqual(pos, reader.ReadPosition);
				}
			}
		}

		[Test]
		public void PositionedStreamReaderBufferSize()
		{
			using (var memStream = new MemoryStream())
			{
				// Setting streams up
				var writer = new StreamWriter(memStream);
				string[] values = new[] {
					new string('1', 1024) + '\n', // 1025: 1 over buffer size
					new string('1', 1023) + '\n', // 1024: exactly the buffer size, but 1 over the 1024 line block due to the previous
					new string('1', 1022) + '\n', // 1023: 1 less then the buffer size, should now match the line block again
					new string('1', 1024) };
				foreach (var val in values)
					writer.Write(val);
				writer.Flush();

				memStream.Seek(0, SeekOrigin.Begin);
				var reader = new PositionedStreamReader(memStream);

				int pos = 0;
				foreach (var val in values)
				{
					var line = reader.ReadLine();
					pos += val.Length;

					Assert.AreEqual(val.TrimEnd(new[] { '\r', '\n' }), line);
					Assert.AreEqual(pos, reader.ReadPosition);
				}
			}
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
		public void XCommandSystemFilterTest()
		{
			var filterList = new Dictionary<string, object>
			{
				{ "help", null },
				{ "quit", null },
				{ "play", null },
				{ "ply", null }
			};

			// Exact match
			var result = XCommandSystem.FilterList(filterList, "help");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("help", result.First().Key);

			// The first occurence of y
			result = XCommandSystem.FilterList(filterList, "y");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("ply", result.First().Key);

			// The smallest word
			result = XCommandSystem.FilterList(filterList, "zorn");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("ply", result.First().Key);

			// First letter match
			result = XCommandSystem.FilterList(filterList, "q");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("quit", result.First().Key);

			// Ignore other letters
			result = XCommandSystem.FilterList(filterList, "palyndrom");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("play", result.First().Key);

			filterList.Add("pla", null);

			// Ambiguous command
			result = XCommandSystem.FilterList(filterList, "p");
			Assert.AreEqual(2, result.Count());
			Assert.IsTrue(result.Any(r => r.Key == "ply"));
			Assert.IsTrue(result.Any(r => r.Key == "pla"));
		}

		[Test]
		public void XCommandSystemTest()
		{
			var commandSystem = new XCommandSystem();
			var group = commandSystem.RootCommand;
			group.AddCommand("one", new FunctionCommand(() => "ONE"));
			group.AddCommand("two", new FunctionCommand(() => "TWO"));
			group.AddCommand("echo", new FunctionCommand(s => s));
			group.AddCommand("optional", new FunctionCommand(new Func<string, string>(s => s == null ? "NULL" : "NOT NULL")).SetRequiredParameters(0));

			// Basic tests
			Assert.AreEqual("ONE", ((StringCommandResult)commandSystem.Execute(ExecutionInformation.Debug,
				 new ICommand[] { new StringCommand("one") })).Content);
			Assert.AreEqual("ONE", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!one"));
			Assert.AreEqual("TWO", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!t"));
			Assert.AreEqual("TEST", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!e TEST"));
			Assert.AreEqual("ONE", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!o"));

			// Optional parameters
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!e"));
			Assert.AreEqual("NULL", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!op"));
			Assert.AreEqual("NOT NULL", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!op 1"));

			// Command chaining
			Assert.AreEqual("TEST", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!e (!e TEST)"));
			Assert.AreEqual("TWO", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!e (!t)"));
			Assert.AreEqual("NOT NULL", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!op (!e TEST)"));
			Assert.AreEqual("ONE", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!(!e on)"));

			// Command overloading
			var intCom = new Func<int, string>((int i) => "INT");
			var strCom = new Func<string, string>((string s) => "STRING");
			group.AddCommand("overlord", new OverloadedFunctionCommand(new[] {
				new FunctionCommand(intCom.Method, intCom.Target),
				new FunctionCommand(strCom.Method, strCom.Target)
			}));

			Assert.AreEqual("INT", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!overlord 1"));
			Assert.AreEqual("STRING", commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!overlord a"));
			Assert.Throws<CommandException>(() => commandSystem.ExecuteCommand(ExecutionInformation.Debug, "!overlord"));
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
			using (IResourceFactory rfac = new YoutubeFactory(new YoutubeFactoryData()))
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
		public void Ts3Client_RingQueueTest()
		{
			int ov;
			var q = new RingQueue<int>(3, 5);

			q.Set(0, 42);

			Assert.True(q.TryPeekStart(0, out ov));
			Assert.AreEqual(ov, 42);

			q.Set(1, 43);

			// already set
			Assert.Throws<ArgumentOutOfRangeException>(() => q.Set(1, 99));

			Assert.True(q.TryPeekStart(0, out ov));
			Assert.AreEqual(ov, 42);
			Assert.True(q.TryPeekStart(1, out ov));
			Assert.AreEqual(ov, 43);

			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 42);

			Assert.True(q.TryPeekStart(0, out ov));
			Assert.AreEqual(ov, 43);
			Assert.False(q.TryPeekStart(1, out ov));

			q.Set(3, 45);
			q.Set(2, 44);

			// buffer overfull
			Assert.Throws<ArgumentOutOfRangeException>(() => q.Set(4, 99));

			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 43);
			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 44);

			q.Set(4, 46);

			// out of mod range
			Assert.Throws<ArgumentOutOfRangeException>(() => q.Set(5, 99));

			q.Set(0, 47);

			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 45);
			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 46);
			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 47);

			q.Set(2, 49);

			Assert.False(q.TryDequeue(out ov));

			q.Set(1, 48);

			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 48);
			Assert.True(q.TryDequeue(out ov));
			Assert.AreEqual(ov, 49);
		}

		[Test]
		public void Ts3Client_RingQueueTest2()
		{
			int ov;
			var q = new RingQueue<int>(50, ushort.MaxValue + 1);

			for (int i = 0; i < ushort.MaxValue - 10; i++)
			{
				q.Set(i, 42);
				q.TryDequeue(out ov);
			}

			var setStatus = q.IsSet(ushort.MaxValue - 20);
			Assert.True(setStatus.HasFlag(ItemSetStatus.Set));

			for (int i = ushort.MaxValue - 10; i < ushort.MaxValue + 10; i++)
			{
				q.Set(i % (ushort.MaxValue + 1), 42);
			}
		}
	}

	static class Extensions
	{
		public static IEnumerable<AudioLogEntry> GetLastXEntrys(this HistoryManager hf, int num)
		{
			return hf.Search(new SeachQuery { MaxResults = num });
		}
	}
}
