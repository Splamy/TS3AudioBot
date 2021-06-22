// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Nett;
using System;
using System.Collections.Generic;
using TS3AudioBot.CommandSystem.Text;
using TS3AudioBot.Helper;
using TS3AudioBot.ResourceFactories.Youtube;

namespace TS3AudioBot.Config
{
	public partial class ConfRoot : ConfigTable
	{
		public ConfBot Bot { get; } = Create<ConfBot>("bot",
			"! IMPORTANT !\n" +
			"All config tables here starting with 'bot.*' will only be used as default values for each bot.\n" +
			"To make bot-instance specific changes go to the 'Bots' folder (configs.bots_path) and set your configuration values in the desired bot config.");
		public ConfConfigs Configs { get; } = Create<ConfConfigs>("configs");
		public ConfDb Db { get; } = Create<ConfDb>("db");
		public ConfFactories Factories { get; } = Create<ConfFactories>("factories");
		public ConfTools Tools { get; } = Create<ConfTools>("tools");
		public ConfRights Rights { get; } = Create<ConfRights>("rights");
		public ConfPlugins Plugins { get; } = Create<ConfPlugins>("plugins");
		public ConfWeb Web { get; } = Create<ConfWeb>("web");

		//public ConfigValue<bool> ActiveDocumentation { get; } = new ConfigValue<bool>("_active_doc", true);
	}

	public class ConfConfigs : ConfigTable
	{
		//public ConfigValue<string> RootPath { get; } = new ConfigValue<string>("root_path", "."); // TODO enable when done
		public ConfigValue<string> BotsPath { get; } = new("bots_path", "bots",
			"Path to a folder where the configuration files for each bot template will be stored.");
		public ConfigValue<bool> SendStats { get; } = new("send_stats", true,
			"Enable to contribute to the global stats tracker to help us improve our service.\n" +
			"We do NOT send/store any IPs, identifiable information or logs for this.\n" +
			"If you want to check how a stats packet looks like you can run the bot with 'TS3AudioBot --stats-example'.\n" +
			"To disable contributing without config you can run the bot with 'TS3AudioBot --stats-disabled'. This will ignore the config value.");
	}

	public class ConfDb : ConfigTable
	{
		public ConfigValue<string> Path { get; } = new("path", "ts3audiobot.db",
			"The path to the database file for persistent data.");
	}

	public class ConfFactories : ConfigTable
	{
		public ConfPath Media { get; } = Create<ConfPath>("media",
			"The default path to look for local resources.");
		public ConfResolverYoutube Youtube { get; } = Create<ConfResolverYoutube>("youtube");
	}

	public class ConfResolverYoutube : ConfigTable
	{
		public ConfigValue<LoaderPriority> ResolverPriority { get; } = new("prefer_resolver", LoaderPriority.Internal,
			"Changes how to try to resolve youtube songs\n" +
			" - youtubedl : uses youtube-dl only\n" +
			" - internal : uses the internal resolver, then youtube-dl");
		public ConfigValue<string> ApiKey { get; } = new("youtube_api_key", "",
			"Set your own youtube api key to keep using the old youtube factory loader.\n" +
			"This feature is unsupported and may break at any time");
	}

	public class ConfTools : ConfigTable
	{
		// youtube-dl can be empty by default as we make some thorough lookups.
		public ConfPath YoutubeDl { get; } = Create<ConfPath>("youtube-dl",
			"Path to the youtube-dl binary or local git repository.");
		public ConfToolsFfmpeg Ffmpeg { get; } = Create<ConfToolsFfmpeg>("ffmpeg",
			"The path to ffmpeg.");
		//public ConfPath Ffprobe { get; } = Create<ConfPath>("ffprobe");
	}

	public class ConfToolsFfmpeg : ConfigTable
	{
		public ConfigValue<string> Path { get; } = new("path", "ffmpeg");
	}

	public class ConfRights : ConfigTable
	{
		public ConfigValue<string> Path { get; } = new("path", "rights.toml",
			"Path to the permission file. The file will be generated if it doesn't exist.");
	}

	public class ConfPlugins : ConfigTable
	{
		public ConfigValue<string> Path { get; } = new("path", "plugins",
			"The path to the plugins folder.");

		public ConfPluginsLoad Load { get; } = Create<ConfPluginsLoad>("load");
	}

	public class ConfPluginsLoad : ConfigTable
	{
		// TODO: dynamic table
	}

