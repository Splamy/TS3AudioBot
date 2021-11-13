// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TS3AudioBot.Localization;
using TS3AudioBot.ResourceFactories;
using TSLib;
using TSLib.Helper;

namespace TS3AudioBot.History;

/// <summary>Stores all played songs. Can be used to search and restore played songs.</summary>
public sealed class HistoryManager
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	private const int CurrentHistoryVersion = 2;
	private const string AudioLogEntriesTable = "audioLogEntries";

	private readonly ILiteCollection<AudioLogEntry> audioLogEntries;
	private readonly object dbLock = new();

	public IHistoryFormatter Formatter { get; private set; }
	public uint HighestId => (uint)audioLogEntries.Max().AsInt32;

	static HistoryManager()
	{
		BsonMapper.Global.Entity<AudioLogEntry>()
			.Id(x => x.Id);
	}

	public HistoryManager(DbStore database)
	{
		Formatter = new SmartHistoryFormatter();

		var meta = database.GetMetaData(AudioLogEntriesTable);

		if (meta.Version > CurrentHistoryVersion)
		{
			Log.Error("Database table \"{0}\" is higher than the current version. (table:{1}, app:{2}). " +
				"Please download the latest TS3AudioBot to read the history.", AudioLogEntriesTable, meta.Version, CurrentHistoryVersion);
			throw new NotSupportedException();
		}

		audioLogEntries = database.GetCollection<AudioLogEntry>(AudioLogEntriesTable);
		audioLogEntries.EnsureIndex(x => x.AudioResource.UniqueId, true);
		audioLogEntries.EnsureIndex(x => x.Timestamp);


		if (meta.Version == CurrentHistoryVersion)
			return;

		void SetMetaVersion(int version)
		{
			meta.Version = version;
			database.UpdateMetaData(meta);
		}


		if (audioLogEntries.Count() == 0)
		{
			SetMetaVersion(CurrentHistoryVersion);
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
			SetMetaVersion(1);
			goto case 1;

		case 1:
			audioLogEntries.DropIndex("lowTitle");
			SetMetaVersion(2);
			goto default;

		default:
			Debug.Assert(meta.Version == CurrentHistoryVersion);
			Log.Info("Database table \"{0}\" upgraded to {1}", AudioLogEntriesTable, meta.Version);
			break;
		}
	}

	public AudioLogEntry? LogAudioResource(HistorySaveData saveData)
	{
		if (saveData is null)
			throw new ArgumentNullException(nameof(saveData));

		lock (dbLock)
		{
			var ale = FindByUniqueId(saveData.Resource.UniqueId);
			if (ale is null)
			{
				if (!CreateLogEntry(saveData).Get(out ale, out var error))
				{
					Log.Warn(error, "AudioLogEntry could not be created!");
					return null;
				}
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
		ale.Timestamp = Tools.Now;
		// update the playcount
		ale.PlayCount++;

		audioLogEntries.Update(ale);
	}

	private R<AudioLogEntry, Exception> CreateLogEntry(HistorySaveData saveData)
	{
		if (string.IsNullOrWhiteSpace(saveData.Resource.ResourceTitle))
			return new Exception("Track name is empty");

		var userUid = saveData.InvokerUid?.Value ?? Uid.Anonymous.Value;
		var ale = new AudioLogEntry(0, saveData.Resource, userUid)
		{
			Timestamp = Tools.Now,
			PlayCount = 1,
		};

		try
		{
			audioLogEntries.Insert(ale);
			return ale;
		}
		catch (Exception ex) { return ex; }
	}

	private AudioLogEntry? FindByUniqueId(string uniqueId) => audioLogEntries.FindOne(x => x.AudioResource.UniqueId == uniqueId);

	/// <summary>Gets all Entries matching the search criteria.
	/// The entries are sorted by last playtime descending.</summary>
	/// <param name="search">All search criteria.</param>
	/// <returns>A list of all found entries.</returns>
	public IEnumerable<AudioLogEntry> Search(SearchQuery search)
	{
		if (search is null)
			throw new ArgumentNullException(nameof(search));

		if (search.MaxResults <= 0)
			return Array.Empty<AudioLogEntry>();

		var q2 = audioLogEntries.Query().OrderByDescending(x => x.Timestamp);

		if (!string.IsNullOrEmpty(search.TitlePart))
		{
			var titleLower = search.TitlePart.ToLowerInvariant();
			q2 = q2.Where(x => x.AudioResource.ResourceTitle!.ToLower().Contains(titleLower));
		}

		if (search.UserUid != null)
			q2 = q2.Where(x => x.UserUid == search.UserUid);

		if (search.LastInvokedAfter is { } invoked)
			q2 = q2.Where(x => x.Timestamp >= invoked);

		return q2.Limit(search.MaxResults).ToEnumerable();
	}

	public string SearchParsed(SearchQuery query) => Format(Search(query));

	public string Format(AudioLogEntry ale)
		=> Formatter.ProcessQuery(ale, SmartHistoryFormatter.DefaultAleFormat);
	public string Format(IEnumerable<AudioLogEntry> aleList)
		=> Formatter.ProcessQuery(aleList, SmartHistoryFormatter.DefaultAleFormat);

	public AudioLogEntry? FindEntryByResource(AudioResource resource)
	{
		if (resource is null)
			throw new ArgumentNullException(nameof(resource));
		return FindByUniqueId(resource.UniqueId);
	}

	/// <summary>Gets an <see cref="AudioLogEntry"/> by its history id or null if not existing.</summary>
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

	public async Task UpdadeDbIdToUid(Ts3Client ts3Client)
	{
		var upgradedEntries = new List<AudioLogEntry>();
		var dbIdCache = new Dictionary<uint, (bool valid, Uid uid)>();

		foreach (var audioLogEntry in audioLogEntries.FindAll())
		{
#pragma warning disable CS0618
			if (audioLogEntry.UserInvokeId is null)
				continue;

			if (audioLogEntry.UserUid != null || audioLogEntry.UserInvokeId.Value == 0)
			{
				audioLogEntry.UserInvokeId = null;
				upgradedEntries.Add(audioLogEntry);
				continue;
			}

			if (!dbIdCache.TryGetValue(audioLogEntry.UserInvokeId.Value, out var data))
			{
				try
				{
					var dbData = await ts3Client.GetDbClientByDbId((ClientDbId)audioLogEntry.UserInvokeId.Value);
					data = (true, dbData.Uid);
				}
				catch (AudioBotException)
				{
					Log.Warn("Client DbId {0} could not be found.", audioLogEntry.UserInvokeId.Value);
					data = (false, Uid.Null);
				}
				dbIdCache.Add(audioLogEntry.UserInvokeId.Value, data);
			}

			if (!data.valid)
				continue;

			audioLogEntry.UserInvokeId = null;
			audioLogEntry.UserUid = data.uid.Value;
			upgradedEntries.Add(audioLogEntry);
#pragma warning restore CS0618
		}

		if (upgradedEntries.Count > 0)
			audioLogEntries.Update(upgradedEntries);
		Log.Info("Upgraded {0} entries.", upgradedEntries.Count);
	}
}
