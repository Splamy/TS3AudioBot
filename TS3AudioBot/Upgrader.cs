// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TS3AudioBot.Dependency;

namespace TS3AudioBot
{
	internal static class Upgrader
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const string CoreTable = "core";
		private const int CurrentVersion = 1;

		public static void PerformUpgrades(CoreInjector injector)
		{
			var database = injector.GetModuleOrThrow<DbStore>();
			var meta = database.GetMetaData(CoreTable);

			void Advance(int version, string? explanation)
			{
				meta.Version = version;
				database.UpdateMetaData(meta);
				if (explanation != null)
					Log.Info("Upgrading data to ver {0}. {1}", version, explanation);
			}

			switch (meta.Version)
			{
			case 0:
				// Case 0 should always jump to the lastest version, since it gets created on first start.
				Advance(CurrentVersion, null);
				goto case CurrentVersion;

			case CurrentVersion:
				break;

			default:
				Log.Warn("It seems that you downgraded your TS3AB version. " +
					"Due to automatic upgrades some stuff might not work anymore, be advised. " +
					"It is recommended to backup data before upgrading to unstable/beta builds if you intend to downgrade again.");
				break;
			}
		}
	}
}
