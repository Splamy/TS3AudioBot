// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.IO;

namespace TS3AudioBot.Config.Deprecated
{
	internal static class UpgradeScript
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		// false on error, true if successful
		public static void CheckAndUpgrade(ConfRoot coreConfig, string oldFilename = "configTS3AudioBot.cfg")
		{
			if (!File.Exists(oldFilename))
				return;
			var oldConfig = ConfigFile.OpenOrCreate(oldFilename);
			if (oldConfig is null)
			{
				Log.Error("Old config file '{0}' found but could not be read", oldFilename);
				return;
			}

			try
			{
				Upgrade(oldConfig, coreConfig);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error while upgrading from old config");
				return;
			}

			try
			{
				File.Move(oldFilename, oldFilename + ".old");
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Couldn't move the old config file. Remove it manually to prevent this upgrade step and message.");
			}
		}

		// false on error, true if successful
		private static void Upgrade(ConfigFile from, ConfRoot to)
		{
			// Read old data
			var web = from.GetDataStruct<WebData>("WebData", true);
			var rmd = from.GetDataStruct<RightsManagerData>("RightsManager", true);
			var ytd = from.GetDataStruct<YoutubeFactoryData>("YoutubeFactory", true);
			var pmd = from.GetDataStruct<PluginManagerData>("PluginManager", true);
			var mfd = from.GetDataStruct<MediaFactoryData>("MediaFactory", true);
			var mbd = from.GetDataStruct<MainBotData>("MainBot", true);
			var hmd = from.GetDataStruct<HistoryManagerData>("HistoryManager", true);
			var pld = from.GetDataStruct<PlaylistManagerData>("PlaylistManager", true);
			var afd = from.GetDataStruct<AudioFrameworkData>("AudioFramework", true);
			var qcd = from.GetDataStruct<Ts3FullClientData>("QueryConnection", true);

			// Get all root stuff and save it

			to.Web.Port.Value = web.Port;
			to.Web.Api.Enabled.Value = web.EnableApi;
			to.Web.Interface.Enabled.Value = web.EnableWebinterface;
			to.Web.Interface.Path.Value = web.WebinterfaceHostPath;
			to.Rights.Path.Value = rmd.RightsFile;
			to.Tools.YoutubeDl.Path.Value = ytd.YoutubedlPath;
			to.Tools.Ffmpeg.Path.Value = qcd.FfmpegPath;
			to.Plugins.Path.Value = pmd.PluginPath;
			to.Plugins.WriteStatusFiles.Value = pmd.WriteStatusFiles;
			to.Factories.Media.Path.Value = mfd.DefaultPath;
			to.Db.Path.Value = hmd.HistoryFile;

			to.Save();

			// Create a default client for all bot instance relate stuff and save

			var bot = to.CreateBot();

			bot.Run.Value = true;
			bot.Language.Value = mbd.Language;
			bot.BotGroupId.Value = mbd.BotGroupId;
			bot.GenerateStatusAvatar.Value = mbd.GenerateStatusAvatar;
			bot.Commands.Matcher.Value = mbd.CommandMatching;
			bot.History.Enabled.Value = hmd.EnableHistory;
			bot.History.FillDeletedIds.Value = hmd.FillDeletedIds;
			bot.Audio.Volume.Default.Value = afd.DefaultVolume;
			bot.Audio.MaxUserVolume.Value = afd.MaxUserVolume;
			bot.Audio.SendMode.Value = afd.AudioMode;
			bot.Audio.Bitrate.Value = qcd.AudioBitrate;
			bot.Connect.Address.Value = qcd.Address;
			bot.Connect.Identity.PrivateKey.Value = qcd.Identity;
			bot.Connect.Identity.Level.Value = qcd.IdentityLevel == "auto" ? -1 : int.Parse(qcd.IdentityLevel);
			bot.Connect.Identity.Offset.Value = qcd.IdentityOffset;
			bot.Connect.ServerPassword.Password.Value = qcd.ServerPassword;
			bot.Connect.ServerPassword.AutoHash.Value = qcd.ServerPasswordAutoHash;
			bot.Connect.ServerPassword.Hashed.Value = qcd.ServerPasswordIsHashed;
			if (!string.IsNullOrEmpty(qcd.ClientVersion))
			{
				var clientVersion = qcd.ClientVersion.Split('|');
				bot.Connect.ClientVersion.Build.Value = clientVersion[0];
				bot.Connect.ClientVersion.Platform.Value = clientVersion[1];
				bot.Connect.ClientVersion.Sign.Value = clientVersion[2];
			}
			bot.Connect.Name.Value = qcd.DefaultNickname;
			bot.Connect.Channel.Value = qcd.DefaultChannel;
			bot.Connect.ChannelPassword.Password.Value = qcd.DefaultChannelPassword;
			bot.Connect.Badges.Value = qcd.ClientBadges == "overwolf=0:badges=" ? "" : qcd.ClientBadges;

			bot.SaveNew(ConfigHelper.DefaultBotName);
		}
	}
}
