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
		public int MaxSizeChannelName { get; private set; } = 40;
		public int MaxSizeVirtualserverName { get; private set; } = 64;
		public int MaxSizeClientNicknameSdk { get; private set; } = 64;
		public int MinSizeClientNicknameSdk { get; private set; } = 3;
		public int MaxSizeReasonMessage { get; private set; } = 80;

		//limited length, measured in bytes (utf8 encoded)
		public int MaxSizeTextMessage { get; private set; } = 1024;
		public int MaxSizeChannelTopic { get; private set; } = 255;
		public int MaxSizeChannelDescription { get; private set; } = 8192;
		public int MaxSizeVirtualserverWelcomeMessage { get; private set; } = 1024;

		// Rare Definitions

		//limited length, measured in characters
		public int MaxSizeClientNickname { get; private set; } = 30;
		public int MinSizeClientNickname { get; private set; } = 3;
		public int MaxSizeAwayMessage { get; private set; } = 80;
		public int MaxSizeGroupName { get; private set; } = 30;
		public int MaxSizeTalkRequestMessage { get; private set; } = 50;
		public int MaxSizeComplainMessage { get; private set; } = 200;
		public int MaxSizeClientDescription { get; private set; } = 200;
		public int MaxSizeHostMessage { get; private set; } = 200;
		public int MaxSizeHostbuttonTooltip { get; private set; } = 50;
		public int MaxSizepokeMessage { get; private set; } = 100;
		public int MaxSizeOfflineMessage { get; private set; } = 4096;
		public int MaxSizeOfflineMessageSubject { get; private set; } = 200;

		//limited length, measured in bytes (utf8 encoded)
		public int MaxSizePluginCommand { get; private set; } = 1024 * 8;
		public int MaxSizeVirtualserverHostbannerGfxUrl { get; private set; } = 2000;

		public TsConst() { }
	}
}
