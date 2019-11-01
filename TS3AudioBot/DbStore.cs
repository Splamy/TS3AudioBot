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
using System.IO;
using TS3AudioBot.Config;

namespace TS3AudioBot
{
	public class DbStore : IDisposable
	{
		private const string DbMetaInformationTable = "dbmeta";

		private readonly LiteDatabase database;
		private readonly LiteCollection<DbMetaData> metaTable;

		public DbStore(ConfDb config)
		{
			var historyFile = Path.GetFullPath(config.Path);
			database = new LiteDatabase(historyFile);

			metaTable = database.GetCollection<DbMetaData>(DbMetaInformationTable);
		}

		public DbMetaData GetMetaData(string table)
		{
			var meta = metaTable.FindById(table);
			if (meta is null)
			{
				meta = new DbMetaData { Id = table, Version = 0, CustomData = null };
				metaTable.Insert(meta);
			}
			return meta;
		}

		public void UpdateMetaData(DbMetaData metaData)
		{
			metaTable.Update(metaData);
		}

		public LiteCollection<T> GetCollection<T>(string name)
		{
			return database.GetCollection<T>(name);
		}

		public void CleanFile()
		{
			database.Shrink();
		}

		public void Dispose()
		{
			database.Dispose();
		}
	}

	public class DbMetaData
	{
		public string Id { get; set; }
		public int Version { get; set; }
		public object CustomData { get; set; }
	}
}
