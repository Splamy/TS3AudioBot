using System;
using System.Data.SQLite;
using System.IO;
using TS3AudioBot.RessourceFactories;

namespace TS3AudioBot
{
	class HistoryManager
	{
		SQLiteConnection sqlConnection;

		readonly SQLiteCommand createLogTableCommand = new SQLiteCommand(
			@"create table if not exists AudioLogTable
			(
			Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
			UserInvokeId INTEGER,
			AudioType INTEGER,
			Ressource TEXT,
			Timestamp DATETIME,
			Title TEXT
			)");

		public HistoryManager()
		{

		}

		public void LoadDatabase()
		{
			string dbPath = Util.GetFilePath(FilePath.HistoryFile);

			if (!File.Exists(dbPath))
			{
				SQLiteConnection.CreateFile(dbPath);
			}
			sqlConnection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
			sqlConnection.Open();
			createLogTableCommand.Connection = sqlConnection;
			createLogTableCommand.ExecuteNonQuery();
		}

		public void LogAudioRessource(AudioRessource ar)
		{
			//...
		}
	}

	class AudioLogEntry
	{
		//[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		//[Indexed]
		public int UserInvokeId { get; set; }
		public AudioType AudioType { get; set; }
		public string Ressource { get; set; }
		public DateTime Timestamp { get; set; }
		public string Title { get; set; }
	}
}
