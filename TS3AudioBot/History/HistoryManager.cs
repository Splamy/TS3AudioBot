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
	using Config;
	using Helper;
	using LiteDB;
	using Localization;
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
		private readonly LinkedList<int> unusedIds;
		private readonly object dbLock = new object();
		private readonly ConfHistory config;

		public IHistoryFormatter Formatter { get; private set; }
		public uint HighestId => (uint)audioLogEntries.Max().AsInt32;

		public DbStore Database { get; set; }

		static HistoryManager()
		{
			BsonMapper.Global.Entity<AudioLogEntry>()
				.Id(x => x.Id);
		}

		public HistoryManager(ConfHistory config)
		{
			Formatter = new SmartHistoryFormatter();

			Util.Init(out unusedIds);
			this.config = config;
		}

		public void Initialize()
		{
			var meta = Database.GetMetaData(AudioLogEntriesTable);

			if (meta.Version > CurrentHistoryVersion)
			{
				Log.Error("Database table \"{0}\" is higher than the current version. (table:{1}, app:{2}). " +
					"Please download the latest TS3AudioBot to read the history.", AudioLogEntriesTable, meta.Version, CurrentHistoryVersion);
				return;
			}

			audioLogEntries = Database.GetCollection<AudioLogEntry>(AudioLogEntriesTable);
			audioLogEntries.EnsureIndex(x => x.AudioResource.UniqueId, true);
			audioLogEntries.EnsureIndex(x => x.Timestamp);
			audioLogEntries.EnsureIndex(ResourceTitleQueryColumn,
				$"LOWER($.{nameof(AudioLogEntry.AudioResource)}.{nameof(AudioResource.ResourceTitle)})");
			RestoreFromFile();

			if (meta.Version == CurrentHistoryVersion)
				return;

			if (audioLogEntries.Count() == 0)
			{
				meta.Version = CurrentHistoryVersion;
				Database.UpdateMetaData(meta);
				return;
			}

			// Content upgrade
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
			if (saveData is null)
				throw new ArgumentNullException(nameof(saveData));

			lock (dbLock)
			{
				var ale = FindByUniqueId(saveData.Resource.UniqueId);
				if (ale is null)
				{
					var createResult = CreateLogEntry(saveData);
					if (!createResult.Ok)
					{
						Log.Warn("AudioLogEntry could not be created! ({0})", createResult.Error);
						return R.Err;
					}
					ale = createResult.Value;
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
			if (ale is null)
				throw new ArgumentNullException(nameof(ale));

			// update the playtime
			ale.Timestamp = Util.GetNow();
			// update the playcount
			ale.PlayCount++;

			audioLogEntries.Update(ale);
		}

		private R<AudioLogEntry, string> CreateLogEntry(HistorySaveData saveData)
		{
			if (string.IsNullOrWhiteSpace(saveData.Resource.ResourceTitle))
				return "Track name is empty";

			int nextHid;
			if (config.FillDeletedIds && unusedIds.Count > 0)
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
				UserUid = saveData.InvokerUid,
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
			if (search is null)
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

			if (search.UserUid != null)
				query = Query.And(query, Query.EQ(nameof(AudioLogEntry.UserUid), search.UserUid));

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
			if (resource is null)
				throw new ArgumentNullException(nameof(resource));
			return FindByUniqueId(resource.UniqueId);
		}

		/// <summary>Gets an <see cref="AudioLogEntry"/> by its history id or null if not exising.</summary>
		/// <param name="id">The id of the AudioLogEntry</param>
		public R<AudioLogEntry, LocalStr> GetEntryById(uint id)
		{
			var entry = audioLogEntries.FindById((long)id);
			if (entry != null) return entry;
			else return new LocalStr(strings.error_history_could_not_find_entry);
		}

		/// <summary>Removes the <see cref="AudioLogEntry"/> from the Database.</summary>
		/// <param name="ale">The <see cref="AudioLogEntry"/> to delete.</param>
		public bool RemoveEntry(AudioLogEntry ale)
		{
			if (ale is null)
				throw new ArgumentNullException(nameof(ale));
			return audioLogEntries.Delete(ale.Id);
		}

		/// <summary>Sets the name of a <see cref="AudioLogEntry"/>.</summary>
		/// <param name="ale">The id of the <see cref="AudioLogEntry"/> to rename.</param>
		/// <param name="newName">The new name for the <see cref="AudioLogEntry"/>.</param>
		/// <exception cref="ArgumentNullException">When ale is null or the name is null, empty or only whitespaces</exception>
		public void RenameEntry(AudioLogEntry ale, string newName)
		{
			if (ale is null)
				throw new ArgumentNullException(nameof(ale));
			if (string.IsNullOrWhiteSpace(newName))
				throw new ArgumentNullException(nameof(newName));
			// update the name
			ale.SetName(newName);
			audioLogEntries.Update(ale);
		}

		public void RemoveBrokenLinks(ResourceFactoryManager resourceFactory)
		{
			const int iterations = 3;
			var currentIter = audioLogEntries.FindAll().ToList();

			for (int i = 0; i < iterations; i++)
			{
				Log.Info("Filter iteration {0}", i);
				currentIter = FilterList(resourceFactory, currentIter);
			}

			foreach (var entry in currentIter)
			{
				if (RemoveEntry(entry))
				{
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
		private List<AudioLogEntry> FilterList(ResourceFactoryManager resourceFactory, IReadOnlyCollection<AudioLogEntry> list)
		{
			int userNotifyCnt = 0;
			var nextIter = new List<AudioLogEntry>(list.Count);
			foreach (var entry in list)
			{
				var result = resourceFactory.Load(entry.AudioResource);
				if (!result)
				{
					Log.Debug("Cleaning: ({0}) Reason: {1}", entry.AudioResource.UniqueId, result.Error);
					nextIter.Add(entry);
				}

				if (++userNotifyCnt % 100 == 0)
					Log.Debug("Clean in progress {0}", new string('.', userNotifyCnt / 100 % 10));
			}
			return nextIter;
		}

		public void UpdadeDbIdToUid(Ts3Client ts3Client)
		{
			var upgradedEntries = new List<AudioLogEntry>();
			var dbIdCache = new Dictionary<uint, (bool valid, string uid)>();

			foreach (var audioLogEntry in audioLogEntries.FindAll())
			{
#pragma warning disable CS0612
				if (!audioLogEntry.UserInvokeId.HasValue)
					continue;

				if (audioLogEntry.UserUid != null || audioLogEntry.UserInvokeId.Value == 0)
				{
					audioLogEntry.UserInvokeId = null;
					upgradedEntries.Add(audioLogEntry);
					continue;
				}

				if (!dbIdCache.TryGetValue(audioLogEntry.UserInvokeId.Value, out var data))
				{
					var result = ts3Client.GetDbClientByDbId(audioLogEntry.UserInvokeId.Value);
					data.uid = (data.valid = result.Ok) ? result.Value.Uid : null;
					if (!data.valid)
					{
						Log.Warn("Client DbId {0} could not be found.", audioLogEntry.UserInvokeId.Value);
					}
					dbIdCache.Add(audioLogEntry.UserInvokeId.Value, data);
				}

				if (!data.valid)
					continue;

				audioLogEntry.UserInvokeId = null;
				audioLogEntry.UserUid = data.uid;
				upgradedEntries.Add(audioLogEntry);
#pragma warning restore CS0612
			}

			if (upgradedEntries.Count > 0)
				audioLogEntries.Update(upgradedEntries);
			Log.Info("Upgraded {0} entries.", upgradedEntries.Count);
		}
	}
}
