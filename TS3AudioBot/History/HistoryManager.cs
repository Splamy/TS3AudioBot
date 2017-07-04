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

namespace TS3AudioBot.History
{
	using Helper;
	using LiteDB;
	using ResourceFactories;
	using Sessions;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	public sealed class HistoryManager : IDisposable
	{
		private const string AudioLogEntriesTable = "audioLogEntries";
		private const string ResourceTitleQueryColumn = nameof(AudioLogEntry.AudioResource) + "." + nameof(AudioResource.ResourceTitle);

		private readonly LiteDatabase database;
		private readonly LiteCollection<AudioLogEntry> audioLogEntries;
		private readonly HistoryManagerData historyManagerData;
		private readonly LinkedList<int> unusedIds;

		public IHistoryFormatter Formatter { get; private set; }
		public uint HighestId => (uint)audioLogEntries.Max().AsInt32;

		static HistoryManager()
		{
			BsonMapper.Global.Entity<AudioLogEntry>()
				.Id(x => x.Id, true)
				.Index(x => x.UserInvokeId)
				.Index(x => x.Timestamp);
		}

		public HistoryManager(HistoryManagerData hmd)
		{
			Formatter = new SmartHistoryFormatter();
			historyManagerData = hmd;

			#region CheckUpgrade
			AudioLogEntry[] moveData = null;

			var upgrader = new Deprecated.HistoryFile();
			// check if the old history database system can open it
			try
			{
				upgrader.OpenFile(historyManagerData.HistoryFile);
				Log.Write(Log.Level.Info, "Found old history database vesion, upgrading now.");

				moveData = upgrader
					.GetAll()
					.Select(x => new AudioLogEntry((int)x.Id, x.AudioResource)
					{
						PlayCount = x.PlayCount,
						Timestamp = x.Timestamp,
						UserInvokeId = x.UserInvokeId,
					})
					.ToArray();
				// the old database allowed 0-id, while the new one kinda doesn't
				var nullIdEntity = moveData.FirstOrDefault(x => x.Id == 0);
				if (nullIdEntity != null)
					nullIdEntity.Id = moveData.Select(x => x.Id).Max() + 1;

				upgrader.CloseFile();
				upgrader.BackupFile();
				upgrader.historyFile.Delete();
			}
			// if not it is already the new one or corrupted
			catch (FormatException) { }
			finally
			{
				upgrader.Dispose();
			}
			#endregion

			Util.Init(ref unusedIds);
			var historyFile = new FileInfo(hmd.HistoryFile);
			database = new LiteDatabase(historyFile.FullName);

			audioLogEntries = database.GetCollection<AudioLogEntry>(AudioLogEntriesTable);
			audioLogEntries.EnsureIndex(x => x.AudioResource.ResourceTitle);
			audioLogEntries.EnsureIndex(x => x.AudioResource.UniqueId, true);

			RestoreFromFile();

			#region CheckUpgrade
			if (moveData != null)
				audioLogEntries.Insert(moveData);
			#endregion
		}

		private void RestoreFromFile()
		{
			// TODO load unused id list
		}

		public R<AudioLogEntry> LogAudioResource(HistorySaveData saveData)
		{
			var entry = Store(saveData);
			if (entry != null) return entry;
			else return "Entry could not be stored";
		}

		private AudioLogEntry Store(HistorySaveData saveData)
		{
			if (saveData == null)
				throw new ArgumentNullException(nameof(saveData));

			AudioLogEntry ale;
			using (var trans = database.BeginTrans())
			{
				ale = FindByUniqueId(saveData.Resource.UniqueId);
				if (ale == null)
				{
					ale = CreateLogEntry(saveData);
					if (ale == null)
						Log.Write(Log.Level.Error, "AudioLogEntry could not be created!");
				}
				else
					LogEntryPlay(ale);
				trans.Commit();
			}
			return ale;
		}

		/// <summary>Increases the playcount and updates the last playtime.</summary>
		/// <param name="ale">The <see cref="AudioLogEntry"/> to update.</param>
		private void LogEntryPlay(AudioLogEntry ale)
		{
			if (ale == null)
				throw new ArgumentNullException(nameof(ale));

			// update the playtime
			ale.Timestamp = Util.GetNow();
			// update the playcount
			ale.PlayCount++;

			audioLogEntries.Update(ale);
		}

		private AudioLogEntry CreateLogEntry(HistorySaveData saveData)
		{
			if (string.IsNullOrWhiteSpace(saveData.Resource.ResourceTitle))
				return null;

			int nextHid;
			if (historyManagerData.FillDeletedIds && unusedIds.Any())
			{
				nextHid = unusedIds.First.Value;
				unusedIds.RemoveFirst();
			}
			else
			{
				nextHid = 0;
			}

			var ale = new AudioLogEntry(nextHid, saveData.Resource)
			{
				UserInvokeId = (uint)(saveData.OwnerDbId ?? 0),
				Timestamp = Util.GetNow(),
				PlayCount = 1,
			};

			audioLogEntries.Insert(ale);
			return ale;
		}

		private AudioLogEntry FindByUniqueId(string uniqueId) => audioLogEntries.FindOne(x => x.AudioResource.UniqueId == uniqueId);

		/// <summary>Gets all Entries matching the search criteria.
		/// The entries are sorted by last playtime descending.</summary>
		/// <param name="search">All search criteria.</param>
		/// <returns>A list of all found entries.</returns>
		public IEnumerable<AudioLogEntry> Search(SeachQuery search)
		{
			if (search == null)
				throw new ArgumentNullException(nameof(search));

			if (search.MaxResults <= 0)
				return Enumerable.Empty<AudioLogEntry>();

			var query = Query.All(nameof(AudioLogEntry.Timestamp), Query.Descending);

			if (!string.IsNullOrEmpty(search.TitlePart))
				query = Query.And(query, Query.Where(ResourceTitleQueryColumn, val => val.AsString.ToLowerInvariant().Contains(search.TitlePart)));

			if (search.UserId.HasValue)
				query = Query.And(query, Query.EQ(nameof(AudioLogEntry.UserInvokeId), (long)search.UserId.Value));

			if (search.LastInvokedAfter.HasValue)
				query = Query.And(query, Query.GTE(nameof(AudioLogEntry.Timestamp), search.LastInvokedAfter.Value));

			return audioLogEntries.Find(query, 0, search.MaxResults);
		}

		public string SearchParsed(SeachQuery query) => Format(Search(query));

		public string Format(AudioLogEntry ale)
			=> Formatter.ProcessQuery(ale, SmartHistoryFormatter.DefaultAleFormat);
		public string Format(IEnumerable<AudioLogEntry> aleList)
			=> Formatter.ProcessQuery(aleList, SmartHistoryFormatter.DefaultAleFormat);

		public AudioLogEntry FindEntryByResource(AudioResource resource)
		{
			if (resource == null)
				throw new ArgumentNullException(nameof(resource));
			return FindByUniqueId(resource.UniqueId);
		}

		/// <summary>Gets an <see cref="AudioLogEntry"/> by its history id or null if not exising.</summary>
		/// <param name="id">The id of the AudioLogEntry</param>
		public R<AudioLogEntry> GetEntryById(uint id)
		{
			var entry = audioLogEntries.FindById((long)id);
			if (entry != null) return entry;
			else return "Could not find track with this id";
		}

		/// <summary>Removes the <see cref="AudioLogEntry"/> from the Database.</summary>
		/// <param name="ale">The <see cref="AudioLogEntry"/> to delete.</param>
		public void RemoveEntry(AudioLogEntry ale)
		{
			if (ale == null)
				throw new ArgumentNullException(nameof(ale));
			audioLogEntries.Delete(ale.Id);
		}

		/// <summary>Sets the name of a <see cref="AudioLogEntry"/>.</summary>
		/// <param name="ale">The id of the <see cref="AudioLogEntry"/> to rename.</param>
		/// <param name="newName">The new name for the <see cref="AudioLogEntry"/>.</param>
		/// <exception cref="ArgumentNullException">When ale is null or the name is null, empty or only whitspaces</exception>
		public void RenameEntry(AudioLogEntry ale, string newName)
		{
			if (ale == null)
				throw new ArgumentNullException(nameof(ale));
			if (string.IsNullOrWhiteSpace(newName))
				throw new ArgumentNullException(nameof(newName));
			// update the name
			ale.SetName(newName);
			audioLogEntries.Update(ale);
		}

		public void CleanHistoryFile()
		{
			database.Shrink();
		}

		public void RemoveBrokenLinks(CommandSystem.ExecutionInformation info)
		{
			using (var trans = database.BeginTrans())
			{
				const int iterations = 3;
				var currentIter = audioLogEntries.FindAll();

				for (int i = 0; i < iterations; i++)
				{
					info.Write("Filter iteration " + i);
					currentIter = FilterList(info, currentIter);
				}

				foreach (var entry in currentIter)
				{
					RemoveEntry(entry);
					info.Bot.PlaylistManager.AddToTrash(new PlaylistItem(entry.AudioResource));
					info.Write($"Removed: {entry.Id} - {entry.AudioResource.ResourceTitle}");
				}

				trans.Commit();
			}
		}

		/// <summary>
		/// Goes through a list of <see cref="AudioLogEntry"/> and checks if the contained <see cref="AudioResource"/>
		/// is playable/resolveable.
		/// </summary>
		/// <param name="session">Session object to inform the user about the current cleaning status.</param>
		/// <param name="list">The list to iterate.</param>
		/// <returns>A new list with all working items.</returns>
		private static List<AudioLogEntry> FilterList(CommandSystem.ExecutionInformation info, IEnumerable<AudioLogEntry> list)
		{
			int userNotityCnt = 0;
			var nextIter = new List<AudioLogEntry>();
			foreach (var entry in list)
			{
				var result = info.Bot.FactoryManager.Load(entry.AudioResource);
				if (!result)
				{
					info.Write($"//DEBUG// ({entry.AudioResource.UniqueId}) Reason: {result.Message}");
					nextIter.Add(entry);
				}

				if (++userNotityCnt % 100 == 0)
					info.Write("Working" + new string('.', userNotityCnt / 100));
			}
			return nextIter;
		}

		public void Dispose()
		{
			database.Dispose();
		}
	}

	public class HistoryManagerData : ConfigData
	{
		[Info("The Path to the history database file", "history.db")]
		public string HistoryFile { get; set; }
		[Info("Whether or not deleted history ids should be filled up with new songs", "true")]
		public bool FillDeletedIds { get; set; }
	}
}
