using System.Collections.Generic;
using System.IO;
using System.Linq;
using LockCheck;
using NUnit.Framework;
using TS3AudioBot;
using TS3AudioBot.Algorithm;
using TS3AudioBot.History;
using TS3AudioBot.ResourceFactories;
using TS3Query.Messages;

namespace TS3ABotUnitTests
{
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
			if (File.Exists("test.txt")) File.Delete("test.txt");


			var inv1 = Generator.ActivateResponse<ClientData>();
			{ inv1.ClientId = 10; inv1.DatabaseId = 101; inv1.NickName = "Invoker1"; }
			var inv2 = Generator.ActivateResponse<ClientData>();
			{ inv2.ClientId = 20; inv2.DatabaseId = 102; inv2.NickName = "Invoker2"; }

			var ar1 = new SoundcloudResource("asdf", "sc_ar1", "https://soundcloud.de/sc_ar1");
			var ar2 = new MediaResource("./File.mp3", "me_ar2", "https://splamy.de/sc_ar2", RResultCode.Success);

			var data1 = new PlayData(null, inv1, "", false) { Resource = ar1, };
			var data2 = new PlayData(null, inv2, "", false) { Resource = ar2, };


			HistoryFile hf = new HistoryFile();
			hf.OpenFile("test.txt");

			hf.Store(data1);

			var lastXEntries = hf.GetLastXEntrys(1);
			Assert.True(lastXEntries.Any());
			var lastEntry = lastXEntries.First();
			Assert.AreEqual(ar1, lastEntry);

			hf.CloseFile();

			hf.OpenFile("test.txt");
			lastXEntries = hf.GetLastXEntrys(1);
			Assert.True(lastXEntries.Any());
			lastEntry = lastXEntries.First();
			Assert.AreEqual(ar1, lastEntry);

			hf.Store(data1);
			hf.Store(data2);

			lastXEntries = hf.GetLastXEntrys(1);
			Assert.True(lastXEntries.Any());
			lastEntry = lastXEntries.First();
			Assert.AreEqual(ar2, lastEntry);

			hf.CloseFile();

