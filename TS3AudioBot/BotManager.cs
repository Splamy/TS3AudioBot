// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Helper;
using TSLib.Helper;

namespace TS3AudioBot
{
	public class BotManager : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private List<Bot> activeBots = new List<Bot>();
		private readonly object lockObj = new object();

		private readonly ConfRoot confRoot;
		private readonly CoreInjector coreInjector;

		public BotManager(ConfRoot confRoot, CoreInjector coreInjector)
		{
			this.confRoot = confRoot;
			this.coreInjector = coreInjector;
		}

		public void RunBots(bool interactive)
		{
			var botConfigList = confRoot.GetAllBots();
			if (botConfigList is null)
				return;

			if (botConfigList.Length == 0)
			{
				if (!interactive)
				{
					Log.Warn("No bots are configured in the load list.");
					return;
				}

				Console.WriteLine("It seems like there are no bots configured.");
				Console.WriteLine("Fill out this quick setup to get started.");

				var newBot = CreateNewBot();
				newBot.Run.Value = true;

				string address = Interactive.LoopAction("Please enter the ip, domain or nickname (with port; default: 9987) where to connect to:", addr =>
				{
					if (TSLib.TsDnsResolver.TryResolve(addr, out _))
						return true;
					Console.WriteLine("The address seems invalid or could not be resolved, continue anyway? [y/N]");
					return Interactive.UserAgree(defaultTo: false);
				});
				if (address is null)
					return;
				newBot.Connect.Address.Value = address;
				Console.WriteLine("Please enter the server password (or leave empty for none):");
				newBot.Connect.ServerPassword.Password.Value = Console.ReadLine();

				if (!newBot.SaveNew(ConfigHelper.DefaultBotName))
				{
					Log.Error("Could not save new bot. Ensure that the bot has access to the directory.");
					return;
				}

				if (!confRoot.Save())
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

		public ConfBot CreateNewBot() => confRoot.CreateBot();

		public R<BotInfo, string> CreateAndRunNewBot()
		{
			var botConf = CreateNewBot();
			return RunBot(botConf);
		}

		public R<BotInfo, string> RunBotTemplate(string name)
		{
			var config = confRoot.GetBotConfig(name);
			if (!config.Ok)
				return config.Error.Message;
			return RunBot(config.Value);
		}

		public R<BotInfo, string> RunBot(ConfBot config)
		{
			Bot bot;

			lock (lockObj)
			{
				if (!string.IsNullOrEmpty(config.Name))
				{
					bot = GetBotSave(config.Name);
					if (bot != null)
						return bot.GetInfo();
				}

				var id = GetFreeId();
				if (id == null)
					return "BotManager is shutting down";

				var botInjector = new BotInjector(coreInjector);
				botInjector.AddModule(botInjector);
				botInjector.AddModule(new Id(id.Value));
				botInjector.AddModule(config);
				if (!botInjector.TryCreate(out bot))
					return "Failed to create new Bot";
				InsertBot(bot);
			}

			lock (bot.SyncRoot)
			{
				var initializeResult = bot.InitializeBot();
				if (!initializeResult.Ok)
				{
					StopBot(bot);
					return $"Bot failed to connect ({initializeResult.Error})";
				}
			}
			return bot.GetInfo();
		}

		// !! This method must be called with a lock on lockObj
		private void InsertBot(Bot bot)
		{
			activeBots[bot.Id] = bot;
		}

		// !! This method must be called with a lock on lockObj
		// !! The id must be either used withing the same lock or considered invalid.
		private int? GetFreeId()
		{
			if (activeBots is null)
				return null;

			for (int i = 0; i < activeBots.Count; i++)
			{
				if (activeBots[i] is null)
				{
					return i;
				}
			}

			// All slots are full, get a new slot
			activeBots.Add(null);
			return activeBots.Count - 1;
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