	public class ConfWeb : ConfigTable
	{
		public ConfigArray<string> Hosts { get; } = new("hosts", new[] { "*" },
			"An array of all urls the web api should be possible to be accessed with.");
		public ConfigValue<ushort> Port { get; } = new("port", 58913,
			"The port for the web server.");

		public ConfWebApi Api { get; } = Create<ConfWebApi>("api");
		public ConfWebInterface Interface { get; } = Create<ConfWebInterface>("interface");
	}

	public class ConfWebApi : ConfigTable
	{
		public ConfigValue<bool> Enabled { get; } = new("enabled", true,
			"If you want to enable the web api.");
		public ConfigValue<int> CommandComplexity { get; } = new("command_complexity", 64,
			"Limits the maximum command complexity to prevent endless loops.");
		public ConfigValue<string> Matcher { get; } = new("matcher", "exact", "See: bot.commands.matcher");
	}

	public class ConfWebInterface : ConfigTable
	{
		public ConfigValue<bool> Enabled { get; } = new("enabled", true,
			"If you want to enable the webinterface.");
		public ConfigValue<string> Path { get; } = new("path", "",
			"The webinterface folder to host. Leave empty to let the bot look for default locations.");
	}

	public partial class ConfBot : ConfigTable
	{
		public ConfigValue<ulong> BotGroupId { get; } = new("bot_group_id", 0,
			"This field will be automatically set when you call '!bot setup'.\n" +
			"The bot will use the specified group to set/update the required permissions and add himself into it.\n" +
			"You can set this field manually if you already have a preexisting group the bot should add himself to.");
		public ConfigValue<bool> GenerateStatusAvatar { get; } = new("generate_status_avatar", true,
			"Tries to fetch a cover image when playing.");
		public ConfigValue<bool> SetStatusDescription { get; } = new("set_status_description", true,
			"Sets the description of the bot to the current song title.");
		public ConfigValue<string> Language { get; } = new("language", "en",
			"The language the bot should use to respond to users. (Make sure you have added the required language packs)");
		public ConfigValue<bool> Run { get; } = new("run", false,
			"Starts the instance when the TS3AudioBot is launched.");

		public ConfCommands Commands { get; } = Create<ConfCommands>("commands");
		public ConfConnect Connect { get; } = Create<ConfConnect>("connect");
		public ConfReconnect Reconnect { get; } = Create<ConfReconnect>("reconnect");
		public ConfAudio Audio { get; } = Create<ConfAudio>("audio");
		public ConfPlaylists Playlists { get; } = Create<ConfPlaylists>("playlists");
		public ConfHistory History { get; } = Create<ConfHistory>("history");
		public ConfEvents Events { get; } = Create<ConfEvents>("events");
	}

	public class ConfCommands : ConfigTable
	{
		public ConfigValue<string> Matcher { get; } = new("matcher", "ic3",
			"Defines how the bot tries to match your !commands. Possible types:\n" +
			" - exact : Only when the command matches exactly.\n" +
			" - substring : The shortest command starting with the given prefix.\n" +
			" - ic3 : 'interleaved continuous character chain' A fuzzy algorithm similar to hamming distance but preferring characters at the start."
			/* "hamming : " */);
		public ConfigValue<LongTextBehaviour> LongMessage { get; } = new("long_message", LongTextBehaviour.Split,
			"Defines how the bot handles messages which are too long for a single ts3 message. Options are:\n" +
			" - split : The message will be split up into multiple messages.\n" +
			" - drop : Does not send the message.");
		public ConfigValue<int> LongMessageSplitLimit { get; } = new("long_message_split_limit", 1,
			"Limits the split count for long messages. When for example set to 1 the message will simply be trimmed to one message.");
		public ConfigValue<bool> Color { get; } = new("color", true,
			"Enables colors and text highlights for respones.");
		public ConfigValue<int> CommandComplexity { get; } = new("command_complexity", 64,
			"Limits the maximum command complexity to prevent endless loops.");

		public ConfCommandsAlias Alias { get; } = Create<ConfCommandsAlias>("alias");
	}

	public class ConfCommandsAlias : ConfigDynamicTable<ConfigValue<string>>
	{
		public ConfCommandsAlias() : base(key => new ConfigValue<string>(key, "")) { }
	}

