using System;
using System.IO;
using System.Linq;
using System.Text;
using TS3AudioBot.ResourceFactories;
using TS3AudioBot.Algorithm;
using System.Collections.Generic;
using System.Globalization;
using TS3AudioBot.Helper;

namespace TS3AudioBot
{
	class HistoryManager
	{
		private HistoryFile historyFile;
		private IEnumerable<AudioLogEntry> lastResult;
		public SmartHistoryFormatter Formatter { get; private set; }

		public HistoryManager(HistoryManagerData hmd)
		{
			Formatter = new SmartHistoryFormatter();
			historyFile = new HistoryFile();
			historyFile.LoadFile(hmd.historyFile);
		}

		public void LogAudioResource(PlayData playData)
		{
			historyFile.Store(playData);
		}

		public IEnumerable<AudioLogEntry> Search(SeachQuery query)
		{
			IEnumerable<AudioLogEntry> filteredHistory = null;

			if (!string.IsNullOrEmpty(query.TitlePart))
			{
				filteredHistory = historyFile.SearchTitle(query.TitlePart);
				if (query.UserId != null)
					filteredHistory = filteredHistory.Where(ald => ald.UserInvokeId == query.UserId.Value);
				if (query.LastInvokedAfter != null)
					filteredHistory = filteredHistory.Where(ald => ald.Timestamp > query.LastInvokedAfter.Value);
			}
			else if (query.UserId != null)
			{
				filteredHistory = historyFile.SeachByUser(query.UserId.Value);
				if (query.LastInvokedAfter != null)
					filteredHistory = filteredHistory.Where(ald => ald.Timestamp > query.LastInvokedAfter.Value);
			}
			else if (query.LastInvokedAfter != null)
			{
				filteredHistory = historyFile.SeachTillTime(query.LastInvokedAfter.Value);
			}
			else if (query.MaxResults >= 0)
			{
				lastResult = historyFile.GetLastXEntrys(query.MaxResults);
				return lastResult;
			}

			lastResult = filteredHistory;
			return filteredHistory.TakeLast(query.MaxResults);
		}

		public string SearchParsed(SeachQuery query)
		{
			var aleList = Search(query);
			return Formatter.ProcessQuery(aleList);
		}

		public AudioLogEntry GetEntryById(uint id)
		{
			return historyFile.GetEntryById(id);
		}
	}

	class SeachQuery
	{
		public string TitlePart;
		public uint? UserId;
		public DateTime? LastInvokedAfter;
		public int MaxResults;

		public SeachQuery()
		{
			TitlePart = null;
			UserId = null;
			LastInvokedAfter = null;
			MaxResults = 10;
		}
	}

	class HistoryFile
	{
		private IDictionary<string, uint> resIdToId;
		private IDictionary<uint, AudioLogEntry> idFilter;
		private ISubstringSearch<AudioLogEntry> titleFilter;
		private IDictionary<uint, IList<AudioLogEntry>> userIdFilter;
		private SortedList<DateTime, AudioLogEntry> timeFilter;

		private uint currentID = 0;

		private readonly IList<AudioLogEntry> noResult = new List<AudioLogEntry>().AsReadOnly();

		private static readonly Encoding FileEncoding = Encoding.ASCII;
		private static readonly byte[] NewLineArray = FileEncoding.GetBytes(Environment.NewLine);
		private FileStream fileStream;
		//private StreamWriter fileWriter;
		private PositionedStreamReader fileReader;
		public string Path { get; private set; }

		public HistoryFile()
		{
			resIdToId = new Dictionary<string, uint>();
			idFilter = new SortedList<uint, AudioLogEntry>();
			titleFilter = new SimpleSubstringFinder<AudioLogEntry>();
			userIdFilter = new SortedList<uint, IList<AudioLogEntry>>();
			timeFilter = new SortedList<DateTime, AudioLogEntry>();
		}


		public void LoadFile(string path)
		{
			Path = path;

			if (fileStream != null)
			{
				fileStream.Dispose();
				fileStream = null;
			}

			Clear();

			fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
			fileReader = new PositionedStreamReader(fileStream, FileEncoding);
			RestoreFromFile();
		}

		private void RestoreFromFile()
		{
			string line;
			long readIndex = 0;
			while ((line = fileReader.ReadLine()) != null)
			{
				if (string.IsNullOrEmpty(line))
					continue;
				var ale = AudioLogEntry.Parse(line, readIndex);
				if (ale != null)
				{
					AddToMemoryIndex(ale);
					if (ale.Id >= currentID)
						currentID = ale.Id + 1;
				}
				readIndex = fileReader.ReadPosition;
			}
		}


		public void Store(PlayData playData)
		{
			uint? index = Contains(playData.Resource);
			if (index == null)
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
				UpdateLogEntry(index.Value, playData);
			}
		}

