// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Config.Deprecated
{
	public class WebData : ConfigData
	{
		[Info("A space seperated list of all urls the web api should be possible to be accessed with", "")]
		public string HostAddress { get => Get<string>(); set => Set(value); }

		[Info("The port for the api server", "8180")]
		public ushort Port { get => Get<ushort>(); set => Set(value); }

		[Info("If you want to start the web api server.", "false")]
		public bool EnableApi { get => Get<bool>(); set => Set(value); }

		[Info("If you want to start the webinterface server", "false")]
		public bool EnableWebinterface { get => Get<bool>(); set => Set(value); }

		[Info("The folder to host. Leave empty to let the bot look for default locations.", "")]
		public string WebinterfaceHostPath { get => Get<string>(); set => Set(value); }
	}

	public class RightsManagerData : ConfigData
	{
		[Info("Path to the config file", "rights.toml")]
		public string RightsFile { get => Get<string>(); set => Set(value); }
	}

	public class YoutubeFactoryData : ConfigData
	{
		[Info("Path to the youtube-dl binary or local git repository", "")]
		public string YoutubedlPath { get => Get<string>(); set => Set(value); }
	}

	public class PluginManagerData : ConfigData
	{
		[Info("The absolute or relative path to the plugins folder", "Plugins")]
		public string PluginPath { get => Get<string>(); set => Set(value); }

		[Info("Write to .status files to store a plugin enable status persistently and restart them on launch.", "false")]
		public bool WriteStatusFiles { get => Get<bool>(); set => Set(value); }
	}

	public class MediaFactoryData : ConfigData
	{
		[Info("The default path to look for local resources.", "")]
		public string DefaultPath { get => Get<string>(); set => Set(value); }
	}

	internal class MainBotData : ConfigData
	{
		[Info("The language the bot should use to respond to users. (Make sure you have added the required language packs)", "en")]
		public string Language { get => Get<string>(); set => Set(value); }
		[Info("Teamspeak group id giving the Bot enough power to do his job", "0")]
		public ulong BotGroupId { get => Get<ulong>(); set => Set(value); }
		[Info("Generate fancy status images as avatar", "true")]
		public bool GenerateStatusAvatar { get => Get<bool>(); set => Set(value); }
		[Info("Defines how the bot tries to match your !commands.\n" +
			"# Possible types: exact, substring, ic3, hamming", "ic3")]
		public string CommandMatching { get => Get<string>(); set => Set(value); }
	}

	public class HistoryManagerData : ConfigData
	{
		[Info("Allows to enable or disable history features completely to save resources.", "true")]
		public bool EnableHistory { get => Get<bool>(); set => Set(value); }
		[Info("The Path to the history database file", "history.db")]
		public string HistoryFile { get => Get<string>(); set => Set(value); }
		[Info("Whether or not deleted history ids should be filled up with new songs", "true")]
		public bool FillDeletedIds { get => Get<bool>(); set => Set(value); }
	}

	public class PlaylistManagerData : ConfigData
	{
		[Info("Path the playlist folder", "Playlists")]
		public string PlaylistPath { get => Get<string>(); set => Set(value); }
	}

	public class AudioFrameworkData : ConfigData
	{
		[Info("The default volume a song should start with", "10")]
		public float DefaultVolume { get => Get<float>(); set => Set(value); }
		[Info("The maximum volume a normal user can request", "30")]
		public float MaxUserVolume { get => Get<float>(); set => Set(value); }
		[Info("How the bot should play music. Options are: whisper, voice, (!...)", "whisper")]
		public string AudioMode { get => Get<string>(); set => Set(value); }
	}

	public class Ts3FullClientData : ConfigData
	{
		[Info("The address (and port, default: 9987) of the TeamSpeak3 server")]
		public string Address { get => Get<string>(); set => Set(value); }
		[Info("| DO NOT MAKE THIS KEY PUBLIC | The client identity", "")]
		public string Identity { get => Get<string>(); set => Set(value); }
		[Info("The client identity security offset", "0")]
		public ulong IdentityOffset { get => Get<ulong>(); set => Set(value); }
		[Info("The client identity security level which should be calculated before connecting, or \"auto\" to generate on demand.", "auto")]
		public string IdentityLevel { get => Get<string>(); set => Set(value); }
		[Info("The server password. Leave empty for none.")]
		public string ServerPassword { get => Get<string>(); set => Set(value); }
		[Info("Set this to true, if the server password is hashed.", "false")]
		public bool ServerPasswordIsHashed { get => Get<bool>(); set => Set(value); }
		[Info("Enable this to automatically hash and store unhashed passwords.\n" +
			"# (Be careful since this will overwrite the 'ServerPassword' field with the hashed value once computed)", "false")]
		public bool ServerPasswordAutoHash { get => Get<bool>(); set => Set(value); }
		[Info("The path to ffmpeg", "ffmpeg")]
		public string FfmpegPath { get => Get<string>(); set => Set(value); }
		[Info("Specifies the bitrate (in kbps) for sending audio.\n" +
			"# Values between 8 and 98 are supported, more or less can work but without guarantees.\n" +
			"# Reference values: 32 - ok (~5KiB/s), 48 - good (~7KiB/s), 64 - very good (~9KiB/s)", "48")]
		public int AudioBitrate { get => Get<int>(); set => Set(value); }
		[Info("Version for the client in the form of <version build>|<platform>|<version sign>\n" +
			"# Leave empty for default.", "")]
		public string ClientVersion { get => Get<string>(); set => Set(value); }
		[Info("Default Nickname when connecting", "AudioBot")]
		public string DefaultNickname { get => Get<string>(); set => Set(value); }
		[Info("Default Channel when connectiong\n" +
			"# Use a channel path or '/<id>', examples: 'Home/Lobby', '/5', 'Home/Afk \\/ Not Here'", "")]
		public string DefaultChannel { get => Get<string>(); set => Set(value); }
		[Info("The password for the default channel. Leave empty for none. Not required with permission b_channel_join_ignore_password", "")]
		public string DefaultChannelPassword { get => Get<string>(); set => Set(value); }
		[Info("The client badges. You can set a comma seperated string with max three GUID's. Here is a list: http://yat.qa/ressourcen/abzeichen-badges/", "overwolf=0:badges=")]
		public string ClientBadges { get => Get<string>(); set => Set(value); }
	}
}
