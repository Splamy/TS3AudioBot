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
		private readonly List<Bot> activeBots;

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
				lock (activeBots)
				{
					if (activeBots.Count == 0)
					{
						if (!CreateBot())
						{
							Thread.Sleep(1000);
						}
					}
				}
				Thread.Sleep(200);
			}
		}

		public bool CreateBot(/*Ts3FullClientData bot*/)
		{
			string error = string.Empty;
			var bot = new Bot(Core);
			try
			{
				if (bot.InitializeBot())
				{
					lock (activeBots)
					{
						activeBots.Add(bot);
					}
					return true;
				}
			}
			catch (Exception ex) { error = ex.ToString(); }

			bot.Dispose();
			Log.Write(Log.Level.Warning, "Could not create new Bot ({0})", error);
			return false;
		}

		public Bot GetBot(int id)
		{
			lock (activeBots)
			{
				return id < activeBots.Count
					? activeBots[id]
					: null;
			}
		}

		public void StopBot(Bot bot)
		{
			lock (activeBots)
			{
				if (activeBots.Remove(bot))
				{
					bot.Dispose();
				}
			}
		}

		public void Dispose()
		{
			isRunning = false;
			lock (activeBots)
			{
				var bots = activeBots.ToArray();
				foreach (var bot in bots)
				{
					StopBot(bot);
				}
			}
		}
	}
}
