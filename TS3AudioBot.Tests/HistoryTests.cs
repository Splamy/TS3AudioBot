using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TS3AudioBot;
using TS3AudioBot.Config;
using TS3AudioBot.History;
using TS3AudioBot.ResourceFactories;
using TSLib;

namespace TS3AudioBot.Tests;

public class HistoryTests
{
	[Fact]
	public void HistoryFileIntegrityTest()
	{
		string testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.test");
		if (File.Exists(testFile)) File.Delete(testFile);

		var inv1 = new { ClientId = (ClientId)10, Uid = (Uid)"Uid1", Name = "Invoker1" };
		var inv2 = new { ClientId = (ClientId)20, Uid = (Uid)"Uid2", Name = "Invoker2" };

		var ar1 = new AudioResource("asdf", "sc_ar1", "soundcloud");
		var ar2 = new AudioResource("./File.mp3", "me_ar2", "media");
		var ar3 = new AudioResource("kitty", "tw_ar3", "twitch");

		var data1 = new HistorySaveData(ar1, inv1.Uid);
		var data2 = new HistorySaveData(ar2, inv2.Uid);
		var data3 = new HistorySaveData(ar3, (Uid)"Uid3");

		var confDb = ConfigEnumerable.CreateRoot<ConfDb>();
		confDb.Path.Value = testFile;

		DbStore db;
		HistoryManager hf;

		void CreateDbStore()
		{
			db = new DbStore(confDb);
			hf = new HistoryManager(db);
		}

		CreateDbStore();

		hf.LogAudioResourceDelayed(data1);

		var lastXEntries = hf.GetLastXEntries(1);
		var lastEntry = Assert.Single(lastXEntries);
		Assert.Equal(ar1, lastEntry.AudioResource);

		db.Dispose();

		CreateDbStore();
		lastXEntries = hf.GetLastXEntries(1);
		lastEntry = Assert.Single(lastXEntries);
		Assert.Equal(ar1, lastEntry.AudioResource);

		hf.LogAudioResourceDelayed(data1);
		hf.LogAudioResourceDelayed(data2);

		lastXEntries = hf.GetLastXEntries(1);
		lastEntry = Assert.Single(lastXEntries);
		Assert.Equal(ar2, lastEntry.AudioResource);

		db.Dispose();

		// store and order check
		CreateDbStore();
		var lastXEntriesArray = hf.GetLastXEntries(2).ToArray();
		Assert.Equal(2, lastXEntriesArray.Length);
		Assert.Equal(ar2, lastXEntriesArray[0].AudioResource);
		Assert.Equal(ar1, lastXEntriesArray[1].AudioResource);

		var ale1 = hf.FindEntryByResource(ar1);
		hf.RenameEntry(ale1, "sc_ar1X");
		hf.LogAudioResourceDelayed(new HistorySaveData(ale1.AudioResource, (Uid)"Uid4"));

		db.Dispose();

		// check entry renaming
		CreateDbStore();
		lastXEntriesArray = hf.GetLastXEntries(2).ToArray();
		Assert.Equal(2, lastXEntriesArray.Length);
		Assert.Equal(ar1, lastXEntriesArray[0].AudioResource);
		Assert.Equal(ar2, lastXEntriesArray[1].AudioResource);

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
		lastXEntriesArray = hf.GetLastXEntries(2).ToArray();
		Assert.Equal(2, lastXEntriesArray.Length);
		Assert.Equal(ar2, lastXEntriesArray[0].AudioResource);
		Assert.Equal(ar1, lastXEntriesArray[1].AudioResource);
		db.Dispose();

		// delete entry 1
		CreateDbStore();
		hf.RemoveEntry(hf.FindEntryByResource(ar1));

		lastXEntriesArray = hf.GetLastXEntries(3).ToArray();
		Assert.Single(lastXEntriesArray);

		// .. store new entry to check correct stream position writes
		hf.LogAudioResourceDelayed(data3);

		lastXEntriesArray = hf.GetLastXEntries(3).ToArray();
		Assert.Equal(2, lastXEntriesArray.Length);
		db.Dispose();

		// delete entry 2
		CreateDbStore();
		// .. check integrity from previous store
		lastXEntriesArray = hf.GetLastXEntries(3).ToArray();
		Assert.Equal(2, lastXEntriesArray.Length);

		// .. delete and recheck
		hf.RemoveEntry(hf.FindEntryByResource(ar2));

		lastXEntriesArray = hf.GetLastXEntries(3).ToArray();
		Assert.Single(lastXEntriesArray);
		Assert.Equal(ar3, lastXEntriesArray[0].AudioResource);
		db.Dispose();

		// Cleanup
		File.Delete(testFile);
	}
}

internal static class Extensions
{
	extension(HistoryManager hm)
	{
		public IEnumerable<AudioLogEntry> GetLastXEntries(int num)
		{
			return hm.Search(new SearchQuery { MaxResults = num });
		}

		public void LogAudioResourceDelayed(HistorySaveData data)
		{
			Thread.Sleep(1);
			hm.LogAudioResource(data);
		}
	}
}