	public class ConfConnect : ConfigTable
	{
		public ConfigValue<string> Address { get; } = new("address", "", "The address, ip or nickname (and port; default: 9987) of the TeamSpeak3 server");
		public ConfigValue<string> Channel { get; } = new("channel", "", "Default channel when connecting. Use a channel path or \"/<id>\".\n" +
			"Examples: \"Home/Lobby\", \"/5\", \"Home/Afk \\\\/ Not Here\".");
		public ConfigValue<string> Badges { get; } = new("badges", "", "The client badges. You can set a comma seperated string with max three GUID's. Here is a list: http://yat.qa/ressourcen/abzeichen-badges/");
		public ConfigValue<string> Name { get; } = new("name", "TS3AudioBot", "Client nickname when connecting.");

		public ConfPassword ServerPassword { get; } = Create<ConfPassword>("server_password",
			"The server password. Leave empty for none.");
		public ConfPassword ChannelPassword { get; } = Create<ConfPassword>("channel_password",
			"The default channel password. Leave empty for none.");
		public ConfTsVersion ClientVersion { get; } = Create<ConfTsVersion>("client_version",
			"Overrides the displayed version for the ts3 client. Leave empty for default.");
		public ConfIdentity Identity { get; } = Create<ConfIdentity>("identity");
	}

	public class ConfReconnect : ConfigTable
	{
		public ConfigArray<string> OnTimeout { get; } = new("ontimeout", new[] { "1s", "2s", "5s", "10s", "30s", "1m", "5m", "repeat last" });
		public ConfigArray<string> OnKick { get; } = new("onkick", Array.Empty<string>());
		public ConfigArray<string> OnBan { get; } = new("onban", Array.Empty<string>());
		public ConfigArray<string> OnError { get; } = new("onerror", new[] { "30s", "repeat last" });
		public ConfigArray<string> OnShutdown { get; } = new("onshutdown", new[] { "5m" });
		//public ConfigValue<int> MaxReconnect { get; } = new ConfigValue<int>("max_combined_reconnects", -1, "Each reconnect kind has an own counter and resets when ");
	}

	public class ConfIdentity : ConfigTable
	{
		public ConfigValue<string> PrivateKey { get; } = new("key", "", "||| DO NOT MAKE THIS KEY PUBLIC ||| The client identity. You can import a teamspeak3 identity here too.");
		public ConfigValue<ulong> Offset { get; } = new("offset", 0, "The client identity offset determining the security level.");
		public ConfigValue<int> Level { get; } = new("level", -1, "The client identity security level which should be calculated before connecting\n" +
			"or -1 to generate on demand when connecting.");
	}

	public class ConfAudio : ConfigTable
	{
		public ConfAudioVolume Volume { get; } = Create<ConfAudioVolume>("volume",
			"When a new song starts the volume will be trimmed to between min and max.\n" +
			"When the current volume already is between min and max nothing will happen.\n" +
			"To completely or partially disable this feature, set min to 0 and/or max to 100.");
		public ConfigValue<float> MaxUserVolume { get; } = new("max_user_volume", 100, "The maximum volume a normal user can request. Only user with the 'ts3ab.admin.volume' permission can request higher volumes.");
		public ConfigValue<int> Bitrate { get; } = new("bitrate", 48, "Specifies the bitrate (in kbps) for sending audio.\n" +
			"Values between 8 and 98 are supported, more or less can work but without guarantees.\n" +
			"Reference values: 16 - very poor (~3KiB/s), 24 - poor (~4KiB/s), 32 - okay (~5KiB/s), 48 - good (~7KiB/s), 64 - very good (~9KiB/s), 96 - deluxe (~13KiB/s)");
		public ConfigValue<string> SendMode { get; } = new("send_mode", "voice", "How the bot should play music. Options are:\n" +
			" - whisper : Whispers to the channel where the request came from. Other users can join with '!subscribe'.\n" +
			" - voice : Sends via normal voice to the current channel. '!subscribe' will not work in this mode.\n" +
			" - !... : A custom command. Use '!xecute (!a) (!b)' for example to execute multiple commands.");
	}

	public class ConfAudioVolume : ConfigTable
	{
		protected override TomlTable.TableTypes TableType => TomlTable.TableTypes.Inline;

		public ConfigValue<float> Default { get; } = new("default", 50);
		public ConfigValue<float> Min { get; } = new("min", 25);
		public ConfigValue<float> Max { get; } = new("max", 75);
	}

	public class ConfPlaylists : ConfigTable
	{
		//public ConfigValue<int> MaxItemCount { get; } = new ConfigValue<int>("max_item_count", 1000); // TODO
	}

	public class ConfHistory : ConfigTable
	{
		public ConfigValue<bool> Enabled { get; } = new("enabled", true, "Enable or disable history features completely to save resources.");
	}

