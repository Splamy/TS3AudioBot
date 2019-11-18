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
		public ConfigValue<string> BotsPath { get; } = new ConfigValue<string>("bots_path", "bots",
			"Path to a folder where the configuration files for each bot template will be stored.");
	}

	public class ConfDb : ConfigTable
	{
		public ConfigValue<string> Path { get; } = new ConfigValue<string>("path", "ts3audiobot.db",
			"The path to the database file for persistent data.");
	}

	public class ConfFactories : ConfigTable
	{
		public ConfPath Media { get; } = Create<ConfPath>("media",
			"The default path to look for local resources.");
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
		public ConfigValue<string> Path { get; } = new ConfigValue<string>("path", "ffmpeg");
	}

	public class ConfRights : ConfigTable
	{
		public ConfigValue<string> Path { get; } = new ConfigValue<string>("path", "rights.toml",
			"Path to the permission file. The file will be generated if it doesn't exist.");
	}

	public class ConfPlugins : ConfigTable
	{
		public ConfigValue<string> Path { get; } = new ConfigValue<string>("path", "plugins",
			"The path to the plugins folder.");
		public ConfigValue<bool> WriteStatusFiles { get; } = new ConfigValue<bool>("write_status_files", false,
			"Write to .status files to store a plugin enable status persistently and restart them on launch."); // TODO deprecate

		public ConfPluginsLoad Load { get; } = Create<ConfPluginsLoad>("load");
	}

	public class ConfPluginsLoad : ConfigTable
	{
		// TODO: dynamic table
	}

	public class ConfWeb : ConfigTable
	{
		public ConfigArray<string> Hosts { get; } = new ConfigArray<string>("hosts", new[] { "*" },
				"An array of all urls the web api should be possible to be accessed with.");
		public ConfigValue<ushort> Port { get; } = new ConfigValue<ushort>("port", 58913,
			"The port for the web server.");

		public ConfWebApi Api { get; } = Create<ConfWebApi>("api");
		public ConfWebInterface Interface { get; } = Create<ConfWebInterface>("interface");
	}

	public class ConfWebApi : ConfigTable
	{
		public ConfigValue<bool> Enabled { get; } = new ConfigValue<bool>("enabled", true,
			"If you want to enable the web api.");
		public ConfigValue<int> CommandComplexity { get; } = new ConfigValue<int>("command_complexity", 64,
			"Limits the maximum command complexity to prevent endless loops.");
		public ConfigValue<string> Matcher { get; } = new ConfigValue<string>("matcher", "exact", "See: bot.commands.matcher");
	}

	public class ConfWebInterface : ConfigTable
	{
		public ConfigValue<bool> Enabled { get; } = new ConfigValue<bool>("enabled", true,
			"If you want to enable the webinterface.");
		public ConfigValue<string> Path { get; } = new ConfigValue<string>("path", "",
			"The webinterface folder to host. Leave empty to let the bot look for default locations.");
	}

	public partial class ConfBot : ConfigTable
	{
		public ConfigValue<ulong> BotGroupId { get; } = new ConfigValue<ulong>("bot_group_id", 0,
			"This field will be automatically set when you call '!bot setup'.\n" +
			"The bot will use the specified group to set/update the required permissions and add himself into it.\n" +
			"You can set this field manually if you already have a preexisting group the bot should add himself to.");
		public ConfigValue<bool> GenerateStatusAvatar { get; } = new ConfigValue<bool>("generate_status_avatar", true,
			"Tries to fetch a cover image when playing.");
		public ConfigValue<bool> SetStatusDescription { get; } = new ConfigValue<bool>("set_status_description", true,
			"Sets the description of the bot to the current song title.");
		public ConfigValue<string> Language { get; } = new ConfigValue<string>("language", "en",
			"The language the bot should use to respond to users. (Make sure you have added the required language packs)");
		public ConfigValue<bool> Run { get; } = new ConfigValue<bool>("run", false,
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
		public ConfigValue<string> Matcher { get; } = new ConfigValue<string>("matcher", "ic3",
			"Defines how the bot tries to match your !commands. Possible types:\n" +
			" - exact : Only when the command matches exactly.\n" +
			" - substring : The shortest command starting with the given prefix.\n" +
			" - ic3 : 'interleaved continuous character chain' A fuzzy algorithm similar to hamming distance but preferring characters at the start."
			/* "hamming : " */);
		public ConfigValue<LongTextBehaviour> LongMessage { get; } = new ConfigValue<LongTextBehaviour>("long_message", LongTextBehaviour.Split,
			"Defines how the bot handles messages which are too long for a single ts3 message. Options are:\n" +
			" - split : The message will be split up into multiple messages.\n" +
			" - drop : Does not send the message.");
		public ConfigValue<int> LongMessageSplitLimit { get; } = new ConfigValue<int>("long_message_split_limit", 1,
			"Limits the split count for long messages. When for example set to 1 the message will simply be trimmed to one message.");
		public ConfigValue<bool> Color { get; } = new ConfigValue<bool>("color", true,
			"Enables colors and text highlights for respones.");
		public ConfigValue<int> CommandComplexity { get; } = new ConfigValue<int>("command_complexity", 64,
			"Limits the maximum command complexity to prevent endless loops.");

		public ConfCommandsAlias Alias { get; } = Create<ConfCommandsAlias>("alias");
	}

	public class ConfCommandsAlias : ConfigDynamicTable<ConfigValue<string>>
	{
		public ConfCommandsAlias() : base(key => new ConfigValue<string>(key, "")) { }
	}

	public class ConfConnect : ConfigTable
	{
		public ConfigValue<string> Address { get; } = new ConfigValue<string>("address", "",
			"The address, ip or nickname (and port; default: 9987) of the TeamSpeak3 server");
		public ConfigValue<string> Channel { get; } = new ConfigValue<string>("channel", "",
			"Default channel when connecting. Use a channel path or \"/<id>\".\n" +
			"Examples: \"Home/Lobby\", \"/5\", \"Home/Afk \\\\/ Not Here\".");
		public ConfigValue<string> Badges { get; } = new ConfigValue<string>("badges", "",
			"The client badges. You can set a comma seperated string with max three GUID's. Here is a list: http://yat.qa/ressourcen/abzeichen-badges/");
		public ConfigValue<string> Name { get; } = new ConfigValue<string>("name",
			"TS3AudioBot", "Client nickname when connecting.");

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
		public ConfigArray<string> OnTimeout { get; } = new ConfigArray<string>("ontimeout", new[] { "1s", "2s", "5s", "10s", "30s", "1m", "5m", "repeat last" }) { Validator = ConfTimeExtensions.ValidateTime };
		public ConfigArray<string> OnKick { get; } = new ConfigArray<string>("onkick", Array.Empty<string>()) { Validator = ConfTimeExtensions.ValidateTime };
		public ConfigArray<string> OnBan { get; } = new ConfigArray<string>("onban", Array.Empty<string>()) { Validator = ConfTimeExtensions.ValidateTime };
		public ConfigArray<string> OnError { get; } = new ConfigArray<string>("onerror", new[] { "30s", "repeat last" }) { Validator = ConfTimeExtensions.ValidateTime };
		public ConfigArray<string> OnShutdown { get; } = new ConfigArray<string>("onshutdown", new[] { "5m" }) { Validator = ConfTimeExtensions.ValidateTime };
		//public ConfigValue<int> MaxReconnect { get; } = new ConfigValue<int>("max_combined_reconnects", -1, "Each reconnect kind has an own counter and resets when ");
	}

	public class ConfIdentity : ConfigTable
	{
		public ConfigValue<string> PrivateKey { get; } = new ConfigValue<string>("key", "",
			"||| DO NOT MAKE THIS KEY PUBLIC ||| The client identity. You can import a teamspeak3 identity here too.");
		public ConfigValue<ulong> Offset { get; } = new ConfigValue<ulong>("offset", 0,
			"The client identity offset determining the security level.");
		public ConfigValue<int> Level { get; } = new ConfigValue<int>("level", -1,
			"The client identity security level which should be calculated before connecting\n" +
			"or -1 to generate on demand when connecting.");
	}

	public class ConfAudio : ConfigTable
	{
		public ConfAudioVolume Volume { get; } = Create<ConfAudioVolume>("volume",
			"When a new song starts the volume will be trimmed to between min and max.\n" +
			"When the current volume already is between min and max nothing will happen.\n" +
			"To completely or partially disable this feature, set min to 0 and/or max to 100.");
		public ConfigValue<float> MaxUserVolume { get; } = new ConfigValue<float>("max_user_volume", 100,
			"The maximum volume a normal user can request. Only user with the 'ts3ab.admin.volume' permission can request higher volumes.");
		public ConfigValue<int> Bitrate { get; } = new ConfigValue<int>("bitrate", 48,
			"Specifies the bitrate (in kbps) for sending audio.\n" +
			"Values between 8 and 98 are supported, more or less can work but without guarantees.\n" +
			"Reference values: 16 - very poor (~3KiB/s), 24 - poor (~4KiB/s), 32 - okay (~5KiB/s), 48 - good (~7KiB/s), 64 - very good (~9KiB/s), 96 - deluxe (~13KiB/s)");
		public ConfigValue<string> SendMode { get; } = new ConfigValue<string>("send_mode", "voice",
			"How the bot should play music. Options are:\n" +
			" - whisper : Whispers to the channel where the request came from. Other users can join with '!subscribe'.\n" +
			" - voice : Sends via normal voice to the current channel. '!subscribe' will not work in this mode.\n" +
			" - !... : A custom command. Use '!xecute (!a) (!b)' for example to execute multiple commands.");
	}

	public class ConfAudioVolume : ConfigTable
	{
		protected override TomlTable.TableTypes TableType => TomlTable.TableTypes.Inline;

		public ConfigValue<float> Default { get; } = new ConfigValue<float>("default", 50);
		public ConfigValue<float> Min { get; } = new ConfigValue<float>("min", 25);
		public ConfigValue<float> Max { get; } = new ConfigValue<float>("max", 75);
	}

	public class ConfPlaylists : ConfigTable
	{
		//public ConfigValue<int> MaxItemCount { get; } = new ConfigValue<int>("max_item_count", 1000); // TODO
	}

	public class ConfHistory : ConfigTable
	{
		public ConfigValue<bool> Enabled { get; } = new ConfigValue<bool>("enabled", true,
			"Enable or disable history features completely to save resources.");
		public ConfigValue<bool> FillDeletedIds { get; } = new ConfigValue<bool>("fill_deleted_ids", true,
			"Whether or not deleted history ids should be filled up with new songs.");
	}

	public class ConfData : ConfigTable
	{
		//public ConfigValue<string> MaxItemCount { get; } = new ConfigValue<string>("disk_data", "1M"); // TODO
	}

	public class ConfEvents : ConfigTable
	{
		public ConfigValue<string> OnConnect { get; } = new ConfigValue<string>("onconnect", "",
			"Called when the bot is connected.");
		public ConfigValue<string> OnDisconnect { get; } = new ConfigValue<string>("ondisconnect", "",
			"Called when the bot gets disconnected.");
		public ConfigValue<string> OnIdle { get; } = new ConfigValue<string>("onidle", "",
			"Called when the bot does not play anything for a certain amount of time.");
		public ConfigValue<TimeSpan> IdleDelay { get; } = new ConfigValue<TimeSpan>("idletime", TimeSpan.Zero,
			"Specifies how long the bot has to be idle until the 'onidle' event gets fired.\n" +
			"You can specify the time in the ISO-8601 format with quotation marks \"PT30S\" or like: 15s, 1h, 3m30s");
		public ConfigValue<string> OnAlone { get; } = new ConfigValue<string>("onalone", "",
			"Called when the last client leaves the channel of the bot. Delay can be specified");
		public ConfigValue<TimeSpan> AloneDelay { get; } = new ConfigValue<TimeSpan>("alone_delay", TimeSpan.Zero,
			"Specifies how long the bot has to be alone until the 'onalone' event gets fired.\n" +
			"You can specify the time in the ISO-8601 format with quotation marks \"PT30S\" or like: 15s, 1h, 3m30s");
		public ConfigValue<string> OnParty { get; } = new ConfigValue<string>("onparty", "",
			"Called when the bot was alone and a client joins his channel. Delay can be specified.");
		public ConfigValue<TimeSpan> PartyDelay { get; } = new ConfigValue<TimeSpan>("party_delay", TimeSpan.Zero,
			"Specifies how long the bot has to be alone until the 'onalone' event gets fired.\n" +
			"You can specify the time in the ISO-8601 format with quotation marks \"PT30S\" or like: 15s, 1h, 3m30s");
	}

	// Utility config structs

	public class ConfPath : ConfigTable
	{
		protected override TomlTable.TableTypes TableType => TomlTable.TableTypes.Inline;

		public ConfigValue<string> Path { get; } = new ConfigValue<string>("path", string.Empty);
	}

	public class ConfPassword : ConfigTable
	{
		protected override TomlTable.TableTypes TableType => TomlTable.TableTypes.Inline;

		public ConfigValue<string> Password { get; } = new ConfigValue<string>("pw", string.Empty);
		public ConfigValue<bool> Hashed { get; } = new ConfigValue<bool>("hashed", false);
		public ConfigValue<bool> AutoHash { get; } = new ConfigValue<bool>("autohash", false);

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

		public ConfigValue<string> Build { get; } = new ConfigValue<string>("build", string.Empty);
		public ConfigValue<string> Platform { get; } = new ConfigValue<string>("platform", string.Empty);
		public ConfigValue<string> Sign { get; } = new ConfigValue<string>("sign", string.Empty);
	}

	public static class ConfTimeExtensions
	{
		public static TimeSpan? GetValueAsTime(this ConfigArray<string> conf, int index)
		{
			var value = conf.Value;
			if (value.Count == 0)
				return null;
			var last = value[value.Count - 1];
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
			var last = value[value.Count - 1];
			var repeat = last == "repeat" || last == "repeat last";
			if (repeat && value.Count == 1)
				return $"Specified 'repeat' without any previous value.";

			var max = repeat ? value.Count - 2 : value.Count - 1;
			for (int i = 0; i <= max; i++)
			{
				var r = TextUtil.ValidateTime(value[i]);
				if (!r.Ok)
					return r;
			}
			return R.Ok;
		}
	}
}
