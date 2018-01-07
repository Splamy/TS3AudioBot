// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Threading;

	public class BotManager : Dependency.ICoreModule, IDisposable
	{
		private bool isRunning;
		public Core Core { get; set; }
		private List<Bot> activeBots;
		private readonly object lockObj = new object();

		public BotManager()
		{
			isRunning = true;
			Util.Init(out activeBots);
		}

		public void Initialize() { }

		public void WatchBots()
		{
			while (isRunning)
			{
				lock (lockObj)
				{
					if (activeBots.Count == 0)
					{
						if (!CreateBot())
						{
							Thread.Sleep(1000);
						}
					}

					CleanStrayBots();
				}
				Thread.Sleep(200);
			}
		}

		private void CleanStrayBots()
		{
			List<Bot> strayList = null;
			lock (lockObj)
			{
				foreach (var bot in activeBots)
				{
					var client = bot.QueryConnection.GetLowLibrary<TS3Client.Full.Ts3FullClient>();
					if (!client.Connected && !client.Connecting)
					{
						Log.Write(Log.Level.Warning, "Cleaning up stray bot.");
						strayList = strayList ?? new List<Bot>();
						strayList.Add(bot);
					}
				}
			}

			if (strayList != null)
				foreach (var bot in strayList)
					StopBot(bot);
		}

		public bool CreateBot(/*Ts3FullClientData bot*/)
		{
			bool removeBot = false;
			var bot = new Bot(Core);
			if (bot.InitializeBot())
			{
				lock (lockObj)
				{
					activeBots.Add(bot);
					removeBot = !isRunning;
				}
			}
			if (removeBot)
			{
				StopBot(bot);
				return false;
			}
			return true;
		}

		public Bot GetBot(int id)
		{
			lock (lockObj)
			{
				if (!isRunning)
					return null;
				return id < activeBots.Count
					? activeBots[id]
					: null;
			}
		}

		public void StopBot(Bot bot)
		{
			bool tookBot = false;
			lock (lockObj)
			{
				tookBot = activeBots.Remove(bot);
			}
			if (tookBot)
			{
				bot.Dispose();
			}
		}

		public void Dispose()
		{
			List<Bot> disposeBots;
			lock (lockObj)
			{
				isRunning = false;
				disposeBots = activeBots;
				activeBots = new List<Bot>();
			}

			foreach (var bot in disposeBots)
			{
				StopBot(bot);
			}
		}
	}
}