		private uint? Contains(AudioResource resource)
		{
			uint rId;
			if (resIdToId.TryGetValue(resource.ResourceId, out rId))
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
			return titleFilter.GetValues(titlePart.ToLower());
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


		private AudioLogEntry CreateLogEntry(PlayData playData)
		{
			var resource = playData.Resource;
			if (string.IsNullOrWhiteSpace(resource.ResourceTitle))
				return null;
			var ale = new AudioLogEntry(currentID, resource.AudioType, resource.ResourceId, fileStream.Position)
			{
				UserInvokeId = (uint)playData.Invoker.DatabaseId,
				Timestamp = GetNow(),
				Title = resource.ResourceTitle,
				PlayCount = 1,
			};
			currentID++;

			return ale;
		}

		private void UpdateLogEntry(uint index, PlayData playData)
		{
			AudioLogEntry ale = idFilter[index];

			if (playData.Invoker.DatabaseId != ale.UserInvokeId) // TODO: test
			{
				userIdFilter[ale.UserInvokeId].Remove(ale);
				ale.UserInvokeId = (uint)playData.Invoker.DatabaseId;
				AutoAdd(userIdFilter, ale);
			}

			timeFilter.Remove(ale.Timestamp);
			ale.Timestamp = GetNow();
			timeFilter.Add(ale.Timestamp, ale);

			ale.PlayCount++;

			ReWriteToFile(ale);
		}

		private void AppendToFile(AudioLogEntry logEntry)
		{
			var strBytes = FileEncoding.GetBytes(logEntry.ToFileString());
			fileStream.Write(strBytes, 0, strBytes.Length);
			fileStream.Write(NewLineArray, 0, NewLineArray.Length);
			fileStream.Flush();
		}

		private void ReWriteToFile(AudioLogEntry logEntry)
		{
			fileStream.Seek(logEntry.FilePosIndex, SeekOrigin.Begin);
			byte[] curLine = FileEncoding.GetBytes(fileReader.ReadLine());
			byte[] newLine = FileEncoding.GetBytes(logEntry.ToFileString());

			if (Enumerable.SequenceEqual(curLine, newLine))
				return;

			fileStream.Seek(logEntry.FilePosIndex, SeekOrigin.Begin);
			if (newLine.Length <= curLine.Length)
			{
				fileStream.Write(newLine, 0, newLine.Length);
				if (newLine.Length < curLine.Length)
				{
					byte[] filler = Enumerable.Repeat((byte)' ', curLine.Length - newLine.Length).ToArray();
					fileStream.Write(filler, 0, filler.Length);
				}
				fileStream.Seek(0, SeekOrigin.End);
			}
			else
			{
				byte[] filler = Enumerable.Repeat((byte)' ', curLine.Length).ToArray();
				fileStream.Write(filler, 0, filler.Length);
				fileStream.Seek(0, SeekOrigin.End);
				AppendToFile(logEntry);
			}
			fileStream.Flush(true);
		}

		private void AddToMemoryIndex(AudioLogEntry logEntry)
		{
			resIdToId.Add(logEntry.ResourceId, logEntry.Id);
			idFilter.Add(logEntry.Id, logEntry);
			titleFilter.Add(logEntry.Title.ToLower(), logEntry);
			AutoAdd(userIdFilter, logEntry);
			timeFilter.Add(logEntry.Timestamp, logEntry);
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

		private DateTime GetNow()
		{
			return DateTime.Now;
		}

		private void Clear()
		{
			resIdToId.Clear();
			idFilter.Clear();
			titleFilter.Clear();
			userIdFilter.Clear();
			timeFilter.Clear();
		}
	}

	class AudioLogEntry
	{
		public uint Id { get; private set; }
		public uint UserInvokeId { get; set; }
		public uint PlayCount { get; set; }
		public DateTime Timestamp { get; set; }
		public AudioType AudioType { get; private set; }
		public string ResourceId { get; private set; }
		public string Title { get; set; }

		public long FilePosIndex { get; private set; }

		public AudioLogEntry(uint id, AudioType audioType, string resId, long fileIndex)
		{
			Id = id;
			PlayCount = 0;
			AudioType = audioType;
			ResourceId = resId;
			FilePosIndex = fileIndex;
		}

		public string ToFileString()
		{
			StringBuilder strb = new StringBuilder();
			// HEX STRINGS
			strb.Append(AsHex(Id));
			strb.Append(",");
			strb.Append(AsHex(UserInvokeId));
			strb.Append(",");
			strb.Append(AsHex(PlayCount));
			strb.Append(",");
			strb.Append(AsHex(Timestamp.ToFileTime()));
			strb.Append(",");

			// OTHER STRINGS
			strb.Append(AudioType.ToString());
			strb.Append(",");
			strb.Append(Uri.EscapeDataString(ResourceId));
			strb.Append(",");
			strb.Append(Uri.EscapeDataString(Title));

			return strb.ToString();
		}

		public static AudioLogEntry Parse(string line, long readIndex)
		{
			string[] strParts = line.Split(',');
			if (strParts.Length != 7)
				return null;
			// Array.ForEach(strParts) // check if spacetrims are needed
			int index = 0;
			uint id = uint.Parse(strParts[index++], NumberStyles.HexNumber);
			uint userInvId = uint.Parse(strParts[index++], NumberStyles.HexNumber);
			uint playCount = uint.Parse(strParts[index++], NumberStyles.HexNumber);
			long dtStamp = long.Parse(strParts[index++], NumberStyles.HexNumber);
			DateTime dateTime = DateTime.FromFileTime(dtStamp);
			AudioType audioType;
			if (!Enum.TryParse(strParts[index++], out audioType))
				return null;
			string resId = Uri.UnescapeDataString(strParts[index++]);
			string title = Uri.UnescapeDataString(strParts[index++]);
			return new AudioLogEntry(id, audioType, resId, readIndex)
			{
				PlayCount = playCount,
				Timestamp = dateTime,
				Title = title,
				UserInvokeId = userInvId,
			};
		}

		private string AsHex(uint num) { return num.ToString("X8"); }
		private string AsHex(long num) { return num.ToString("X16"); }

		public override string ToString()
		{
			return string.Format("[{0}] @ {1} by {2}: {3}, ({4})", Id, Timestamp, UserInvokeId, Title, ResourceId);
		}
	}

	public struct HistoryManagerData
	{
		[Info("the absolute or relative path to the history database file")]
		public string historyFile;
	}
}