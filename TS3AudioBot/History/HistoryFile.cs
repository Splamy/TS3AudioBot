namespace TS3AudioBot.History
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Algorithm;
	using Helper;
	using ResourceFactories;

	public class HistoryFile : IDisposable
	{
		private IDictionary<string, uint> resIdToId;
		private IDictionary<uint, AudioLogEntry> idFilter;
		private ISubstringSearch<AudioLogEntry> titleFilter;
		private IDictionary<uint, IList<AudioLogEntry>> userIdFilter;
		private SortedList<DateTime, AudioLogEntry> timeFilter;

		public uint CurrentID { get; private set; } = 0;

		private readonly IList<AudioLogEntry> noResult = new List<AudioLogEntry>().AsReadOnly();

		private static readonly Encoding FileEncoding = Encoding.ASCII;
		private static readonly byte[] NewLineArray = new byte[] { (byte)'\n' };
		private FileInfo historyFile;
		private FileStream fileStream;
		private PositionedStreamReader fileReader;

		const string VersionHeader = "VERSION-";
		private const int HistoryManagerVersion = 1;


		public HistoryFile()
		{
			resIdToId = new Dictionary<string, uint>();
			idFilter = new SortedList<uint, AudioLogEntry>();
			titleFilter = new SimpleSubstringFinder<AudioLogEntry>();
			userIdFilter = new SortedList<uint, IList<AudioLogEntry>>();
			timeFilter = new SortedList<DateTime, AudioLogEntry>();
		}

		public void OpenFile(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			historyFile = new FileInfo(path);

			CloseFile();
			Clear();

			fileStream = historyFile.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
			fileReader = new PositionedStreamReader(fileStream, FileEncoding);
			VersionCheckAndUpgrade();
			RestoreFromFile();
		}

		public void CloseFile()
		{
			if (fileReader != null)
			{
				fileReader.Dispose();
				fileReader = null;
			}
			if (fileStream != null)
			{
				fileStream.Dispose();
				fileStream = null;
			}
		}

		private void VersionCheckAndUpgrade()
		{
			string line = fileReader.ReadLine();
			if (line == null)
			{
				// fresh file
				WriteHeader();
				return;
			}

			int fileVersion = -1;
			if (!line.StartsWith(VersionHeader)
			|| !int.TryParse(line.Substring(VersionHeader.Length), out fileVersion))
				throw new FormatException("The history file has an invalid header.");

			if (fileVersion < HistoryManagerVersion)
				BackupFile();

			switch (fileVersion)
			{
			case 0: /*do upgrade stuff*/ goto case 1;
			case 1: break; // lastest version

			default:
				throw new FormatException("Not recognized header version");
			}
		}

		public void CleanFile()
		{
			BackupFile();

			fileStream.SetLength(0);
			WriteHeader();

			for (uint i = 0; i < CurrentID; i++)
			{
				var ale = GetEntryById(i);
				if (ale != null)
					AppendToFile(ale, false);
			}
			fileStream.Flush(true);
			fileReader.InvalidateBuffer();
		}

		private void RestoreFromFile()
		{
			string line;
			long readIndex = fileReader.ReadPosition;
			while ((line = fileReader.ReadLine()) != null)
			{
				if (!string.IsNullOrEmpty(line) && line[0] != ' ')
				{
					var ale = AudioLogEntry.Parse(line, readIndex);
					if (ale != null)
					{
						AddToMemoryIndex(ale);
						if (ale.Id >= CurrentID)
							CurrentID = ale.Id + 1;
					}
				}
				readIndex = fileReader.ReadPosition;
			}
		}

		private void WriteHeader()
		{
			byte[] versionHeader = FileEncoding.GetBytes(VersionHeader + HistoryManagerVersion);
			fileStream.Write(versionHeader, 0, versionHeader.Length);
			fileStream.Write(NewLineArray, 0, NewLineArray.Length);
		}

		private void BackupFile()
		{
			int backUpNum = 0;
			string fileName;
			do
			{
				fileName = Path.Combine(historyFile.DirectoryName,
							historyFile.Name.Substring(0, historyFile.Name.Length - historyFile.Extension.Length)
							+ "_old_" + backUpNum + historyFile.Extension);
				backUpNum++;
			} while (File.Exists(fileName));
			historyFile.CopyTo(fileName);
		}


		public void Store(PlayInfoEventArgs playData)
		{
			if (playData == null)
				throw new ArgumentNullException(nameof(playData));

			uint? index = Contains(playData.ResourceData);
			if (!index.HasValue)
			{
				var ale = CreateLogEntry(playData);
				if (ale != null)
				{
					AddToMemoryIndex(ale);
					AppendToFile(ale);
				}
				else
					Log.Write(Log.Level.Error, "AudioLogEntry could not be created!");
			}
			else
			{
				var ale = GetEntryById(index.Value);
				LogEntryPlay(ale);
			}
		}

		public uint? Contains(AudioResource resource)
		{
			if (resource == null)
				throw new ArgumentNullException(nameof(resource));

			uint rId;
			if (resIdToId.TryGetValue(resource.UniqueId, out rId))
				return rId;
			return null;
		}

		/// <summary>Gets an AudioLogEntry by its unique id or null if not exising.</summary>
		/// <param name="id">The id of the AudioLogEntry</param>
		public AudioLogEntry GetEntryById(uint id)
		{
			AudioLogEntry ale;
			if (idFilter.TryGetValue(id, out ale))
				return ale;
			return null;
		}

		/// <summary>Gets all Entrys containing the requested string.<\br>
		/// Sort: Random</summary>
		/// <param name="titlePart">Any part of the title</param>
		/// <returns>A list of all found entries.</returns>
		public IList<AudioLogEntry> SearchTitle(string titlePart)
		{
			return titleFilter.GetValues(titlePart.ToLower(CultureInfo.InvariantCulture));
		}

		/// <summary>Gets all Entrys last called from a user.<\br>
		/// Sort: By id ascending.</summary>
		/// <param name="userId">TeamSpeak 3 Database UID of the user.</param>
		/// <returns>A list of all found entries.</returns>
		public IList<AudioLogEntry> SeachByUser(uint userId)
		{
			IList<AudioLogEntry> result;
			if (userIdFilter.TryGetValue(userId, out result))
				return result;
			else
				return noResult;
		}

		/// <summary>Gets all Entries until a certain datetime.<\br>
		/// Sort: By call time ascending.</summary>
		/// <param name="time">Included last time of an entry called.</param>
		/// <returns>A list of all found entries.</returns>
		public IList<AudioLogEntry> SeachTillTime(DateTime time)
		{
			int index = timeFilter.Keys.ToList().BinarySearch(time);

			if (index > 0)
			{
				return timeFilter.Values.Skip(index - 1).ToList();
			}
			else
			{
				index = ~index;
				if (index == 0)
					return timeFilter.Values;
				else if (index == timeFilter.Values.Count)
					return noResult;
				else
					return timeFilter.Values.Skip(index).ToList();
			}
		}

		/// <summary>Gets the last played entries.<\br>
		/// Sort: By call time ascending.</summary>
		/// <param name="idAmount">The maximal amount of entries.</param>
		/// <returns>A list of all found entries.</returns>
		public IList<AudioLogEntry> GetLastXEntrys(int idAmount)
		{
			if (idAmount <= 0)
				return noResult;

			var aleArray = timeFilter.Values.ToArray();
			var result = new AudioLogEntry[Math.Min(aleArray.Length, idAmount)];
			Array.Copy(aleArray, Math.Max(0, aleArray.Length - idAmount), result, 0, Math.Min(aleArray.Length, result.Length));
			return result;
		}

		// User features

		/// <summary>Increases the playcount and updates the last playtime.</summary>
		/// <param name="id">The id of the AudioLogEntry to update.</param>
		/// <param name="flush">True when the changes should be applied directly to the file.
		/// False to write it manually later with <see cref="ReWriteToFile(AudioLogEntry)"/></param>
		public void LogEntryPlay(AudioLogEntry ale, bool flush = true)
		{
			if (ale == null)
				throw new ArgumentNullException(nameof(ale));

			// update the playtime
			timeFilter.Remove(ale.Timestamp);
			ale.Timestamp = Util.GetNow();
			timeFilter.Add(ale.Timestamp, ale);

			// update the playcount
			ale.PlayCount++;

			if (flush) ReWriteToFile(ale);
		}

		/// <summary>Sets the name of a AudioLogEntry.</summary>
		/// <param name="id">The id of the AudioLogEntry to rename.</param>
		/// <param name="name">The new name for the AudioLogEntry.</param>
		/// <param name="flush">True when the changes should be applied directly to the file.
		/// False to write it manually later with <see cref="ReWriteToFile(AudioLogEntry)"/></param>
		/// <exception cref="ArgumentNullException">When the name is null, empty or only whitspaces</exception>
		public void LogEntryRename(AudioLogEntry ale, string newName, bool flush = true)
		{
			if (string.IsNullOrWhiteSpace(newName))
				throw new ArgumentNullException(nameof(newName));

			// update the name
			ale.ResourceTitle = newName;

			if (flush) ReWriteToFile(ale);
		}

		/// <summary>Removes the AudioLogEntry from the memory index list and file.</summary>
		/// <param name="id">The id of the AudioLogEntry to delete.</param>
		public void LogEntryRemove(AudioLogEntry ale)
		{
			if (ale == null)
				throw new ArgumentNullException(nameof(ale));
			if (!Contains(ale).HasValue)
				throw new ArgumentException("The requested entry was not found.");

			RemoveFromMemoryIndex(ale);
			RemoveFromFile(ale);
		}

		// Internal features

		private AudioLogEntry CreateLogEntry(PlayInfoEventArgs playData)
		{
			var resource = playData.ResourceData;
			if (string.IsNullOrWhiteSpace(resource.ResourceTitle))
				return null;
			var ale = new AudioLogEntry(CurrentID, resource.AudioType, resource.ResourceId)
			{
				UserInvokeId = (uint)playData.Invoker.DatabaseId,
				Timestamp = Util.GetNow(),
				ResourceTitle = resource.ResourceTitle,
				PlayCount = 1,
			};
			CurrentID++;

			return ale;
		}

		private void AppendToFile(AudioLogEntry logEntry, bool flush = true)
		{
			logEntry.FilePosIndex = fileStream.Position;

			var fileString = logEntry.ToFileString();
			var strBytes = FileEncoding.GetBytes(fileString);
			fileStream.Write(strBytes, 0, strBytes.Length);
			fileStream.Write(NewLineArray, 0, NewLineArray.Length);
			if (flush) fileStream.Flush(true);
		}

		private void ReWriteToFile(AudioLogEntry logEntry)
		{
			fileStream.Seek(logEntry.FilePosIndex, SeekOrigin.Begin);
			fileReader.InvalidateBuffer();
			byte[] curLine = FileEncoding.GetBytes(fileReader.ReadLine());
			byte[] newLine = FileEncoding.GetBytes(logEntry.ToFileString());

			if (Enumerable.SequenceEqual(curLine, newLine))
				return;

			fileStream.Seek(logEntry.FilePosIndex, SeekOrigin.Begin);
			if (newLine.Length <= curLine.Length)
			{
				fileStream.Write(newLine, 0, newLine.Length);
				if (newLine.Length < curLine.Length)
					CleanLine(curLine.Length - newLine.Length);
				fileStream.Seek(0, SeekOrigin.End);
			}
			else
			{
				byte[] filler = Enumerable.Repeat((byte)' ', curLine.Length).ToArray();
				fileStream.Write(filler, 0, filler.Length);
				fileStream.Seek(0, SeekOrigin.End);
				AppendToFile(logEntry, false);
			}
			fileStream.Flush(true);
		}

		private void AddToMemoryIndex(AudioLogEntry logEntry)
		{
			resIdToId.Add(logEntry.UniqueId, logEntry.Id);
			idFilter.Add(logEntry.Id, logEntry);
			titleFilter.Add(logEntry.ResourceTitle.ToLower(CultureInfo.InvariantCulture), logEntry);
			AutoAdd(userIdFilter, logEntry);
			timeFilter.Add(logEntry.Timestamp, logEntry);
		}

		private void RemoveFromFile(AudioLogEntry logEntry)
		{
			fileStream.Seek(logEntry.FilePosIndex, SeekOrigin.Begin);
			byte[] curLine = FileEncoding.GetBytes(fileReader.ReadLine());
			fileStream.Seek(logEntry.FilePosIndex, SeekOrigin.Begin);
			CleanLine(curLine.Length);
		}

		private void CleanLine(int length)
		{
			byte[] filler = Enumerable.Repeat((byte)' ', length).ToArray();
			fileStream.Write(filler, 0, length);
		}

		private void RemoveFromMemoryIndex(AudioLogEntry logEntry)
		{
			resIdToId.Remove(logEntry.UniqueId);
			idFilter.Remove(logEntry.Id);
			titleFilter.RemoveValue(logEntry);
			userIdFilter[logEntry.UserInvokeId].Remove(logEntry);
			timeFilter.Remove(logEntry.Timestamp);
		}

		private static void AutoAdd(IDictionary<uint, IList<AudioLogEntry>> dict, AudioLogEntry value)
		{
			IList<AudioLogEntry> uidList;
			if (!dict.TryGetValue(value.UserInvokeId, out uidList))
			{
				uidList = new List<AudioLogEntry>();
				dict.Add(value.UserInvokeId, uidList);
			}
			uidList.Add(value);
		}

		private void Clear()
		{
			resIdToId.Clear();
			idFilter.Clear();
			titleFilter.Clear();
			userIdFilter.Clear();
			timeFilter.Clear();

			CurrentID = 0;
		}

		public void Dispose()
		{
			CloseFile();
			Clear();
		}
	}
}
