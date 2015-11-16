using System;
using System.IO;
using TS3AudioBot.RessourceFactories;
using System.Collections.Generic;

namespace TS3AudioBot
{
	class HistoryManager
	{
		HistoryFile file;

		public HistoryManager()
		{

		}

		public void LogAudioRessource(AudioRessource ar)
		{
			file.Store(ar);
		}
	}

	class HistoryFile
	{
		List<int> FileEntryIndices;
		private const int CacheSize = 64;
		public string Path { get; private set; }

		// Cache tree/dict/array

		private void LoadFile(string path)
		{
			Path = path;
		}

		public AudioLogEntry GetEntryAt(int id)
		{
			throw new NotImplementedException();
		}

		public AudioLogEntry[] GetLastEntrys(int idFrom, int idAmount)
		{
			throw new NotImplementedException();
		}

		public void Store(AudioRessource ressource)
		{
			throw new NotImplementedException();
		}

		/* 
		- int[] index references for entries
		- lazy load last x entrys
		- > load all if >x

		*/
	}

	class AudioLogEntry
	{
		//[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		//[Indexed]
		public int UserInvokeId { get; set; }
		public AudioType AudioType { get; set; }
		public string RessourceId { get; set; }
		public DateTime Timestamp { get; set; }
		public string Title { get; set; }
	}
}
