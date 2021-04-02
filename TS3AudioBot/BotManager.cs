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
using System.Threading.Tasks;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Helper;
using TSLib;
using TSLib.Helper;

namespace TS3AudioBot
{
	public class BotManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private List<Bot?>? activeBots = new List<Bot?>();
		private readonly object lockObj = new object();

		private readonly ConfRoot confRoot;
		private readonly CoreInjector coreInjector;

		public BotManager(ConfRoot confRoot, CoreInjector coreInjector)
		{
			this.confRoot = confRoot;
			this.coreInjector = coreInjector;
		}

		public async Task RunBots(bool interactive)
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

				var address = await Interactive.LoopActionAsync("Please enter the ip, domain or nickname (with port; default: 9987) where to connect to:", async addr =>
				{
					if (await TsDnsResolver.TryResolve(addr) != null)
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

				var runResult = await RunBot(newBot);
				if (!runResult.Ok)
					Log.Error("Could not run bot ({0})", runResult.Error);
				return;
			}

			var launchBotTasks = new List<Task<R<BotInfo, string>>>(botConfigList.Length);
			foreach (var instance in botConfigList)
			{
				if (!instance.Run)
					continue;
				launchBotTasks.Add(RunBot(instance).ContinueWith(async t =>
				{
					var result = await t;
					if (!result.Ok)
					{
						Log.Error("Could not instantiate bot: {0}", result.Error);
					}
					return result;
				}).Unwrap());
			}
			await Task.WhenAll(launchBotTasks);
		}

		public ConfBot CreateNewBot() => confRoot.CreateBot();

		public Task<R<BotInfo, string>> CreateAndRunNewBot()
		{
			var botConf = CreateNewBot();
			return RunBot(botConf);
		}

		public async Task<R<BotInfo, string>> RunBotTemplate(string name)
		{
			var config = confRoot.GetBotConfig(name);
			if (!config.Ok)
				return config.Error.Message;
			return await RunBot(config.Value);
		}

		public async Task<R<BotInfo, string>> RunBot(ConfBot config)
		{
			var (bot, info) = InstantiateNewBot(config);
			if (info != null)
				return info;

			if (bot is null)
				return "Failed to create new Bot";

			return await bot.Scheduler.InvokeAsync<R<BotInfo, string>>(async () =>
			{
				var initializeResult = await bot.Run();
				if (!initializeResult.Ok)
				{
					await StopBot(bot);
					return $"Bot failed to initialize ({initializeResult.Error})";
				}
				return bot.GetInfo();
			});
		}

		private (Bot? bot, BotInfo? info) InstantiateNewBot(ConfBot config)
		{
			lock (lockObj)
			{
				if (!string.IsNullOrEmpty(config.Name))
				{
					var maybeBot = GetBotSave(config.Name);
					if (maybeBot != null)
						return (null, maybeBot.GetInfo());
				}

				var id = GetFreeId();
				if (id == null)
					return (null, null); // "BotManager is shutting down"

				var botInjector = new BotInjector(coreInjector);
				botInjector.AddModule(botInjector);
				botInjector.AddModule(new Id(id.Value));
				botInjector.AddModule(config);
				if (!botInjector.TryCreate<Bot>(out var bot))
					return (null, null); // "Failed to create new Bot"
				InsertBot(bot);
				return (bot, null);
			}
		}

		// !! This method must be called with a lock on lockObj
		private void InsertBot(Bot bot)
		{
			if (activeBots is null)
				return;
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
		private Bot? GetBotSave(int id)
		{
			if (activeBots is null || id < 0 || id >= activeBots.Count)
				return null;
			return activeBots[id];
		}

		// !! This method must be called with a lock on lockObj
		private Bot? GetBotSave(string name)
		{
			if (name is null)
				throw new ArgumentNullException(nameof(name));
			if (activeBots is null)
				return null;
			return activeBots.Find(x => x?.Name == name);
		}

		public Bot? GetBotLock(int id)
		{
			Bot? bot;
			lock (lockObj)
			{
				bot = GetBotSave(id);
				if (bot is null)
					return null;
				if (bot.Id != id)
					throw new Exception("Got not matching bot id");
			}
			return bot;
		}

		public Bot? GetBotLock(string name)
		{
			Bot? bot;
			lock (lockObj)
			{
				bot = GetBotSave(name);
				if (bot is null)
					return null;
				if (bot.Name != name)
					throw new Exception("Got not matching bot name");
			}
			return bot;
		}

		internal void IterateAll(Action<Bot> body)
		{
			lock (lockObj)
			{
				if (activeBots is null)
					return;
				foreach (var bot in activeBots)
				{
					if (bot is null) continue;
					body(bot);
				}
			}
		}

		public async Task StopBot(Bot bot)
		{
			RemoveBot(bot);
			await bot.Scheduler.InvokeAsync(async () => await bot.Stop());
		}

		internal void RemoveBot(Bot bot)
		{
			lock (lockObj)
			{
				Bot? botInList;
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
				return activeBots.Where(x => x != null).Select(x => x!.GetInfo()).ToArray();
			}
		}

		public uint GetRunningBotCount()
		{
			lock (lockObj)
			{
				if (activeBots is null)
					return 0;
				return (uint)activeBots.Count(x => x != null);
			}
		}

		public async Task StopBots()
		{
			List<Bot?> disposeBots;
			lock (lockObj)
			{
				if (activeBots is null)
					return;

				disposeBots = activeBots;
				activeBots = null;
			}

			await Task.WhenAll(disposeBots.Where(x => x != null).Select(x => StopBot(x!)));
		}
	}
}
