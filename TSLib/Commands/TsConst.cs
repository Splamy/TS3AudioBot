// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TSLib.Commands
{
	public class TsConst
	{
		public static TsConst Default { get; } = new TsConst();

		public static TsConst Server_3_8_0 { get; } = new TsConst()
		{
			MaxSizeTextMessage = 8192
		};

		public static TsConst GetByServerBuildNum(ulong buildNum)
		{
			if (buildNum >= 1558938729UL) // 3.8.0 [Build: 1558938729]
				return Server_3_8_0;

			return Default;
		}

		// Common Definitions

		//limited length, measured in characters
		public int MaxSizeChannelName { get; init; } = 40;
		public int MaxSizeVirtualserverName { get; init; } = 64;
		public int MaxSizeClientNicknameSdk { get; init; } = 64;
		public int MinSizeClientNicknameSdk { get; init; } = 3;
		public int MaxSizeReasonMessage { get; init; } = 80;

		//limited length, measured in bytes (utf8 encoded)
		public int MaxSizeTextMessage { get; init; } = 1024;
		public int MaxSizeChannelTopic { get; init; } = 255;
		public int MaxSizeChannelDescription { get; init; } = 8192;
		public int MaxSizeVirtualserverWelcomeMessage { get; init; } = 1024;

		// Rare Definitions

		//limited length, measured in characters
		public int MaxSizeClientNickname { get; init; } = 30;
		public int MinSizeClientNickname { get; init; } = 3;
		public int MaxSizeAwayMessage { get; init; } = 80;
		public int MaxSizeGroupName { get; init; } = 30;
		public int MaxSizeTalkRequestMessage { get; init; } = 50;
		public int MaxSizeComplainMessage { get; init; } = 200;
		public int MaxSizeClientDescription { get; init; } = 200;
		public int MaxSizeHostMessage { get; init; } = 200;
		public int MaxSizeHostbuttonTooltip { get; init; } = 50;
		public int MaxSizepokeMessage { get; init; } = 100;
		public int MaxSizeOfflineMessage { get; init; } = 4096;
		public int MaxSizeOfflineMessageSubject { get; init; } = 200;

		//limited length, measured in bytes (utf8 encoded)
		public int MaxSizePluginCommand { get; init; } = 1024 * 8;
		public int MaxSizeVirtualserverHostbannerGfxUrl { get; init; } = 2000;

		public TsConst() { }
	}
}
