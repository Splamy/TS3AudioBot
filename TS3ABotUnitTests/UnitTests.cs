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
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using LockCheck;
	using NUnit.Framework;

	using TS3AudioBot;
	using TS3AudioBot.Algorithm;
	using TS3AudioBot.Helper;
	using TS3AudioBot.History;
	using TS3AudioBot.ResourceFactories;
	using TS3AudioBot.CommandSystem;

	using TS3Query.Messages;

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


			var inv1 = Generator.ActivateResponse<ClientData>();
			{ inv1.ClientId = 10; inv1.DatabaseId = 101; inv1.NickName = "Invoker1"; }
			var inv2 = Generator.ActivateResponse<ClientData>();
			{ inv2.ClientId = 20; inv2.DatabaseId = 102; inv2.NickName = "Invoker2"; }

			var ar1 = new SoundcloudResource("asdf", "sc_ar1", "https://soundcloud.de/sc_ar1");
			var ar2 = new MediaResource("./File.mp3", "me_ar2", "https://splamy.de/sc_ar2", RResultCode.Success);

			var data1 = new PlayData(null, inv1, "", false) { Resource = ar1, };
			var data2 = new PlayData(null, inv2, "", false) { Resource = ar2, };


			HistoryFile hf = new HistoryFile();
			hf.OpenFile(testFile);

			hf.Store(data1);

			var lastXEntries = hf.GetLastXEntrys(1);
			Assert.True(lastXEntries.Any());
			var lastEntry = lastXEntries.First();
			Assert.AreEqual(ar1, lastEntry);

			hf.CloseFile();

			hf.OpenFile(testFile);
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

			// store and order check
			hf.OpenFile(testFile);
			var lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar1, lastXEntriesArray[0]);
			Assert.AreEqual(ar2, lastXEntriesArray[1]);

			var ale1 = hf.GetEntryById(hf.Contains(ar1).Value);
			hf.LogEntryRename(ale1, "sc_ar1X", false);
			hf.LogEntryPlay(ale1);


			hf.CloseFile();

			// check entry renaming
			hf.OpenFile(testFile);
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar2, lastXEntriesArray[0]);
			Assert.AreEqual(ar1, lastXEntriesArray[1]);

			var ale2 = hf.GetEntryById(hf.Contains(ar2).Value);
			hf.LogEntryRename(ale2, "me_ar2_loong1");
			hf.LogEntryPlay(ale2);

			ale1 = hf.GetEntryById(hf.Contains(ar1).Value);
			hf.LogEntryRename(ale1, "sc_ar1X_loong1");
			hf.LogEntryPlay(ale1);

			hf.LogEntryRename(ale2, "me_ar2_exxxxxtra_loong1");
			hf.LogEntryPlay(ale2);

			hf.CloseFile();

			// reckeck order
			hf.OpenFile(testFile);
			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(2, lastXEntriesArray.Length);
			Assert.AreEqual(ar1, lastXEntriesArray[0]);
			Assert.AreEqual(ar2, lastXEntriesArray[1]);
			hf.CloseFile();

			// delete entry 2
			hf.OpenFile(testFile);
			hf.LogEntryRemove(hf.GetEntryById(hf.Contains(ar2).Value));

			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(1, lastXEntriesArray.Length);
			Assert.AreEqual(ar1, lastXEntriesArray[0]);
			hf.CloseFile();

			// delete entry 1
			hf.OpenFile(testFile);
			hf.LogEntryRemove(hf.GetEntryById(hf.Contains(ar1).Value));

			lastXEntriesArray = hf.GetLastXEntrys(2).ToArray();
			Assert.AreEqual(0, lastXEntriesArray.Length);
			hf.CloseFile();

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

		/* ====================== Algorithm Tests =========================*/

		[Test]
		public void XCommandSystemFilterTest()
		{
			var filterList = new Dictionary<string, object>();
			filterList.Add("help", null);
			filterList.Add("quit", null);
			filterList.Add("play", null);
			filterList.Add("ply", null);

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
			Assert.IsTrue(result.Where(r => r.Key == "ply").Any());
			Assert.IsTrue(result.Where(r => r.Key == "pla").Any());
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
