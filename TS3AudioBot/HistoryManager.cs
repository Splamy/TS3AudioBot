using System;
using System.IO;
using System.Linq;
using System.Text;
using TS3AudioBot.RessourceFactories;
using TS3AudioBot.Algorithm;
using System.Collections.Generic;
using System.Globalization;
using TS3AudioBot.Helper;

namespace TS3AudioBot
{
	class HistoryManager
	{
		HistoryFile historyFile;
		IEnumerable<AudioLogEntry> lastResult;

		public HistoryManager()
		{
			historyFile = new HistoryFile();
			historyFile.LoadFile("history.axe");
		}

		public void LogAudioRessource(AudioRessource ar)
		{
			historyFile.Store(ar);
		}

		public IEnumerable<AudioLogEntry> Search(SeachQuery query)
		{
			IEnumerable<AudioLogEntry> filteredHistory = null;

			if (!string.IsNullOrEmpty(query.TitlePart))
			{
				filteredHistory = historyFile.SearchTitle(query.TitlePart);
				if (query.UserId != -1)
					filteredHistory = filteredHistory.Where(ald => ald.UserInvokeId == query.UserId);
				if (query.LastInvokedAfter != DateTime.MinValue)
					filteredHistory = filteredHistory.Where(ald => ald.Timestamp > query.LastInvokedAfter);
			}
			else if (query.UserId != -1)
			{
				filteredHistory = historyFile.SeachByUser(query.UserId);
				if (query.LastInvokedAfter != DateTime.MinValue)
					filteredHistory = filteredHistory.Where(ald => ald.Timestamp > query.LastInvokedAfter);
			}
			if (query.LastInvokedAfter != DateTime.MinValue)
			{
				filteredHistory = historyFile.SeachTillTime(query.LastInvokedAfter);
			}

			lastResult = filteredHistory;
			return filteredHistory.Take(query.MaxResults);
		}
	}

	class SeachQuery
	{
		public string TitlePart;
		public int UserId;
		public DateTime LastInvokedAfter;
		public int MaxResults;

		public SeachQuery()
		{
			TitlePart = null;
			UserId = -1;
			LastInvokedAfter = DateTime.MinValue;
			MaxResults = 10;
		}
	}

	class HistoryFile
	{
		private IDictionary<string, int> resIdToId;
		private IDictionary<int, AudioLogEntry> idFilter;
		private ISubstringSearch<AudioLogEntry> titleFilter;
		private IDictionary<int, IList<AudioLogEntry>> userIdFilter;
		private SortedList<DateTime, AudioLogEntry> timeFilter;

		private int currentID = 0;

		private readonly IList<AudioLogEntry> noResult = new List<AudioLogEntry>().AsReadOnly();

		private static readonly Encoding FileEncoding = Encoding.ASCII;
		private static readonly byte[] NewLineArray = FileEncoding.GetBytes(Environment.NewLine);
		private FileStream fileStream;
		//private StreamWriter fileWriter;
		private PositionedStreamReader fileReader;
		public string Path { get; private set; }

