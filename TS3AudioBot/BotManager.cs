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

		private bool isRunning;
		private List<Bot> activeBots;
		private readonly object lockObj = new object();

		public ConfRoot Config { get; set; }
		public CoreInjector CoreInjector { get; set; }

		public BotManager()
		{
			isRunning = true;
			Util.Init(out activeBots);
		}

		public void RunBots(bool interactive)
		{
			var templates = Config.ListAllBots().ToArray();

			if (templates.Length == 0)
			{
				if (!interactive)
				{
					Log.Warn("No bots are configured in the load list.");
					return;
				}

				Log.Info("It seems like there are no bots configured.");
				Log.Info("Fill out this quick setup to get started.");

				var newBot = CreateNewBot();
				string address;
				while (true)
				{
					Console.WriteLine("Please enter the ip, domain or nickname (with port; default: 9987) where to connect to:");
					address = Console.ReadLine();
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

				const string defaultBotName = "default";

				if (!newBot.SaveNew(defaultBotName))
				{
					Log.Error("Could not save new bot. Ensure that the bot has access to the directory.");
					return;
				}

				var botMetaConfig = Config.Bots.GetOrCreateItem(defaultBotName);
				botMetaConfig.Run.Value = true;

				if (!Config.Save())
					Log.Error("Could not save root config. The bot won't start by default.");

				var runResult = RunBot(newBot);
				return;
			}

			foreach (var template in Config.Bots.GetAllItems().Where(t => t.Run))
			{
				var result = RunBotTemplate(template.Key);
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
			var config = Config.GetBotTemplate(name);
			if (!config.Ok)
				return config.Error.Message;
			var botInfo = RunBot(config.Value);
			if (!botInfo.Ok)
				return botInfo.Error;
			return botInfo.Value;
		}

		public R<BotInfo, string> RunBot(ConfBot config)
		{
			bool removeBot = false;
			var bot = new Bot(config) { Injector = CoreInjector.CloneRealm<BotInjector>() };
			if (!CoreInjector.TryInject(bot))
				Log.Warn("Partial bot dependency loaded only");

			lock (bot.SyncRoot)
			{
				var initializeResult = bot.InitializeBot();
				if (initializeResult.Ok)
				{
					lock (lockObj)
					{
						activeBots.Add(bot);
						bot.Id = activeBots.Count - 1;
						removeBot = !isRunning;
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

		public BotLock GetBotLock(int id)
		{
			Bot bot;
			lock (lockObj)
			{
				if (!isRunning)
					return null;
				bot = id >= 0 && id < activeBots.Count
					? activeBots[id]
					: null;
				if (bot == null)
					return new BotLock(false, null);
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
				activeBots.Remove(bot);
			}
		}

		public BotInfo[] GetBotInfolist()
		{
			lock (lockObj)
			{
				return activeBots.Select(x => x.GetInfo()).ToArray();
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

	public class BotLock : IDisposable
	{
		private readonly Bot bot;
		public bool IsValid { get; private set; }
		public Bot Bot => IsValid ? bot : throw new InvalidOperationException("The bot lock is not valid.");

		internal BotLock(bool isValid, Bot bot)
		{
			IsValid = isValid;
			this.bot = bot;
		}

		public void Dispose()
		{
			if (IsValid)
			{
				IsValid = false;
				Monitor.Exit(bot.SyncRoot);
			}
		}
	}
}
