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
	using Config;
	using Dependency;
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	public class BotManager : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private List<Bot> activeBots;
		private readonly object lockObj = new object();

		public ConfRoot Config { get; set; }
		public CoreInjector CoreInjector { get; set; }

		public BotManager()
		{
			Util.Init(out activeBots);
		}

		public void RunBots(bool interactive)
		{
			var botConfigList = Config.GetAllBots();
			if (botConfigList is null)
				return;

			if (botConfigList.Length == 0)
			{
				if (!interactive)
				{
					Log.Warn("No bots are configured in the load list.");
					return;
				}

				Log.Info("It seems like there are no bots configured.");
				Log.Info("Fill out this quick setup to get started.");

				var newBot = CreateNewBot();
				newBot.Run.Value = true;

				string address;
				while (true)
				{
					Console.WriteLine("Please enter the ip, domain or nickname (with port; default: 9987) where to connect to:");
					address = Console.ReadLine();
					if (address is null)
						return;
					if (TS3Client.TsDnsResolver.TryResolve(address, out var _))
						break;
					Console.WriteLine("The address seems invalid or could not be resolved, continue anyway? [y/N]");
					var cont = Console.ReadLine();
					if (string.Equals(cont, "y", StringComparison.InvariantCultureIgnoreCase))
						break;
				}
				newBot.Connect.Address.Value = address;
				Console.WriteLine("Please enter the server password (or leave empty for none):");
				newBot.Connect.ServerPassword.Password.Value = Console.ReadLine();

				if (!newBot.SaveNew(ConfigHelper.DefaultBotName))
				{
					Log.Error("Could not save new bot. Ensure that the bot has access to the directory.");
					return;
				}

				if (!Config.Save())
					Log.Error("Could not save root config. The bot won't start by default.");

				var runResult = RunBot(newBot);
				if (!runResult.Ok)
					Log.Error("Could not run bot ({0})", runResult.Error);
				return;
			}

			foreach (var instance in botConfigList)
			{
				if (!instance.Run)
					continue;
				var result = RunBot(instance);
				if (!result.Ok)
				{
					Log.Error("Could not instantiate bot: {0}", result.Error);
				}
			}
		}

		public ConfBot CreateNewBot() => Config.CreateBot();

		public R<BotInfo, string> CreateAndRunNewBot()
		{
			var botConf = CreateNewBot();
			return RunBot(botConf);
		}

		public R<BotInfo, string> RunBotTemplate(string name)
		{
			var config = Config.GetBotConfig(name);
			if (!config.Ok)
				return config.Error.Message;
			return RunBot(config.Value);
		}

		public R<BotInfo, string> RunBot(ConfBot config)
		{
			var bot = new Bot(config, CoreInjector.CloneRealm<BotInjector>());
			if (!CoreInjector.TryInject(bot))
				Log.Warn("Partial bot dependency loaded only");

			lock (bot.SyncRoot)
			{
				var initializeResult = bot.InitializeBot();
				var removeBot = false;
				if (initializeResult.Ok)
				{
					lock (lockObj)
					{
						if (!InsertIntoFreeId(bot))
							removeBot = true;
					}
				}
				else
				{
					return $"Bot failed to connect ({initializeResult.Error})";
				}

				if (removeBot)
				{
					StopBot(bot);
					return "BotManager is shutting down";
				}
			}
			return bot.GetInfo();
		}

		// !! This method must be called with a lock on lockObj
		private bool InsertIntoFreeId(Bot bot)
		{
			if (activeBots is null)
				return false;

			for (int i = 0; i < activeBots.Count; i++)
			{
				if (activeBots[i] is null)
				{
					activeBots[i] = bot;
					bot.Id = i;
					return true;
				}
			}

			// All slots are full, get a new slot
			activeBots.Add(bot);
			bot.Id = activeBots.Count - 1;
			return true;
		}

		// !! This method must be called with a lock on lockObj
		private Bot GetBotSave(int id)
		{
			if (activeBots is null || id < 0 || id >= activeBots.Count)
				return null;
			return activeBots[id];
		}

		// !! This method must be called with a lock on lockObj
		private Bot GetBotSave(string name)
		{
			if (name is null)
				throw new ArgumentNullException(nameof(name));
			if (activeBots is null)
				return null;
			return activeBots.Find(x => x?.Name == name);
		}

		public BotLock GetBotLock(int id)
		{
			Bot bot;
			lock (lockObj)
			{
				bot = GetBotSave(id);
				if (bot is null)
					return null;
				if (bot.Id != id)
					throw new Exception("Got not matching bot id");
			}
			return bot.GetBotLock();
		}

		public BotLock GetBotLock(string name)
		{
			Bot bot;
			lock (lockObj)
			{
				bot = GetBotSave(name);
				if (bot is null)
					return null;
				if (bot.Name != name)
					throw new Exception("Got not matching bot name");
			}
			return bot.GetBotLock();
		}

		public void StopBot(Bot bot)
		{
			RemoveBot(bot);
			bot.Dispose();
		}

		internal void RemoveBot(Bot bot)
		{
			lock (lockObj)
			{
				Bot botInList;
				if (activeBots != null
					&& (botInList = GetBotSave(bot.Id)) != null
					&& botInList == bot)
				{
					activeBots[bot.Id] = null;
				}
			}
		}

		public BotInfo[] GetBotInfolist()
		{
			lock (lockObj)
			{
				if (activeBots is null)
					return Array.Empty<BotInfo>();
				return activeBots.Where(x => x != null).Select(x => x.GetInfo()).ToArray();
			}
		}

		public void Dispose()
		{
			List<Bot> disposeBots;
			lock (lockObj)
			{
				if (activeBots is null)
					return;

				disposeBots = activeBots;
				activeBots = null;
			}

			foreach (var bot in disposeBots.Where(x => x != null))
			{
				StopBot(bot);
			}
		}
	}

	public class BotLock : IDisposable
	{
		public Bot Bot { get; }

		internal BotLock(Bot bot)
		{
			Bot = bot;
		}

		public void Dispose()
		{
			Monitor.Exit(Bot.SyncRoot);
		}
	}
}
