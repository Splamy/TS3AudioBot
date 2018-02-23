// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.History
{
	using Helper;
	using LiteDB;
	using ResourceFactories;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>Stores all played songs. Can be used to search and restore played songs.</summary>
	public sealed class HistoryManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const int CurrentHistoryVersion = 1;
		private const string AudioLogEntriesTable = "audioLogEntries";
		private const string ResourceTitleQueryColumn = "lowTitle";

		private LiteCollection<AudioLogEntry> audioLogEntries;
		private readonly HistoryManagerData historyManagerData;
		private readonly LinkedList<int> unusedIds;
		private readonly object dbLock = new object();

		public IHistoryFormatter Formatter { get; private set; }
		public uint HighestId => (uint)audioLogEntries.Max().AsInt32;

		public DbStore Database { get; set; }
		public ResourceFactoryManager FactoryManager { get; set; }
		public PlaylistManager PlaylistManager { get; set; }

		static HistoryManager()
		{
			BsonMapper.Global.Entity<AudioLogEntry>()
				.Id(x => x.Id);
		}

		public HistoryManager(HistoryManagerData hmd)
		{
			Formatter = new SmartHistoryFormatter();
			historyManagerData = hmd;

			Util.Init(out unusedIds);
		}

		public void Initialize()
		{
			audioLogEntries = Database.GetCollection<AudioLogEntry>(AudioLogEntriesTable);
			audioLogEntries.EnsureIndex(x => x.AudioResource.UniqueId, true);
			audioLogEntries.EnsureIndex(x => x.Timestamp);
			audioLogEntries.EnsureIndex(ResourceTitleQueryColumn,
				$"LOWER($.{nameof(AudioLogEntry.AudioResource)}.{nameof(AudioResource.ResourceTitle)})");

			RestoreFromFile();

			// Content upgrade

			var meta = Database.GetMetaData(AudioLogEntriesTable);
			if (meta.Version >= CurrentHistoryVersion)
				return;

			switch (meta.Version)
			{
			case 0:
				var all = audioLogEntries.FindAll().ToArray();
				foreach (var audioLogEntry in all)
				{
					switch (audioLogEntry.AudioResource.AudioType)
					{
					case "MediaLink": audioLogEntry.AudioResource.AudioType = "media"; break;
					case "Youtube": audioLogEntry.AudioResource.AudioType = "youtube"; break;
					case "Soundcloud": audioLogEntry.AudioResource.AudioType = "soundcloud"; break;
					case "Twitch": audioLogEntry.AudioResource.AudioType = "twitch"; break;
					}
				}
				audioLogEntries.Update(all);
				meta.Version = 1;
				Database.UpdateMetaData(meta);
				goto default;

			default:
				Log.Info("Database table \"{0}\" upgraded to {1}", AudioLogEntriesTable, meta.Version);
				break;
			}
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

			lock (dbLock)
			{
				var ale = FindByUniqueId(saveData.Resource.UniqueId);
				if (ale == null)
				{
					ale = CreateLogEntry(saveData);
					if (ale == null)
						Log.Error("AudioLogEntry could not be created!");
				}
				else
				{
					LogEntryPlay(ale);
				}

				return ale;
			}
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
			if (historyManagerData.FillDeletedIds && unusedIds.Count > 0)
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
				return Array.Empty<AudioLogEntry>();

			var query = Query.All(nameof(AudioLogEntry.Timestamp), Query.Descending);

			if (!string.IsNullOrEmpty(search.TitlePart))
			{
				var titleLower = search.TitlePart.ToLowerInvariant();
				query = Query.And(query,
					Query.Where(ResourceTitleQueryColumn, val => val.AsString.Contains(titleLower)));
			}

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
		public bool RemoveEntry(AudioLogEntry ale)
		{
			if (ale == null)
				throw new ArgumentNullException(nameof(ale));
			return audioLogEntries.Delete(ale.Id);
		}

		/// <summary>Sets the name of a <see cref="AudioLogEntry"/>.</summary>
		/// <param name="ale">The id of the <see cref="AudioLogEntry"/> to rename.</param>
		/// <param name="newName">The new name for the <see cref="AudioLogEntry"/>.</param>
		/// <exception cref="ArgumentNullException">When ale is null or the name is null, empty or only whitespaces</exception>
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

		public void RemoveBrokenLinks()
		{
			const int iterations = 3;
			var currentIter = audioLogEntries.FindAll().ToList();

			for (int i = 0; i < iterations; i++)
			{
				Log.Info("Filter iteration {0}", i);
				currentIter = FilterList(currentIter);
			}

			foreach (var entry in currentIter)
			{
				if (RemoveEntry(entry))
				{
					PlaylistManager.AddToTrash(new PlaylistItem(entry.AudioResource));
					Log.Info("Removed: {0} - {1}", entry.Id, entry.AudioResource.ResourceTitle);
				}
			}
		}

		/// <summary>
		/// Goes through a list of <see cref="AudioLogEntry"/> and checks if the contained <see cref="AudioResource"/>
		/// is playable/resolvable.
		/// </summary>
		/// <param name="list">The list to iterate.</param>
		/// <returns>A new list with all working items.</returns>
		private List<AudioLogEntry> FilterList(IReadOnlyCollection<AudioLogEntry> list)
		{
			int userNotityCnt = 0;
			var nextIter = new List<AudioLogEntry>(list.Count);
			foreach (var entry in list)
			{
				var result = FactoryManager.Load(entry.AudioResource);
				if (!result)
				{
					Log.Debug("Cleaning: ({0}) Reason: {1}", entry.AudioResource.UniqueId, result.Error);
					nextIter.Add(entry);
				}

				if (++userNotityCnt % 100 == 0)
					Log.Debug("Clean in progress {0}", new string('.', userNotityCnt / 100 % 10));
			}
			return nextIter;
		}
	}

	public class HistoryManagerData : ConfigData
	{
		[Info("Allows to enable or disable history features completely to save resources.", "true")]
		public bool EnableHistory { get; set; }
		[Info("The Path to the history database file", "history.db")]
		public string HistoryFile { get; set; }
		[Info("Whether or not deleted history ids should be filled up with new songs", "true")]
		public bool FillDeletedIds { get; set; }
	}
}
