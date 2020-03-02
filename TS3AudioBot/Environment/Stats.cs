// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

//#define FEATURE_TRACK

using LiteDB;
using Newtonsoft.Json;
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
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const string StatsTable = "stats";
		private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5); // = TimeSpan.FromHours(1);
		private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(15); // = TimeSpan.FromDays(7);

		private readonly ConfRoot conf;
		private readonly DbStore database;
		private readonly BotManager botManager;
		private TickWorker ticker;
		private readonly object lockObj = new object();
		private bool uploadParamEnabled;

		private DateTime runtimeLastTrack;
		private LiteCollection<StatsData> trackEntries;
		private StatsPoints statsPoints = new StatsPoints();
		private StatsData CurrentStatsData => statsPoints.LastCheck;
		private bool UploadEnabled => uploadParamEnabled && conf.Configs.SendStats;

		private const int StatsVersion = 1;

		// Track loops:
		// Minute [60]
		// Hour   [24]
		// Day    [07]
		// Week   1

		public Stats(ConfRoot conf, DbStore database, BotManager botManager)
		{
			this.conf = conf;
			this.database = database;
			this.botManager = botManager;
			uploadParamEnabled = true;

			runtimeLastTrack = Tools.Now;

#if FEATURE_TRACK
			ReadAndUpgradeStats();
#endif
		}

		private void ReadAndUpgradeStats()
		{
			var meta = database.GetMetaData(StatsTable);
			trackEntries = database.GetCollection<StatsData>(StatsTable);
			bool save = false;

			if (meta.Version != StatsVersion)
			{
				statsPoints.LastCheck.LastWrite = Tools.Now;
				statsPoints.LastWeek.LastWrite = Tools.Now;
				statsPoints.Overall.LastWrite = Tools.Now;
				meta.Version = StatsVersion;
				meta.CustomData = JsonConvert.SerializeObject(CurrentStatsData);
				save = true;
			}
			else
			{
				statsPoints = JsonConvert.DeserializeObject<StatsPoints>(meta.CustomData);
				// Upgrade steps here
			}

			save = false;
			if (save)
				database.UpdateMetaData(meta);
		}

		public void StartTimer(bool upload)
		{
			uploadParamEnabled = upload;
			if (ticker != null)
				throw new InvalidOperationException();
#if FEATURE_TRACK
			ticker = TickPool.RegisterTick(() => SaveRunningValues(true), CheckInterval, true);
#endif
		}

		private void SendStats(StatsPing sendPacket)
		{
			try
			{
				var sendData = JsonConvert.SerializeObject(sendPacket);
				var sendBytes = Tools.Utf8Encoder.GetBytes(sendData);
				//sendBytes = Tools.Utf8Encoder.GetBytes("{ \"Platform\": \"Win\" }");

				var request = WebWrapper.CreateRequest(new Uri("http://127.0.0.1:50580/api/tab/stats")).Unwrap(); // "https://splamy.de/api/tab/stats"
				request.Method = "POST";
				request.ContentType = "application/json";
				request.ContentLength = sendBytes.Length;
				using (var rStream = request.GetRequestStream())
					rStream.Write(sendBytes, 0, sendBytes.Length);
				//using (var sw = new StreamWriter(rStream, Tools.Utf8Encoder))
				//{
				//	var serializer = new JsonSerializer
				//	{
				//		Formatting = Formatting.None,
				//	};
				//	serializer.Serialize(sw, sendPacket);
				//}
				using (var response = request.GetResponse()) ;
			}
			catch (Exception ex) { Log.Info(ex, "Could not upload stats"); }
		}

		private StatsPing GetWeeklyStats()
		{
			var sendPacket = new StatsPing
			{
				BotVersion = SystemData.AssemblyData.ToString(),
				Platform = SystemData.PlatformData,
				Runtime = SystemData.RuntimeData.FullName,
				RunningBots = botManager.GetRunningBotCount(),
				TrackTime = Tools.Now - runtimeLastTrack,
			};
			lock (lockObj)
			{
				sendPacket.SongStats = new Dictionary<string, StatsFactory>(statsPoints.LastWeek.SongStats);
				sendPacket.Commands = statsPoints.LastWeek.Commands;
				sendPacket.TotalUptime = statsPoints.LastWeek.TotalUptime;
			}
			return sendPacket;
		}

		public void TrackSongLoad(string factory, bool successful, bool fromUser)
		{
			lock (lockObj)
			{
				var statsFactory = CurrentStatsData.SongStats.GetOrNew(factory ?? "");
				statsFactory.Requests++;
				if (successful) statsFactory.Loaded++;
				if (fromUser) statsFactory.FromUser++;
			}
		}

		public void TrackSongPlayback(string factory, TimeSpan time)
		{
			lock (lockObj)
			{
				var statsFactory = CurrentStatsData.SongStats.GetOrNew(factory ?? "");
				statsFactory.Playtime += time;
			}
		}

		public void TrackCommandCall(bool byUser)
		{
			lock (lockObj)
			{
				CurrentStatsData.Commands.CommandCalls++;
				if (byUser) CurrentStatsData.Commands.FromUser++;
			}
		}

		private void TrackRuntime()
		{
			var now = Tools.Now;
			var trackTime = now - runtimeLastTrack;
			runtimeLastTrack = now;
			CurrentStatsData.TotalUptime += trackTime;
			Log.Info("Track Runtime: {tt} {@cur}", trackTime, CurrentStatsData);
		}

		private void SaveRunningValues(bool upload)
		{
			TrackRuntime();

			var now = Tools.Now;
			StatsPing sendData = null;

			lock (lockObj)
			{
				Log.Info("LastCheck: {@data}", statsPoints.LastCheck);
				if (statsPoints.LastCheck.LastWrite + CheckInterval < now)
				{
					Log.Info("LastWeek PRE: {@data}", statsPoints.LastWeek);
					statsPoints.LastWeek.Add(statsPoints.LastCheck);
					statsPoints.LastCheck.Reset();
					statsPoints.LastCheck.LastWrite = now;
				}
				Log.Info("LastWeek: {@data}", statsPoints.LastWeek);
				if (statsPoints.LastWeek.LastWrite + SendInterval < now)
				{
					if (upload && UploadEnabled)
						sendData = GetWeeklyStats();

					statsPoints.Overall.Add(statsPoints.LastWeek);
					statsPoints.LastWeek.Reset();
					statsPoints.LastWeek.LastWrite = now;
				}
			}

			if (sendData != null)
				SendStats(sendData);
		}

		private string GetSerializedData()
		{
			return JsonConvert.SerializeObject(statsPoints);
		}
	}

	internal class StatsPing
	{
		// Meta
		public string BotVersion { get; set; }
		public string Platform { get; set; }
		public string Runtime { get; set; }
		public uint RunningBots { get; set; }
		public TimeSpan TrackTime { get; set; }
		// StatsData
		public TimeSpan TotalUptime { get; set; }
		public IDictionary<string, StatsFactory> SongStats { get; set; }
		public StatsCommands Commands { get; set; }
	}

	internal class StatsPoints
	{
		public StatsData LastCheck { get; set; } = new StatsData();
		public StatsData LastWeek { get; set; } = new StatsData();
		public StatsData Overall { get; set; } = new StatsData();

		public LinkedList<StatsData> Minutes { get; set; } = new LinkedList<StatsData>();
		public LinkedList<StatsData> Hours { get; set; } = new LinkedList<StatsData>();
		public LinkedList<StatsData> Days { get; set; } = new LinkedList<StatsData>();
	}

	internal class StatsData
	{
		public DateTime LastWrite = DateTime.MinValue;

		public TimeSpan TotalUptime { get; set; } = TimeSpan.Zero;
		public Dictionary<string, StatsFactory> SongStats { get; set; } = new Dictionary<string, StatsFactory>();
		public StatsCommands Commands { get; set; } = new StatsCommands();

		public void Add(StatsData other)
		{
			TotalUptime += other.TotalUptime;
			foreach (var kvp in other.SongStats)
				SongStats.GetOrNew(kvp.Key).Add(kvp.Value);
			Commands.Add(other.Commands);
		}

		public void Reset()
		{
			TotalUptime = TimeSpan.Zero;
			SongStats.Clear();
			Commands.Reset();
		}
	}

	internal class StatsFactory
	{
		public uint Requests { get; set; } = 0;
		public uint Loaded { get; set; } = 0;
		///<summary>How many actually were started by a user (and not i.e. from a playlist)</summary>
		public uint FromUser { get; set; } = 0;
		public uint SearchRequests { get; set; } = 0;
		public TimeSpan Playtime { get; set; } = TimeSpan.Zero;

		public void Add(StatsFactory other)
		{
			Requests += other.Requests;
			Loaded += other.Loaded;
			FromUser += other.FromUser;
			Playtime += other.Playtime;
			SearchRequests += other.SearchRequests;
		}

		public void Reset()
		{
			Requests = 0;
			Loaded = 0;
			FromUser = 0;
			Playtime = TimeSpan.Zero;
			SearchRequests = 0;
		}
	}

	internal class StatsCommands
	{
		public uint CommandCalls { get; set; } = 0;
		///<summary>How many actually were started by a user (and not i.e. by event)</summary>
		public uint FromUser { get; set; } = 0;

		public void Add(StatsCommands other)
		{
			CommandCalls += other.CommandCalls;
			FromUser += other.FromUser;
		}

		public void Reset()
		{
			CommandCalls = 0;
			FromUser = 0;
		}
	}
}
