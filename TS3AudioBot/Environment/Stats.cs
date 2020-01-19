// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using NLog.Time;
using System;
using System.Collections.Generic;
using System.IO;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TSLib.Helper;

namespace TS3AudioBot.Environment
{
	public class Stats
	{
		private const string StatsTable = "stats";
		private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
		private static readonly TimeSpan SendInterval = TimeSpan.FromDays(7);

		private readonly ConfRoot conf;
		private readonly DbStore database;
		private readonly BotManager botManager;
		private TickWorker ticker;
		private readonly object lockObj = new object();

		private DateTime runtimeLastTrack;
		private DbMetaData dbMeta;
		private StatsData statsData;

		private const int StatsVersion = 1;

		public Stats(ConfRoot conf, DbStore database, BotManager botManager)
		{
			this.conf = conf;
			this.database = database;
			this.botManager = botManager;

			runtimeLastTrack = Tools.Now;

			ReadAndUpgradeStats();
		}

		private void ReadAndUpgradeStats()
		{
			var meta = database.GetMetaData(StatsTable);
			bool save = false;

			if (meta.CustomData is null || meta.Version != StatsVersion)
			{
				statsData = new StatsData()
				{
					LastSend = Tools.Now,
				};
				meta.Version = StatsVersion;
				meta.CustomData = JsonConvert.SerializeObject(statsData);
				save = true;
			}
			else
			{
				statsData = JsonConvert.DeserializeObject<StatsData>(meta.CustomData);
				// Upgrade steps here
			}

			save = false; // TODO REMOVE WHEN NOT IN DEBUG
			if (save)
				database.UpdateMetaData(meta);
		}

		public void StartSendStats()
		{
			if (ticker != null)
				throw new InvalidOperationException();
			ticker = TickPool.RegisterTick(CheckStats, CheckInterval, true);
		}

		public void CheckStats()
		{
			SaveRunningValues();
			SendStats();
		}

		private void SendStats()
		{
			return; // TODO REMOVE WHEN NOT IN DEBUG

			if (!conf.Configs.SendStats)
				return;

			if (statsData.LastSend + SendInterval < Tools.Now)
				return;

			var sendPacket = new StatsPing
			{
				BotVersion = SystemData.AssemblyData.ToString(),
				Platform = SystemData.PlatformData,
				Runtime = SystemData.RuntimeData.FullName,
				RunningBots = botManager.GetRunningBotCount(),
			};
			lock (lockObj)
			{
				sendPacket.SongStats = new Dictionary<string, StatsFactory>(statsData.SongStats);
				sendPacket.Commands = statsData.Commands;
				sendPacket.TotalUptime = statsData.TotalUptime;
			}

			try
			{
				var request = WebWrapper.CreateRequest(new Uri("https://splamy.de/api/tab/stats")).Unwrap();
				request.Method = "POST";
				request.ContentType = "application/json";
				using (var response = request.GetResponse())
				using (var stream = response.GetResponseStream())
				using (var sw = new StreamWriter(stream, Tools.Utf8Encoder))
				{
					var serializer = new JsonSerializer
					{
						Formatting = Formatting.None,
					};
					serializer.Serialize(sw, sendPacket);
				}
			}
			catch (Exception) { }
		}

		public void TrackSongLoad(string factory, bool successful, bool fromUser)
		{
			lock (lockObj)
			{
				var statsFactory = statsData.SongStats.GetOrNew(factory ?? "");
				statsFactory.Requests++;
				if (successful) statsFactory.Loaded++;
				if (fromUser) statsFactory.FromUser++;
			}
		}

		public void TrackSongPlayback(string factory, TimeSpan time)
		{
			lock (lockObj)
			{
				var statsFactory = statsData.SongStats.GetOrNew(factory ?? "");
				statsFactory.Playtime += time;
			}
		}

		public void TrackCommandCall(bool byUser)
		{
			lock (lockObj)
			{
				statsData.Commands.CommandCalls++;
				if (byUser) statsData.Commands.FromUser++;
			}
		}

		private void TrackRuntime()
		{
			var now = Tools.Now;
			var trackTime = now - runtimeLastTrack;
			runtimeLastTrack = now;
			statsData.TotalUptime += trackTime;
		}

		public void SaveRunningValues()
		{
			TrackRuntime();

			dbMeta.CustomData = JsonConvert.SerializeObject(statsData);
			database.UpdateMetaData(dbMeta);
		}

		internal string GetDebugString()
		{
			return JsonConvert.SerializeObject(statsData);
		}
	}

	internal class StatsPing
	{
		// Meta
		public string BotVersion { get; set; }
		public string Platform { get; set; }
		public string Runtime { get; set; }
		public uint RunningBots { get; set; }
		// StatsData
		public TimeSpan TotalUptime { get; set; }
		public IDictionary<string, StatsFactory> SongStats { get; set; }
		public StatsCommands Commands { get; set; }
	}

	internal class StatsData
	{
		public DateTime LastSend = DateTime.MinValue;

		public TimeSpan TotalUptime { get; set; } = TimeSpan.Zero;
		public Dictionary<string, StatsFactory> SongStats { get; set; } = new Dictionary<string, StatsFactory>();
		public StatsCommands Commands { get; set; } = new StatsCommands();
	}

	internal class StatsFactory
	{
		public uint Requests { get; set; } = 0;
		public uint Loaded { get; set; } = 0;
		///<summary>How many actually were started by a user (and not i.e. from a playlist)</summary>
		public uint FromUser { get; set; } = 0;
		public TimeSpan Playtime { get; set; } = TimeSpan.Zero;
	}

	internal class StatsCommands
	{
		public uint CommandCalls { get; set; } = 0;
		///<summary>How many actually were started by a user (and not i.e. by event)</summary>
		public uint FromUser { get; set; } = 0;
	}
}