		public HistoryFile()
		{
			resIdToId = new Dictionary<string, int>();
			idFilter = new SortedList<int, AudioLogEntry>();
			titleFilter = new SimpleSubstringFinder<AudioLogEntry>();
			userIdFilter = new SortedList<int, IList<AudioLogEntry>>();
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

		public int Contains(AudioRessource resource)
		{
			int rId;
			if (resIdToId.TryGetValue(resource.RessourceId, out rId))
				return rId;
			return -1;
		}

		public IList<AudioLogEntry> SearchTitle(string titlePart)
		{
			return titleFilter.GetValues(titlePart.ToLower());
		}

		public IList<AudioLogEntry> SeachByUser(int userId)
		{
			IList<AudioLogEntry> result;
			if (userIdFilter.TryGetValue(userId, out result))
				return result;
			else
				return noResult;
		}

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

		public IList<AudioLogEntry> GetLastXEntrys(int idAmount)
		{
			if (idAmount <= 0)
				return noResult;
			var aleArray = (AudioLogEntry[])timeFilter.Values; // TODO fix ? probably not an array
			var result = new AudioLogEntry[idAmount];
			aleArray.CopyTo(result, aleArray.Length - idAmount);
			return result;
		}


		public void Store(AudioRessource resource)
		{
			int index = Contains(resource);
			if (index == -1)
			{
				var ale = CreateLogEntry(resource);
				if (ale != null)
				{
					AddToMemoryIndex(ale);
					AppendToFile(ale);
				}
				else
					Log.Write(Log.Level.Error, "AudioRessource could not be created!");
			}
			else
			{
				UpdateLogEntry(index, resource);
			}
		}

		private AudioLogEntry CreateLogEntry(AudioRessource resource)
		{
			if (string.IsNullOrWhiteSpace(resource.RessourceTitle))
				return null;
			var ale = new AudioLogEntry(currentID, resource.AudioType, resource.RessourceId, fileStream.Position)
			{
				UserInvokeId = resource.InvokingUser.DatabaseId,
				Timestamp = GetNow(),
				Title = resource.RessourceTitle,
			};
			currentID++;

			return ale;
		}

		private void UpdateLogEntry(int index, AudioRessource resource)
		{
			AudioLogEntry ale = idFilter[index];

			if (resource.InvokingUser.DatabaseId == ale.UserInvokeId)
			{
				userIdFilter[ale.UserInvokeId].Remove(ale);
				ale.UserInvokeId = resource.InvokingUser.DatabaseId;
				AutoAdd(userIdFilter, ale);
			}

			timeFilter.Remove(ale.Timestamp);
			ale.Timestamp = GetNow();
			timeFilter.Add(ale.Timestamp, ale);

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
			resIdToId.Add(logEntry.RessourceId, logEntry.Id);
			idFilter.Add(logEntry.Id, logEntry);
			titleFilter.Add(logEntry.Title.ToLower(), logEntry);
			AutoAdd(userIdFilter, logEntry);
			timeFilter.Add(logEntry.Timestamp, logEntry);
		}

		private static void AutoAdd(IDictionary<int, IList<AudioLogEntry>> dict, AudioLogEntry value)
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
		public int Id { get; private set; }
		public int UserInvokeId { get; set; }
		public AudioType AudioType { get; private set; }
		public string RessourceId { get; private set; }
		public DateTime Timestamp { get; set; }
		public string Title { get; set; }
		public long FilePosIndex { get; private set; }

		public AudioLogEntry(int id, AudioType audioType, string resId, long fileIndex)
		{
			Id = id;
			AudioType = audioType;
			RessourceId = resId;
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
			strb.Append(AsHex(Timestamp.ToFileTime()));
			strb.Append(",");

			// OTHER STRINGS
			strb.Append(AudioType.ToString());
			strb.Append(",");
			strb.Append(Uri.EscapeDataString(RessourceId));
			strb.Append(",");
			strb.Append(Uri.EscapeDataString(Title));

			return strb.ToString();
		}

		public static AudioLogEntry Parse(string line, long readIndex)
		{
			string[] strParts = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (strParts.Length != 6)
				return null;
			int id = int.Parse(strParts[0], NumberStyles.HexNumber);
			int userInvId = int.Parse(strParts[1], NumberStyles.HexNumber);
			long dtStamp = long.Parse(strParts[2], NumberStyles.HexNumber);
			DateTime dateTime = DateTime.FromFileTime(dtStamp);
			AudioType audioType;
			if (!Enum.TryParse(strParts[3], out audioType))
				return null;
			string resId = Uri.UnescapeDataString(strParts[4]);
			string title = Uri.UnescapeDataString(strParts[5]);
			return new AudioLogEntry(id, audioType, resId, readIndex)
			{
				Timestamp = dateTime,
				Title = title,
				UserInvokeId = userInvId,
			};
		}

		private string AsHex(int num) { return num.ToString("X8"); }
		private string AsHex(long num) { return num.ToString("X16"); }

		public override string ToString()
		{
			return string.Format("{0} @ {1} by {2}: {3}, ({4})", Id, Timestamp, UserInvokeId, Title, RessourceId);
		}
	}
}