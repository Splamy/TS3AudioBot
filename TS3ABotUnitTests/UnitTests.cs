using LockCheck;
using NUnit.Framework;
using TS3AudioBot;
using TS3AudioBot.Algorithm;
using System.Collections.Generic;
using System.Linq;
using TS3AudioBot.ResourceFactories;

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
				Assert.Equals(rfac.RestoreLink("robqdGEhQWo"), "https://youtu.be/robqdGEhQWo");
			}
		}
	}
}