	public class ConfData : ConfigTable
	{
		//public ConfigValue<string> MaxItemCount { get; } = new ConfigValue<string>("disk_data", "1M"); // TODO
	}

	public class ConfEvents : ConfigTable
	{
		public ConfigValue<string> OnConnect { get; } = new("onconnect", "", "Called when the bot is connected.");
		public ConfigValue<string> OnDisconnect { get; } = new("ondisconnect", "", "Called when the bot gets disconnected.");
		public ConfigValue<string> OnIdle { get; } = new("onidle", "", "Called when the bot does not play anything for a certain amount of time.");
		public ConfigValue<TimeSpan> IdleDelay { get; } = new("idletime", TimeSpan.Zero, "Specifies how long the bot has to be idle until the 'onidle' event gets fired.\n" +
			"You can specify the time in the ISO-8601 format \"PT30S\" or like: 15s, 1h, 3m30s");
		public ConfigValue<string> OnAlone { get; } = new("onalone", "", "Called when the last client leaves the channel of the bot. Delay can be specified");
		public ConfigValue<TimeSpan> AloneDelay { get; } = new("alone_delay", TimeSpan.Zero, "Specifies how long the bot has to be alone until the 'onalone' event gets fired.\n" +
			"You can specify the time in the ISO-8601 format \"PT30S\" or like: 15s, 1h, 3m30s");
		public ConfigValue<string> OnParty { get; } = new("onparty", "", "Called when the bot was alone and a client joins his channel. Delay can be specified.");
		public ConfigValue<TimeSpan> PartyDelay { get; } = new("party_delay", TimeSpan.Zero, "Specifies how long the bot has to be alone until the 'onalone' event gets fired.\n" +
			"You can specify the time in the ISO-8601 format \"PT30S\" or like: 15s, 1h, 3m30s");
		public ConfigValue<string> OnSongStart { get; } = new("onsongstart", "", "Called when a new song starts.");
	}

	// Utility config structs

	public class ConfPath : ConfigTable
	{
		protected override TomlTable.TableTypes TableType => TomlTable.TableTypes.Inline;

		public ConfigValue<string> Path { get; } = new("path", string.Empty);
	}

	public class ConfPassword : ConfigTable
	{
		protected override TomlTable.TableTypes TableType => TomlTable.TableTypes.Inline;

		public ConfigValue<string> Password { get; } = new("pw", string.Empty);
		public ConfigValue<bool> Hashed { get; } = new("hashed", false);
		public ConfigValue<bool> AutoHash { get; } = new("autohash", false);

		public TSLib.Password Get()
		{
			if (string.IsNullOrEmpty(Password))
				return TSLib.Password.Empty;
			var pass = Hashed
				? TSLib.Password.FromHash(Password)
				: TSLib.Password.FromPlain(Password);
			if (AutoHash && !Hashed)
			{
				Password.Value = pass.HashedPassword;
				Hashed.Value = true;
			}
			return pass;
		}
	}

	public class ConfTsVersion : ConfigTable
	{
		protected override TomlTable.TableTypes TableType => TomlTable.TableTypes.Inline;

		public ConfigValue<string> Build { get; } = new("build", string.Empty);
		public ConfigValue<string> Platform { get; } = new("platform", string.Empty);
		public ConfigValue<string> Sign { get; } = new("sign", string.Empty);
	}

	public static class ConfTimeExtensions
	{
		public static TimeSpan? GetValueAsTime(this ConfigArray<string> conf, int index)
		{
			var value = conf.Value;
			if (value.Count == 0)
				return null;
			var last = value[^1];
			var repeat = last == "repeat" || last == "repeat last"; // "repeat" might get removed for other loops, but for now keep as hidden alternative
			var max = repeat ? value.Count - 2 : value.Count - 1;
			if (index <= max)
				return TextUtil.ParseTime(value[index]);
			else
				return TextUtil.ParseTime(value[max]);
		}

		public static E<string> ValidateTime(IReadOnlyList<string> value)
		{
			if (value.Count == 0)
				return R.Ok;
			var last = value[^1];
			var repeat = last == "repeat" || last == "repeat last";
			if (repeat && value.Count == 1)
				return $"Specified 'repeat' without any previous value.";

			var max = repeat ? value.Count - 2 : value.Count - 1;
			for (int i = 0; i <= max; i++)
			{
				if (!TextUtil.ValidateTime(value[i]).GetOk(out var error))
					return error;
			}
			return R.Ok;
		}
	}
}