			hf.OpenFile("test.txt");
			var lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar1, lastXEntriesArray[0]);
			Assert.AreEqual(ar2, lastXEntriesArray[1]);

			ar1.ResourceTitle = "sc_ar1X";
			hf.Store(data1);

			hf.CloseFile();

			hf.OpenFile("test.txt");
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar2, lastXEntriesArray[0]);
			Assert.AreEqual(ar1, lastXEntriesArray[1]);

			ar2.ResourceTitle = "me_ar2_loong1";
			hf.Store(data2);

			ar1.ResourceTitle = "sc_ar1X_loong1";
			hf.Store(data1);

			ar2.ResourceTitle = "me_ar2_exxxxxtra_loong1";
			hf.Store(data2);

			hf.CloseFile();

			Assert.DoesNotThrow(() => hf.OpenFile("test.txt"));
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar1, lastXEntriesArray[0]);
			Assert.AreEqual(ar2, lastXEntriesArray[1]);

			File.Delete("test.txt");
		}

		// TODO positionfilereader test

		/* ====================== Algorithm Tests =========================*/

		[Test]
		public void TrieStructureTests()
		{
			var trie = new Trie<string>();
			string[] values = new string[] { "val1", "val2", "val3", "val4", "val5" };
			int adix = 0;

			trie.Add("hans", values[adix++]);
			Assert.AreEqual(string.Format("+(h*(a*(n*(s[{0}]))))", values), trie.ToString());
			trie.Add("hani", values[adix++]);
			Assert.AreEqual(string.Format("+(h(a(n(i[{1}]s[{0}]))))", values), trie.ToString());
			trie.Add("hana", values[adix++]);
			Assert.AreEqual(string.Format("+(h(a(n(a[{2}]i[{1}]s[{0}]))))", values), trie.ToString());
			trie.Add("hansolo", values[adix++]);
			Assert.AreEqual(string.Format("+(h(a(n(a[{2}]i[{1}]s[{0}](o*(l*(o[{3}])))))))", values), trie.ToString());
			trie.Add("hansololo", values[adix++]);
			Assert.AreEqual(string.Format("+(h(a(n(a[{2}]i[{1}]s[{0}](o(l(o[{3}](l*(o[{4}])))))))))", values), trie.ToString());
		}

		[Test]
		public void XCommandFilterTest()
		{
			XCommandFilter<string> filter = new XCommandFilter<string>();
			filter.Add("help", "HELP");
			filter.Add("quit", "QUIT");
			filter.Add("play", "PLAY");
			filter.Add("ply", "PLY");
			string result;
			Assert.IsTrue(filter.TryGetValue("help", out result));
			Assert.AreEqual("HELP", result);
			Assert.IsTrue(filter.TryGetValue("y", out result));
			Assert.AreEqual("PLY", result);
			Assert.IsFalse(filter.TryGetValue("zorn", out result));
			Assert.AreEqual(default(string), result);
			Assert.IsTrue(filter.TryGetValue("q", out result));
			Assert.AreEqual("QUIT", result);
			Assert.IsTrue(filter.TryGetValue("palyndrom", out result));
			Assert.AreEqual("PLAY", result);
		}

		[Test]
		public void XCommandSystemTest()
		{
			var group = new CommandGroup();
			group.AddCommand("one", new FunctionCommand(() => { return "Called one"; }));
			var commandSystem = new XCommandSystem(group);
			Assert.AreEqual("Called one", ((StringCommandResult) commandSystem.Execute(new ExecutionInformation(),
                 new StaticEnumerableCommandResult(new ICommandResult[] { new StringCommandResult("one") }))).Content);
		}

		[Test]
		public void SimpleSubstringFinderTest()
		{
			var subf = new SimpleSubstringFinder<string>();
			TestISubstringFinder(subf);
		}

		public void TestISubstringFinder(ISubstringSearch<string> subf)
		{
			subf.Add("thisIsASongName", "1");
			subf.Add("abcdefghijklmnopqrstuvwxyz", "2");
			subf.Add("123456789song!@#$%^&*()_<>?|{}", "3");
			subf.Add("SHOUTING SONG", "4");
			subf.Add("not shouting song", "5");
			subf.Add("http://test.song.123/text?var=val&format=mp3", "6");
			subf.Add("...........a...........", "7");

			var res = subf.GetValues("song");
			Assert.True(HaveSameItems(res, new[] { "1", "3", "4", "5", "6" }));
			res = subf.GetValues("shouting");
			Assert.True(HaveSameItems(res, new[] { "4", "5" }));
			res = subf.GetValues("this");
			Assert.True(HaveSameItems(res, new[] { "1" }));
			res = subf.GetValues("a");
			Assert.True(HaveSameItems(res, new[] { "1", "2", "6", "7" }));
			res = subf.GetValues(string.Empty);
			Assert.True(HaveSameItems(res, new[] { "1", "2", "3", "4", "5", "6", "7" }));
			res = subf.GetValues("zzzzzzzzzzzzzzzzz");
			Assert.True(HaveSameItems(res, new string[0]));
		}

		static bool HaveSameItems<T>(IEnumerable<T> self, IEnumerable<T> other) => !other.Except(self).Any() && !self.Except(other).Any();

		/* =================== ResourceFactories Tests =====================*/

		[Test]
		public void Factory_YoutubeFactoryTest()
		{
			using (IResourceFactory rfac = new YoutubeFactory())
			{
				// matching links
				Assert.True(rfac.MatchLink(@"https://www.youtube.com/watch?v=robqdGEhQWo"));
				Assert.True(rfac.MatchLink(@"https://youtu.be/robqdGEhQWo"));
				Assert.False(rfac.MatchLink(@"https://discarded-ideas.org/sites/discarded-ideas.org/files/music/darkforestkeep_symphonic.mp3"));
				Assert.False(rfac.MatchLink(@"http://splamy.de/youtube.com/youtu.be/fake.mp3"));

				// restoring links
				Assert.AreEqual("https://youtu.be/robqdGEhQWo", rfac.RestoreLink("robqdGEhQWo"));
			}
		}
	}
}
