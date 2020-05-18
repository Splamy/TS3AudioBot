// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using LiteDB;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TSLib.Helper;
using TSLib.Scheduler;

namespace TS3AudioBot.Environment
{
	public class Stats
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const string StatsTable = "stats";
		private const string StatsTableAcc = "stats_acc";
		private const int OverallId = 1;
		private const int StatsVersion = 1;
		private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
		private static readonly TimeSpan SendInterval = TimeSpan.FromDays(1);
		private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.None,
		};

		private readonly ConfRoot conf;
		private readonly DbStore database;
		private readonly BotManager botManager;
		private readonly TickWorker ticker;
		private bool uploadParamEnabled;
		private bool UploadEnabled => uploadParamEnabled && conf.Configs.SendStats;

		private readonly DbMetaData meta;
		private readonly StatsData overallStats;
		private readonly StatsMeta statsPoints;
		private readonly LiteCollection<StatsData> trackEntries;
		private readonly LiteCollection<StatsData> accEntries;
		private readonly StatsData CurrentStatsData = new StatsData()
		{
			SongStats = new ConcurrentDictionary<string, StatsFactory>()
		};
		private DateTime runtimeLastTrack;
		// bot id -> factory
		private readonly ConcurrentDictionary<Id, string> runningSongsPerFactory = new ConcurrentDictionary<Id, string>();

		public Stats(ConfRoot conf, DbStore database, BotManager botManager, DedicatedTaskScheduler scheduler)
		{
			this.conf = conf;
			this.database = database;
			this.botManager = botManager;
			uploadParamEnabled = true;
			runtimeLastTrack = Tools.Now;
			ticker = scheduler.CreateTimer(TrackPoint, CheckInterval, false);

			meta = database.GetMetaData(StatsTable);
			trackEntries = database.GetCollection<StatsData>(StatsTable);
			trackEntries.EnsureIndex(x => x.Id, true);
			trackEntries.EnsureIndex(x => x.Time);
			accEntries = database.GetCollection<StatsData>(StatsTableAcc);
			accEntries.EnsureIndex(x => x.Id, true);

			if (meta.Version != StatsVersion || meta.CustomData is null)
			{
				statsPoints = new StatsMeta
				{
					LastSend = Tools.Now,
				};
				meta.Version = StatsVersion;
				UpdateMeta();
			}
			else
			{
				statsPoints = JsonConvert.DeserializeObject<StatsMeta>(meta.CustomData, JsonSettings) ?? new StatsMeta();
				// Upgrade steps here
			}

			overallStats = accEntries.FindById(OverallId) ?? new StatsData
			{
				Id = OverallId
			};
		}

		private void UpdateMeta()
		{
			meta.CustomData = JsonConvert.SerializeObject(statsPoints, JsonSettings);
			database.UpdateMetaData(meta);
		}

		public void StartTimer(bool upload)
		{
			uploadParamEnabled = upload;
			ticker.Enable();
		}

		private async Task SendStats(StatsPing sendPacket)
		{
			try
			{
				Log.Debug("Send: {@data}", sendPacket);
				var request = WebWrapper
					.Request("https://splamy.de/api/tab/stats")
					.WithMethod(HttpMethod.Post);
				request.Content = new StringContent(JsonConvert.SerializeObject(sendPacket), Tools.Utf8Encoder, "application/json");
				await request.ToAction(response =>
				{
					Log.Debug("Stats response {0}", response.StatusCode);
					return Task.CompletedTask;
				});
			}
			catch (Exception ex) { Log.Debug(ex, "Could not upload stats"); }
		}

		private StatsPing GetStatsTill(DateTime date)
		{
			var sendPacket = GetDefaultStatsPing();

			uint count = 0;
			uint avgBots = 0;
			var entries = trackEntries.Find(x => x.Time > date);
			foreach (var entry in entries)
			{
				count++;
				sendPacket.Add(entry);
				avgBots += entry.RunningBots;
			}
			sendPacket.RunningBots = count == 0 ? 0 : (uint)(avgBots / (float)count + .5f);
			return sendPacket;
		}

		private async void TrackPoint()
		{
			var nextId = statsPoints.GenNextIndex();

			var now = Tools.Now;
			CurrentStatsData.Time = now;
			CurrentStatsData.Id = nextId;
			var trackTime = now - runtimeLastTrack;
			runtimeLastTrack = now;
			CurrentStatsData.TotalUptime += trackTime;
			CurrentStatsData.RunningBots = botManager.GetRunningBotCount();
			CurrentStatsData.BotsRuntime = TimeSpan.FromTicks(trackTime.Ticks * CurrentStatsData.RunningBots);
			foreach (var factory in runningSongsPerFactory.Values)
				CurrentStatsData.SongStats.GetOrNew(factory).Playtime += trackTime;

			Log.Debug("Track: {@data}", CurrentStatsData);
			trackEntries.Upsert(CurrentStatsData);
			overallStats.Add(CurrentStatsData);
			accEntries.Upsert(overallStats);
			CurrentStatsData.Reset();

			if (UploadEnabled && statsPoints.LastSend + SendInterval < now)
			{
				var sendData = GetStatsTill(statsPoints.LastSend);
				await SendStats(sendData);
				statsPoints.LastSend = now;
			}

			UpdateMeta();
		}

		// Track operations

		public void TrackSongLoad(string? factory, bool successful, bool fromUser)
		{
			var statsFactory = CurrentStatsData.SongStats.GetOrNew(factory ?? "");
			statsFactory.PlayRequests++;
			if (successful) statsFactory.PlaySucessful++;
			if (fromUser) statsFactory.PlayFromUser++;
		}

		public void TrackCommandCall(bool byUser)
		{
			CurrentStatsData.CommandCalls++;
			if (byUser) CurrentStatsData.CommandFromUser++;
		}

		public void TrackCommandApiCall()
		{
			CurrentStatsData.CommandCalls++;
			CurrentStatsData.CommandFromApi++;
		}

		public void TrackSongStart(Id bot, string factory)
		{
			factory ??= "";
			runningSongsPerFactory[bot] = factory;
			var statsFactory = CurrentStatsData.SongStats.GetOrNew(factory);
			statsFactory.Playtime -= (Tools.Now - runtimeLastTrack);
		}

		public void TrackSongStop(Id bot)
		{
			if (runningSongsPerFactory.TryRemove(bot, out var factory))
			{
				var statsFactory = CurrentStatsData.SongStats.GetOrNew(factory);
				statsFactory.Playtime += (Tools.Now - runtimeLastTrack);
			}
		}

		private static StatsPing GetDefaultStatsPing()
		{
			return new StatsPing
			{
				BotVersion = SystemData.AssemblyData.ToString(),
				Platform = SystemData.PlatformData,
				Runtime = SystemData.RuntimeData.FullName,
			};
		}

		public static string CreateExample()
		{
			var sendData = GetDefaultStatsPing();
			sendData.TotalUptime = TimeSpan.FromHours(12.34);
			sendData.BotsRuntime = TimeSpan.FromHours(4.20);
			sendData.CommandCalls = 1234;
			sendData.CommandFromApi = 100;
			sendData.RunningBots = 3;
			sendData.SongStats = new Dictionary<string, StatsFactory>()
			{
				{ "youtube", new StatsFactory {
					PlayRequests = 100,
					PlayFromUser = 42,
					Playtime = TimeSpan.FromMinutes(12.34),
					SearchRequests = 5,
				}}
			};

			return JsonConvert.SerializeObject(sendData, Formatting.Indented);
		}
	}

	internal class StatsPing : StatsData
	{
		// Meta
		public string? BotVersion { get; set; }
		public string? Platform { get; set; }
		public string? Runtime { get; set; }
	}

	internal class StatsMeta
	{
		public const int RingOff = 1;
		public const int RingSize = 60 * 24 * 7; /* min * day * week */

		public int CurrentIndex { get; set; } = 0;
		public DateTime LastSend = DateTime.MinValue;

		public int GenNextIndex()
		{
			CurrentIndex = (CurrentIndex + 1) % RingSize;
			return CurrentIndex + RingOff;
		}
	}

	internal class StatsData
	{
		[JsonIgnore]
		public int Id { get; set; }
		[JsonIgnore]
		public DateTime Time { get; set; }
		public uint RunningBots { get; set; }
		public TimeSpan BotsRuntime { get; set; } = TimeSpan.Zero;

		public TimeSpan TotalUptime { get; set; } = TimeSpan.Zero;
		public IDictionary<string, StatsFactory> SongStats { get; set; } = new Dictionary<string, StatsFactory>();

		public uint CommandCalls { get; set; }
		///<summary>How many actually were started by a user (and not i.e. by event)</summary>
		public uint CommandFromUser { get; set; }
		public uint CommandFromApi { get; set; }

		public bool ShouldSerializeSongStats() => SongStats.Count > 0;
		public bool ShouldSerializeCommandCalls() => CommandCalls != 0;
		public bool ShouldSerializeCommandFromUser() => CommandFromUser != 0;
		public bool ShouldSerializeCommandFromApi() => CommandFromApi != 0;

		public void Add(StatsData other)
		{
			TotalUptime += other.TotalUptime;
			BotsRuntime += other.BotsRuntime;
			foreach (var kvp in other.SongStats)
				SongStats.GetOrNew(kvp.Key).Add(kvp.Value);
			CommandCalls += other.CommandCalls;
			CommandFromUser += other.CommandFromUser;
			CommandFromApi += other.CommandFromApi;
		}

		public void Reset()
		{
			TotalUptime = TimeSpan.Zero;
			RunningBots = 0;
			BotsRuntime = TimeSpan.Zero;
			SongStats.Clear();
			CommandCalls = 0;
			CommandFromUser = 0;
			CommandFromApi = 0;
		}
	}

	internal class StatsFactory
	{
		public uint PlayRequests { get; set; }
		public uint PlaySucessful { get; set; }
		///<summary>How many actually were started by a user (and not i.e. from a playlist)</summary>
		public uint PlayFromUser { get; set; }
		public uint SearchRequests { get; set; }
		public TimeSpan Playtime { get; set; }

		public bool ShouldSerializePlayRequests() => PlayRequests != 0;
		public bool ShouldSerializePlaySucessful() => PlaySucessful != 0;
		public bool ShouldSerializePlayFromUser() => PlayFromUser != 0;
		public bool ShouldSerializeSearchRequests() => SearchRequests != 0;
		public bool ShouldSerializePlaytime() => Playtime != TimeSpan.Zero;

		public void Add(StatsFactory other)
		{
			PlayRequests += other.PlayRequests;
			PlaySucessful += other.PlaySucessful;
			PlayFromUser += other.PlayFromUser;
			SearchRequests += other.SearchRequests;
			Playtime += other.Playtime;
		}

		public void Reset()
		{
			PlayRequests = 0;
			PlaySucessful = 0;
			PlayFromUser = 0;
			SearchRequests = 0;
			Playtime = TimeSpan.Zero;
		}
	}
}
