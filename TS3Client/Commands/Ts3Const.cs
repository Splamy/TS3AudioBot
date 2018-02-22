// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Commands
{
	public static class Ts3Const
	{
		// Common Definitions

		//limited length, measured in characters
		public const int MaxSizeChannelName = 40;
		public const int MaxSizeVirtualserverName = 64;
		public const int MaxSizeClientNicknameSdk = 64;
		public const int MinSizeClientNicknameSdk = 3;
		public const int MaxSizeReasonMessage = 80;

		//limited length, measured in bytes (utf8 encoded)
		public const int MaxSizeTextMessage = 1024;
		public const int MaxSizeChannelTopic = 255;
		public const int MaxSizeChannelDescription = 8192;
		public const int MaxSizeVirtualserverWelcomeMessage = 1024;

		// Rare Definitions

		//limited length, measured in characters
		public const int MaxSizeClientNickname = 30;
		public const int MinSizeClientNickname = 3;
		public const int MaxSizeAwayMessage = 80;
		public const int MaxSizeGroupName = 30;
		public const int MaxSizeTalkRequestMessage = 50;
		public const int MaxSizeComplainMessage = 200;
		public const int MaxSizeClientDescription = 200;
		public const int MaxSizeHostMessage = 200;
		public const int MaxSizeHostbuttonTooltip = 50;
		public const int MaxSizepokeMessage = 100;
		public const int MaxSizeOfflineMessage = 4096;
		public const int MaxSizeOfflineMessageSubject = 200;

		//limited length, measured in bytes (utf8 encoded)
		public const int MaxSizePluginCommand = 1024 * 8;
		public const int MaxSizeVirtualserverHostbannerGfxUrl = 2000;
	}
}
