// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using System;

	/*
		* Most important Id datatypes:
		*
		* ClientUid: string
		* ClientDbId: ulong
		* ClientId: ushort
		* ChannelId: ulong
		* ServerGroupId: ulong
		* ChannelGroupId: ulong
		* PermissionIdT: int ???
	*/

	public enum ClientType
	{
		Full = 0,
		Query,
	}

	[Flags]
	public enum ClientListOptions
	{
		// ReSharper disable InconsistentNaming, UnusedMember.Global
		uid = 1 << 0,
		away = 1 << 1,
		voice = 1 << 2,
		times = 1 << 3,
		groups = 1 << 4,
		info = 1 << 5,
		icon = 1 << 6,
		country = 1 << 7,
		// ReSharper restore InconsistentNaming, UnusedMember.Global
	}

	public enum GroupNamingMode
	{
		/// <summary>No group name is displayed.</summary>
		None = 0,
		/// <summary>The group is displayed before the client name.</summary>
		Before,
		/// <summary>The group is displayed after the client name.</summary>
		After
	}

	public enum NotificationType
	{
		Unknown,
		Error,
		// Official notifies, used by client and query
		ChannelCreated,
		ChannelDeleted,
		ChannelChanged,
		ChannelEdited,
		ChannelMoved,
		ChannelPasswordChanged,
		ClientEnterView,
		ClientLeftView,
		ClientMoved,
		ServerEdited,
		TextMessage,
		TokenUsed,

		// Internal notifies, used by client
		InitIvExpand,
		InitServer,
		ChannelList,
		ChannelListFinished,
		ClientNeededPermissions,
		ClientChannelGroupChanged,
		ClientServerGroupAdded,
		ConnectionInfoRequest,
		ChannelSubscribed,
		ChannelUnsubscribed,
		ClientChatComposing,
		ServerGroupList,
		ServerGroupsByClientId,
		StartUpload,
		StartDownload,
		FileTransfer,
		FileTransferStatus,
		FileList,
		FileListFinished,
		FileInfo,
		// TODO: notifyclientchatclosed
		// TODO: notifyclientpoke
		// TODO: notifyclientupdated
		// TODO: notifyclientchannelgroupchanged
		// TODO: notifychannelpasswordchanged
		// TODO: notifychanneldescriptionchanged
	}

	// ReSharper disable UnusedMember.Global
	public enum MoveReason
	{
		UserAction = 0,
		UserOrChannelMoved,
		SubscriptionChanged,
		Timeout,
		KickedFromChannel,
		KickedFromServer,
		Banned,
		ServerStopped,
		LeftServer,
		ChannelUpdated,
		ServerOrChannelEdited,
		ServerShutdown,
	}

	public enum GroupWhisperType : byte
	{
		/// <summary>Requires servergroup targetId</summary>
		ServerGroup = 0,
		/// <summary>Requires channelgroup targetId</summary>
		ChannelGroup,
		ChannelCommander,
		AllClients,
	}

	public enum GroupWhisperTarget : byte
	{
		AllChannels = 0,
		CurrentChannel,
		ParentChannel,
		AllParentChannel,
		ChannelFamily,
		CompleteChannelFamily,
		Subchannels,
	}
	// ReSharper enable UnusedMember.Global

	// Source: http://forum.teamspeak.com/threads/102276-Server-query-error-id-list
	public enum Ts3ErrorCode : ushort
	{
		// ReSharper disable InconsistentNaming, UnusedMember.Global
		/// <summary>(normal) unknown error code</summary>
		ok = 0x0000,
		/// <summary>(normal) undefined error</summary>
		undefined = 0x0001,
		/// <summary>(normal) not implemented</summary>
		not_implemented = 0x0002,
		/// <summary>(normal) </summary>
		ok_no_update = 0x0003,
		/// <summary>(normal) </summary>
		dont_notify = 0x0004,
		/// <summary>(normal) library time limit reached</summary>
		lib_time_limit_reached = 0x0005,
		/// <summary>(normal) command not found</summary>
		command_not_found = 0x0100,
		/// <summary>(normal) unable to bind network port</summary>
		unable_to_bind_network_port = 0x0101,
		/// <summary>(normal) no network port available</summary>
		no_network_port_available = 0x0102,
		/// <summary>(normal) invalid clientID</summary>
		client_invalid_id = 0x0200,
		/// <summary>(normal) nickname is already in use</summary>
		client_nickname_inuse = 0x0201,
		/// <summary>(normal) invalid error code</summary>
		client_invalid_error_code = 0x0202,
		/// <summary>(normal) max clients protocol limit reached</summary>
		client_protocol_limit_reached = 0x0203,
		/// <summary>(normal) invalid client type</summary>
		client_invalid_type = 0x0204,
		/// <summary>(normal) already subscribed</summary>
		client_already_subscribed = 0x0205,
		/// <summary>(normal) not logged in</summary>
		client_not_logged_in = 0x0206,
		/// <summary>(normal) could not validate client identity</summary>
		client_could_not_validate_identity = 0x0207,
		/// <summary>(rare) invalid loginname or password</summary>
		client_invalid_password = 0x0208,
		/// <summary>(rare) too many clones already connected</summary>
		client_too_many_clones_connected = 0x0209,
		/// <summary>(normal) client version outdated, please update</summary>
		client_version_outdated = 0x020a,
		/// <summary>(rare) client is online</summary>
		client_is_online = 0x020b,
		/// <summary>(normal) client is flooding</summary>
		client_is_flooding = 0x020c,
		/// <summary>(normal) client is modified</summary>
		client_hacked = 0x020d,
		/// <summary>(normal) can not verify client at this moment</summary>
		client_cannot_verify_now = 0x020e,
		/// <summary>(normal) client is not permitted to log in</summary>
		client_login_not_permitted = 0x020f,
		/// <summary>(normal) client is not subscribed to the channel</summary>
		client_not_subscribed = 0x0210,
		/// <summary>(normal) invalid channelID</summary>
		channel_invalid_id = 0x0300,
		/// <summary>(normal) max channels protocol limit reached</summary>
		channel_protocol_limit_reached = 0x0301,
		/// <summary>(normal) already member of channel</summary>
		channel_already_in = 0x0302,
		/// <summary>(normal) channel name is already in use</summary>
		channel_name_inuse = 0x0303,
		/// <summary>(normal) channel not empty</summary>
		channel_not_empty = 0x0304,
		/// <summary>(normal) can not delete default channel</summary>
		channel_can_not_delete_default = 0x0305,
		/// <summary>(normal) default channel requires permanent</summary>
		channel_default_require_permanent = 0x0306,
		/// <summary>(normal) invalid channel flags</summary>
		channel_invalid_flags = 0x0307,
		/// <summary>(normal) permanent channel can not be child of non permanent channel</summary>
		channel_parent_not_permanent = 0x0308,
		/// <summary>(normal) channel maxclient reached</summary>
		channel_maxclients_reached = 0x0309,
		/// <summary>(normal) channel maxfamily reached</summary>
		channel_maxfamily_reached = 0x030a,
		/// <summary>(normal) invalid channel order</summary>
		channel_invalid_order = 0x030b,
		/// <summary>(normal) channel does not support filetransfers</summary>
		channel_no_filetransfer_supported = 0x030c,
		/// <summary>(normal) invalid channel password</summary>
		channel_invalid_password = 0x030d,
		/// <summary>(rare) channel is private channel</summary>
		channel_is_private_channel = 0x030e,
		/// <summary>(normal) invalid security hash supplied by client</summary>
		channel_invalid_security_hash = 0x030f,
		/// <summary>(normal) invalid serverID</summary>
		server_invalid_id = 0x0400,
		/// <summary>(normal) server is running</summary>
		server_running = 0x0401,
		/// <summary>(normal) server is shutting down</summary>
		server_is_shutting_down = 0x0402,
		/// <summary>(normal) server maxclient reached</summary>
		server_maxclients_reached = 0x0403,
		/// <summary>(normal) invalid server password</summary>
		server_invalid_password = 0x0404,
		/// <summary>(rare) deployment active</summary>
		server_deployment_active = 0x0405,
		/// <summary>(rare) unable to stop own server in your connection class</summary>
		server_unable_to_stop_own_server = 0x0406,
		/// <summary>(normal) server is virtual</summary>
		server_is_virtual = 0x0407,
		/// <summary>(rare) server wrong machineID</summary>
		server_wrong_machineid = 0x0408,
		/// <summary>(normal) server is not running</summary>
		server_is_not_running = 0x0409,
		/// <summary>(normal) server is booting up</summary>
		server_is_booting = 0x040a,
		/// <summary>(normal) server got an invalid status for this operation</summary>
		server_status_invalid = 0x040b,
		/// <summary>(rare) server modal quit</summary>
		server_modal_quit = 0x040c,
		/// <summary>(normal) server version is too old for command</summary>
		server_version_outdated = 0x040d,
		/// <summary>(rare) database error</summary>
		database = 0x0500,
		/// <summary>(rare) database empty result set</summary>
		database_empty_result = 0x0501,
		/// <summary>(rare) database duplicate entry</summary>
		database_duplicate_entry = 0x0502,
		/// <summary>(rare) database no modifications</summary>
		database_no_modifications = 0x0503,
		/// <summary>(rare) database invalid constraint</summary>
		database_constraint = 0x0504,
		/// <summary>(rare) database reinvoke command</summary>
		database_reinvoke = 0x0505,
		/// <summary>(normal) invalid quote</summary>
		parameter_quote = 0x0600,
		/// <summary>(normal) invalid parameter count</summary>
		parameter_invalid_count = 0x0601,
		/// <summary>(normal) invalid parameter</summary>
		parameter_invalid = 0x0602,
		/// <summary>(normal) parameter not found</summary>
		parameter_not_found = 0x0603,
		/// <summary>(normal) convert error</summary>
		parameter_convert = 0x0604,
		/// <summary>(normal) invalid parameter size</summary>
		parameter_invalid_size = 0x0605,
		/// <summary>(normal) missing required parameter</summary>
		parameter_missing = 0x0606,
		/// <summary>(normal) invalid checksum</summary>
		parameter_checksum = 0x0607,
		/// <summary>(normal) virtual server got a critical error</summary>
		vs_critical = 0x0700,
		/// <summary>(normal) Connection lost</summary>
		connection_lost = 0x0701,
		/// <summary>(normal) not connected</summary>
		not_connected = 0x0702,
		/// <summary>(normal) no cached connection info</summary>
		no_cached_connection_info = 0x0703,
		/// <summary>(normal) currently not possible</summary>
		currently_not_possible = 0x0704,
		/// <summary>(normal) failed connection initialization</summary>
		failed_connection_initialisation = 0x0705,
		/// <summary>(normal) could not resolve hostname</summary>
		could_not_resolve_hostname = 0x0706,
		/// <summary>(normal) invalid server connection handler ID</summary>
		invalid_server_connection_handler_id = 0x0707,
		/// <summary>(normal) could not initialize Input Manager</summary>
		could_not_initialise_input_manager = 0x0708,
		/// <summary>(normal) client library not initialized</summary>
		clientlibrary_not_initialised = 0x0709,
		/// <summary>(normal) server library not initialized</summary>
		serverlibrary_not_initialised = 0x070a,
		/// <summary>(normal) too many whisper targets</summary>
		whisper_too_many_targets = 0x070b,
		/// <summary>(normal) no whisper targets found</summary>
		whisper_no_targets = 0x070c,
		/// <summary>(rare) invalid file name</summary>
		file_invalid_name = 0x0800,
		/// <summary>(rare) invalid file permissions</summary>
		file_invalid_permissions = 0x0801,
		/// <summary>(rare) file already exists</summary>
		file_already_exists = 0x0802,
		/// <summary>(rare) file not found</summary>
		file_not_found = 0x0803,
		/// <summary>(rare) file input/output error</summary>
		file_io_error = 0x0804,
		/// <summary>(rare) invalid file transfer ID</summary>
		file_invalid_transfer_id = 0x0805,
		/// <summary>(rare) invalid file path</summary>
		file_invalid_path = 0x0806,
		/// <summary>(rare) no files available</summary>
		file_no_files_available = 0x0807,
		/// <summary>(rare) overwrite excludes resume</summary>
		file_overwrite_excludes_resume = 0x0808,
		/// <summary>(rare) invalid file size</summary>
		file_invalid_size = 0x0809,
		/// <summary>(rare) file already in use</summary>
		file_already_in_use = 0x080a,
		/// <summary>(rare) could not open file transfer connection</summary>
		file_could_not_open_connection = 0x080b,
		/// <summary>(rare) no space left on device (disk full?)</summary>
		file_no_space_left_on_device = 0x080c,
		/// <summary>(rare) file exceeds file system's maximum file size</summary>
		file_exceeds_file_system_maximum_size = 0x080d,
		/// <summary>(rare) file transfer connection timeout</summary>
		file_transfer_connection_timeout = 0x080e,
		/// <summary>(rare) lost file transfer connection</summary>
		file_connection_lost = 0x080f,
		/// <summary>(rare) file exceeds supplied file size</summary>
		file_exceeds_supplied_size = 0x0810,
		/// <summary>(rare) file transfer complete</summary>
		file_transfer_complete = 0x0811,
		/// <summary>(rare) file transfer canceled</summary>
		file_transfer_canceled = 0x0812,
		/// <summary>(rare) file transfer interrupted</summary>
		file_transfer_interrupted = 0x0813,
		/// <summary>(rare) file transfer server quota exceeded</summary>
		file_transfer_server_quota_exceeded = 0x0814,
		/// <summary>(rare) file transfer client quota exceeded</summary>
		file_transfer_client_quota_exceeded = 0x0815,
		/// <summary>(rare) file transfer reset</summary>
		file_transfer_reset = 0x0816,
		/// <summary>(rare) file transfer limit reached</summary>
		file_transfer_limit_reached = 0x0817,
		/// <summary>(normal) preprocessor disabled</summary>
		sound_preprocessor_disabled = 0x0900,
		/// <summary>(normal) internal preprocessor</summary>
		sound_internal_preprocessor = 0x0901,
		/// <summary>(normal) internal encoder</summary>
		sound_internal_encoder = 0x0902,
		/// <summary>(normal) internal playback</summary>
		sound_internal_playback = 0x0903,
		/// <summary>(normal) no capture device available</summary>
		sound_no_capture_device_available = 0x0904,
		/// <summary>(normal) no playback device available</summary>
		sound_no_playback_device_available = 0x0905,
		/// <summary>(normal) could not open capture device</summary>
		sound_could_not_open_capture_device = 0x0906,
		/// <summary>(normal) could not open playback device</summary>
		sound_could_not_open_playback_device = 0x0907,
		/// <summary>(normal) ServerConnectionHandler has a device registered</summary>
		sound_handler_has_device = 0x0908,
		/// <summary>(normal) invalid capture device</summary>
		sound_invalid_capture_device = 0x0909,
		/// <summary>(normal) invalid clayback device</summary>
		sound_invalid_playback_device = 0x090a,
		/// <summary>(normal) invalid wave file</summary>
		sound_invalid_wave = 0x090b,
		/// <summary>(normal) wave file type not supported</summary>
		sound_unsupported_wave = 0x090c,
		/// <summary>(normal) could not open wave file</summary>
		sound_open_wave = 0x090d,
		/// <summary>(normal) internal capture</summary>
		sound_internal_capture = 0x090e,
		/// <summary>(normal) device still in use</summary>
		sound_device_in_use = 0x090f,
		/// <summary>(normal) device already registerred</summary>
		sound_device_already_registerred = 0x0910,
		/// <summary>(normal) device not registered/known</summary>
		sound_unknown_device = 0x0911,
		/// <summary>(normal) unsupported frequency</summary>
		sound_unsupported_frequency = 0x0912,
		/// <summary>(normal) invalid channel count</summary>
		sound_invalid_channel_count = 0x0913,
		/// <summary>(normal) read error in wave</summary>
		sound_read_wave = 0x0914,
		/// <summary>(normal) sound need more data</summary>
		sound_need_more_data = 0x0915,
		/// <summary>(normal) sound device was busy</summary>
		sound_device_busy = 0x0916,
		/// <summary>(normal) there is no sound data for this period</summary>
		sound_no_data = 0x0917,
		/// <summary>(normal) Channelmask set bits count (speakers) is not the same as (count)</summary>
		sound_channel_mask_mismatch = 0x0918,
		/// <summary>(rare) invalid group ID</summary>
		permission_invalid_group_id = 0x0a00,
		/// <summary>(rare) duplicate entry</summary>
		permission_duplicate_entry = 0x0a01,
		/// <summary>(rare) invalid permission ID</summary>
		permission_invalid_perm_id = 0x0a02,
		/// <summary>(rare) empty result set</summary>
		permission_empty_result = 0x0a03,
		/// <summary>(rare) access to default group is forbidden</summary>
		permission_default_group_forbidden = 0x0a04,
		/// <summary>(rare) invalid size</summary>
		permission_invalid_size = 0x0a05,
		/// <summary>(rare) invalid value</summary>
		permission_invalid_value = 0x0a06,
		/// <summary>(rare) group is not empty</summary>
		permissions_group_not_empty = 0x0a07,
		/// <summary>(normal) insufficient client permissions</summary>
		permissions_client_insufficient = 0x0a08,
		/// <summary>(rare) insufficient group modify power</summary>
		permissions_insufficient_group_power = 0x0a09,
		/// <summary>(rare) insufficient permission modify power</summary>
		permissions_insufficient_permission_power = 0x0a0a,
		/// <summary>(rare) template group is currently used</summary>
		permission_template_group_is_used = 0x0a0b,
		/// <summary>(normal) permission error</summary>
		permissions = 0x0a0c,
		/// <summary>(normal) virtualserver limit reached</summary>
		accounting_virtualserver_limit_reached = 0x0b00,
		/// <summary>(normal) max slot limit reached</summary>
		accounting_slot_limit_reached = 0x0b01,
		/// <summary>(normal) license file not found</summary>
		accounting_license_file_not_found = 0x0b02,
		/// <summary>(normal) license date not ok</summary>
		accounting_license_date_not_ok = 0x0b03,
		/// <summary>(normal) unable to connect to accounting server</summary>
		accounting_unable_to_connect_to_server = 0x0b04,
		/// <summary>(normal) unknown accounting error</summary>
		accounting_unknown_error = 0x0b05,
		/// <summary>(normal) accounting server error</summary>
		accounting_server_error = 0x0b06,
		/// <summary>(normal) instance limit reached</summary>
		accounting_instance_limit_reached = 0x0b07,
		/// <summary>(normal) instance check error</summary>
		accounting_instance_check_error = 0x0b08,
		/// <summary>(normal) license file invalid</summary>
		accounting_license_file_invalid = 0x0b09,
		/// <summary>(normal) virtualserver is running elsewhere</summary>
		accounting_running_elsewhere = 0x0b0a,
		/// <summary>(normal) virtualserver running in same instance already</summary>
		accounting_instance_duplicated = 0x0b0b,
		/// <summary>(normal) virtualserver already started</summary>
		accounting_already_started = 0x0b0c,
		/// <summary>(normal) virtualserver not started</summary>
		accounting_not_started = 0x0b0d,
		/// <summary>(normal) </summary>
		accounting_to_many_starts = 0x0b0e,
		/// <summary>(rare) invalid message id</summary>
		message_invalid_id = 0x0c00,
		/// <summary>(rare) invalid ban id</summary>
		ban_invalid_id = 0x0d00,
		/// <summary>(rare) connection failed, you are banned</summary>
		connect_failed_banned = 0x0d01,
		/// <summary>(rare) rename failed, new name is banned</summary>
		rename_failed_banned = 0x0d02,
		/// <summary>(rare) flood ban</summary>
		ban_flooding = 0x0d03,
		/// <summary>(rare) unable to initialize tts</summary>
		tts_unable_to_initialize = 0x0e00,
		/// <summary>(rare) invalid privilege key</summary>
		privilege_key_invalid = 0x0f00,
		/// <summary>(rare) </summary>
		voip_pjsua = 0x1000,
		/// <summary>(rare) </summary>
		voip_already_initialized = 0x1001,
		/// <summary>(rare) </summary>
		voip_too_many_accounts = 0x1002,
		/// <summary>(rare) </summary>
		voip_invalid_account = 0x1003,
		/// <summary>(rare) </summary>
		voip_internal_error = 0x1004,
		/// <summary>(rare) </summary>
		voip_invalid_connectionId = 0x1005,
		/// <summary>(rare) </summary>
		voip_cannot_answer_initiated_call = 0x1006,
		/// <summary>(rare) </summary>
		voip_not_initialized = 0x1007,
		/// <summary>(normal) invalid password</summary>
		provisioning_invalid_password = 0x1100,
		/// <summary>(normal) invalid request</summary>
		provisioning_invalid_request = 0x1101,
		/// <summary>(normal) no(more) slots available</summary>
		provisioning_no_slots_available = 0x1102,
		/// <summary>(normal) pool missing</summary>
		provisioning_pool_missing = 0x1103,
		/// <summary>(normal) pool unknown</summary>
		provisioning_pool_unknown = 0x1104,
		/// <summary>(normal) unknown ip location(perhaps LAN ip?)</summary>
		provisioning_unknown_ip_location = 0x1105,
		/// <summary>(normal) internal error(tried exceeded)</summary>
		provisioning_internal_tries_exceeded = 0x1106,
		/// <summary>(normal) too many slots requested</summary>
		provisioning_too_many_slots_requested = 0x1107,
		/// <summary>(normal) too many reserved</summary>
		provisioning_too_many_reserved = 0x1108,
		/// <summary>(normal) could not connect to provisioning server</summary>
		provisioning_could_not_connect = 0x1109,
		/// <summary>(normal) authentication server not connected</summary>
		provisioning_auth_server_not_connected = 0x1110,
		/// <summary>(normal) authentication data too large</summary>
		provisioning_auth_data_too_large = 0x1111,
		/// <summary>(normal) already initialized</summary>
		provisioning_already_initialized = 0x1112,
		/// <summary>(normal) not initialized</summary>
		provisioning_not_initialized = 0x1113,
		/// <summary>(normal) already connecting</summary>
		provisioning_connecting = 0x1114,
		/// <summary>(normal) already connected</summary>
		provisioning_already_connected = 0x1115,
		/// <summary>(normal) </summary>
		provisioning_not_connected = 0x1116,
		/// <summary>(normal) io_error</summary>
		provisioning_io_error = 0x1117,
		/// <summary>(normal) </summary>
		provisioning_invalid_timeout = 0x1118,
		/// <summary>(normal) </summary>
		provisioning_ts3server_not_found = 0x1119,
		/// <summary>(normal) unknown permissionID</summary>
		provisioning_no_permission = 0x111A,

		/// <summary>For own custom errors</summary>
		custom_error = 0xFFFF,
		// ReSharper enable InconsistentNaming, UnusedMember.Global
	}

	// Source: https://www.tsviewer.com/index.php?page=faq&id=12&newlanguage=en
	public enum PermissionId : int
	{
		undefined = -1,
		unknown = 0,
		b_serverinstance_help_view = 1,
		b_serverinstance_version_view = 2,
		b_serverinstance_info_view = 3,
		b_serverinstance_virtualserver_list = 4,
		b_serverinstance_binding_list = 5,
		b_serverinstance_permission_list = 6,
		b_serverinstance_permission_find = 7,
		b_virtualserver_create = 8,
		b_virtualserver_delete = 9,
		b_virtualserver_start_any = 10,
		b_virtualserver_stop_any = 11,
		b_virtualserver_change_machine_id = 12,
		b_virtualserver_change_template = 13,
		b_serverquery_login = 14,
		b_serverinstance_textmessage_send = 15,
		b_serverinstance_log_view = 16,
		b_serverinstance_log_add = 17,
		b_serverinstance_stop = 18,
		b_serverinstance_modify_settings = 19,
		b_serverinstance_modify_querygroup = 20,
		b_serverinstance_modify_templates = 21,
		b_virtualserver_select = 22,
		b_virtualserver_info_view = 23,
		b_virtualserver_connectioninfo_view = 24,
		b_virtualserver_channel_list = 25,
		b_virtualserver_channel_search = 26,
		b_virtualserver_client_list = 27,
		b_virtualserver_client_search = 28,
		b_virtualserver_client_dblist = 29,
		b_virtualserver_client_dbsearch = 30,
		b_virtualserver_client_dbinfo = 31,
		b_virtualserver_permission_find = 32,
		b_virtualserver_custom_search = 33,
		b_virtualserver_start = 34,
		b_virtualserver_stop = 35,
		b_virtualserver_token_list = 36,
		b_virtualserver_token_add = 37,
		b_virtualserver_token_use = 38,
		b_virtualserver_token_delete = 39,
		b_virtualserver_log_view = 40,
		b_virtualserver_log_add = 41,
		b_virtualserver_join_ignore_password = 42,
		b_virtualserver_notify_register = 43,
		b_virtualserver_notify_unregister = 44,
		b_virtualserver_snapshot_create = 45,
		b_virtualserver_snapshot_deploy = 46,
		b_virtualserver_permission_reset = 47,
		b_virtualserver_modify_name = 48,
		b_virtualserver_modify_welcomemessage = 49,
		b_virtualserver_modify_maxclients = 50,
		b_virtualserver_modify_reserved_slots = 51,
		b_virtualserver_modify_password = 52,
		b_virtualserver_modify_default_servergroup = 53,
		b_virtualserver_modify_default_channelgroup = 54,
		b_virtualserver_modify_default_channeladmingroup = 55,
		b_virtualserver_modify_channel_forced_silence = 56,
		b_virtualserver_modify_complain = 57,
		b_virtualserver_modify_antiflood = 58,
		b_virtualserver_modify_ft_settings = 59,
		b_virtualserver_modify_ft_quotas = 60,
		b_virtualserver_modify_hostmessage = 61,
		b_virtualserver_modify_hostbanner = 62,
		b_virtualserver_modify_hostbutton = 63,
		b_virtualserver_modify_port = 64,
		b_virtualserver_modify_autostart = 65,
		b_virtualserver_modify_needed_identity_security_level = 66,
		b_virtualserver_modify_priority_speaker_dimm_modificator = 67,
		b_virtualserver_modify_log_settings = 68,
		b_virtualserver_modify_min_client_version = 69,
		b_virtualserver_modify_icon_id = 70,
		b_virtualserver_modify_weblist = 71,
		b_virtualserver_modify_codec_encryption_mode = 72,
		b_virtualserver_modify_temporary_passwords = 73,
		b_virtualserver_modify_temporary_passwords_own = 74,
		b_virtualserver_modify_channel_temp_delete_delay_default = 75,
		i_channel_min_depth = 76,
		i_channel_max_depth = 77,
		b_channel_group_inheritance_end = 78,
		i_channel_permission_modify_power = 79,
		i_channel_needed_permission_modify_power = 80,
		b_channel_info_view = 81,
		b_channel_create_child = 82,
		b_channel_create_permanent = 83,
		b_channel_create_semi_permanent = 84,
		b_channel_create_temporary = 85,
		b_channel_create_private = 86,
		b_channel_create_with_topic = 87,
		b_channel_create_with_description = 88,
		b_channel_create_with_password = 89,
		b_channel_create_modify_with_codec_speex8 = 90,
		b_channel_create_modify_with_codec_speex16 = 91,
		b_channel_create_modify_with_codec_speex32 = 92,
		b_channel_create_modify_with_codec_celtmono48 = 93,
		b_channel_create_modify_with_codec_opusvoice = 94,
		b_channel_create_modify_with_codec_opusmusic = 95,
		i_channel_create_modify_with_codec_maxquality = 96,
		i_channel_create_modify_with_codec_latency_factor_min = 97,
		b_channel_create_with_maxclients = 98,
		b_channel_create_with_maxfamilyclients = 99,
		b_channel_create_with_sortorder = 100,
		b_channel_create_with_default = 101,
		b_channel_create_with_needed_talk_power = 102,
		b_channel_create_modify_with_force_password = 103,
		i_channel_create_modify_with_temp_delete_delay = 104,
		b_channel_modify_parent = 105,
		b_channel_modify_make_default = 106,
		b_channel_modify_make_permanent = 107,
		b_channel_modify_make_semi_permanent = 108,
		b_channel_modify_make_temporary = 109,
		b_channel_modify_name = 110,
		b_channel_modify_topic = 111,
		b_channel_modify_description = 112,
		b_channel_modify_password = 113,
		b_channel_modify_codec = 114,
		b_channel_modify_codec_quality = 115,
		b_channel_modify_codec_latency_factor = 116,
		b_channel_modify_maxclients = 117,
		b_channel_modify_maxfamilyclients = 118,
		b_channel_modify_sortorder = 119,
		b_channel_modify_needed_talk_power = 120,
		i_channel_modify_power = 121,
		i_channel_needed_modify_power = 122,
		b_channel_modify_make_codec_encrypted = 123,
		b_channel_modify_temp_delete_delay = 124,
		b_channel_delete_permanent = 125,
		b_channel_delete_semi_permanent = 126,
		b_channel_delete_temporary = 127,
		b_channel_delete_flag_force = 128,
		i_channel_delete_power = 129,
		i_channel_needed_delete_power = 130,
		b_channel_join_permanent = 131,
		b_channel_join_semi_permanent = 132,
		b_channel_join_temporary = 133,
		b_channel_join_ignore_password = 134,
		b_channel_join_ignore_maxclients = 135,
		i_channel_join_power = 136,
		i_channel_needed_join_power = 137,
		i_channel_subscribe_power = 138,
		i_channel_needed_subscribe_power = 139,
		i_channel_description_view_power = 140,
		i_channel_needed_description_view_power = 141,
		i_icon_id = 142,
		i_max_icon_filesize = 143,
		b_icon_manage = 144,
		b_group_is_permanent = 145,
		i_group_auto_update_type = 146,
		i_group_auto_update_max_value = 147,
		i_group_sort_id = 148,
		i_group_show_name_in_tree = 149,
		b_virtualserver_servergroup_list = 150,
		b_virtualserver_servergroup_permission_list = 151,
		b_virtualserver_servergroup_client_list = 152,
		b_virtualserver_channelgroup_list = 153,
		b_virtualserver_channelgroup_permission_list = 154,
		b_virtualserver_channelgroup_client_list = 155,
		b_virtualserver_client_permission_list = 156,
		b_virtualserver_channel_permission_list = 157,
		b_virtualserver_channelclient_permission_list = 158,
		b_virtualserver_servergroup_create = 159,
		b_virtualserver_channelgroup_create = 160,
		i_group_modify_power = 161,
		i_group_needed_modify_power = 162,
		i_group_member_add_power = 163,
		i_group_needed_member_add_power = 164,
		i_group_member_remove_power = 165,
		i_group_needed_member_remove_power = 166,
		i_permission_modify_power = 167,
		b_permission_modify_power_ignore = 168,
		b_virtualserver_servergroup_delete = 169,
		b_virtualserver_channelgroup_delete = 170,
		i_client_permission_modify_power = 171,
		i_client_needed_permission_modify_power = 172,
		i_client_max_clones_uid = 173,
		i_client_max_idletime = 174,
		i_client_max_avatar_filesize = 175,
		i_client_max_channel_subscriptions = 176,
		b_client_is_priority_speaker = 177,
		b_client_skip_channelgroup_permissions = 178,
		b_client_force_push_to_talk = 179,
		b_client_ignore_bans = 180,
		b_client_ignore_antiflood = 181,
		b_client_issue_client_query_command = 182,
		b_client_use_reserved_slot = 183,
		b_client_use_channel_commander = 184,
		b_client_request_talker = 185,
		b_client_avatar_delete_other = 186,
		b_client_is_sticky = 187,
		b_client_ignore_sticky = 188,
		b_client_info_view = 189,
		b_client_permissionoverview_view = 190,
		b_client_permissionoverview_own = 191,
		b_client_remoteaddress_view = 192,
		i_client_serverquery_view_power = 193,
		i_client_needed_serverquery_view_power = 194,
		b_client_custom_info_view = 195,
		i_client_kick_from_server_power = 196,
		i_client_needed_kick_from_server_power = 197,
		i_client_kick_from_channel_power = 198,
		i_client_needed_kick_from_channel_power = 199,
		i_client_ban_power = 200,
		i_client_needed_ban_power = 201,
		i_client_move_power = 202,
		i_client_needed_move_power = 203,
		i_client_complain_power = 204,
		i_client_needed_complain_power = 205,
		b_client_complain_list = 206,
		b_client_complain_delete_own = 207,
		b_client_complain_delete = 208,
		b_client_ban_list = 209,
		b_client_ban_create = 210,
		b_client_ban_delete_own = 211,
		b_client_ban_delete = 212,
		i_client_ban_max_bantime = 213,
		i_client_private_textmessage_power = 214,
		i_client_needed_private_textmessage_power = 215,
		b_client_server_textmessage_send = 216,
		b_client_channel_textmessage_send = 217,
		b_client_offline_textmessage_send = 218,
		i_client_talk_power = 219,
		i_client_needed_talk_power = 220,
		i_client_poke_power = 221,
		i_client_needed_poke_power = 222,
		b_client_set_flag_talker = 223,
		i_client_whisper_power = 224,
		i_client_needed_whisper_power = 225,
		b_client_modify_description = 226,
		b_client_modify_own_description = 227,
		b_client_modify_dbproperties = 228,
		b_client_delete_dbproperties = 229,
		b_client_create_modify_serverquery_login = 230,
		b_ft_ignore_password = 231,
		b_ft_transfer_list = 232,
		i_ft_file_upload_power = 233,
		i_ft_needed_file_upload_power = 234,
		i_ft_file_download_power = 235,
		i_ft_needed_file_download_power = 236,
		i_ft_file_delete_power = 237,
		i_ft_needed_file_delete_power = 238,
		i_ft_file_rename_power = 239,
		i_ft_needed_file_rename_power = 240,
		i_ft_file_browse_power = 241,
		i_ft_needed_file_browse_power = 242,
		i_ft_directory_create_power = 243,
		i_ft_needed_directory_create_power = 244,
		i_ft_quota_mb_download_per_client = 245,
		i_ft_quota_mb_upload_per_client = 246,
	}

	public static class PerissionInfo
	{
		public static string Get(PermissionId permid)
		{
			switch (permid)
			{
			case PermissionId.undefined: return "Undefined permission";
			case PermissionId.unknown: return "Unknown permission";
			case PermissionId.b_serverinstance_help_view: return "Retrieve information about ServerQuery commands";
			case PermissionId.b_serverinstance_version_view: return "Retrieve global server version (including platform and build number)";
			case PermissionId.b_serverinstance_info_view: return "Retrieve global server information";
			case PermissionId.b_serverinstance_virtualserver_list: return "List virtual servers stored in the database";
			case PermissionId.b_serverinstance_binding_list: return "List active IP bindings on multi-homed machines";
			case PermissionId.b_serverinstance_permission_list: return "List permissions available available on the server instance";
			case PermissionId.b_serverinstance_permission_find: return "Search permission assignments by name or ID";
			case PermissionId.b_virtualserver_create: return "Create virtual servers";
			case PermissionId.b_virtualserver_delete: return "Delete virtual servers";
			case PermissionId.b_virtualserver_start_any: return "Start any virtual server in the server instance";
			case PermissionId.b_virtualserver_stop_any: return "Stop any virtual server in the server instance";
			case PermissionId.b_virtualserver_change_machine_id: return "Change a virtual servers machine ID";
			case PermissionId.b_virtualserver_change_template: return "Edit virtual server default template values";
			case PermissionId.b_serverquery_login: return "Login to ServerQuery";
			case PermissionId.b_serverinstance_textmessage_send: return "Send text messages to all virtual servers at once";
			case PermissionId.b_serverinstance_log_view: return "Retrieve global server log";
			case PermissionId.b_serverinstance_log_add: return "Write to global server log";
			case PermissionId.b_serverinstance_stop: return "Shutdown the server process";
			case PermissionId.b_serverinstance_modify_settings: return "Edit global settings";
			case PermissionId.b_serverinstance_modify_querygroup: return "Edit global ServerQuery groups";
			case PermissionId.b_serverinstance_modify_templates: return "Edit global template groups";
			case PermissionId.b_virtualserver_select: return "Select a virtual server";
			case PermissionId.b_virtualserver_info_view: return "Retrieve virtual server information";
			case PermissionId.b_virtualserver_connectioninfo_view: return "Retrieve virtual server connection information";
			case PermissionId.b_virtualserver_channel_list: return "List channels on a virtual server";
			case PermissionId.b_virtualserver_channel_search: return "Search for channels on a virtual server";
			case PermissionId.b_virtualserver_client_list: return "List clients online on a virtual server";
			case PermissionId.b_virtualserver_client_search: return "Search for clients online on a virtual server";
			case PermissionId.b_virtualserver_client_dblist: return "List client identities known by the virtual server";
			case PermissionId.b_virtualserver_client_dbsearch: return "Search for client identities known by the virtual server";
			case PermissionId.b_virtualserver_client_dbinfo: return "Retrieve client information";
			case PermissionId.b_virtualserver_permission_find: return "Find permissions";
			case PermissionId.b_virtualserver_custom_search: return "Find custom fields";
			case PermissionId.b_virtualserver_start: return "Start own virtual server";
			case PermissionId.b_virtualserver_stop: return "Stop own virtual server";
			case PermissionId.b_virtualserver_token_list: return "List privilege keys available";
			case PermissionId.b_virtualserver_token_add: return "Create new privilege keys";
			case PermissionId.b_virtualserver_token_use: return "Use a privilege keys to gain access to groups";
			case PermissionId.b_virtualserver_token_delete: return "Delete a privilege key";
			case PermissionId.b_virtualserver_log_view: return "Retrieve virtual server log";
			case PermissionId.b_virtualserver_log_add: return "Write to virtual server log";
			case PermissionId.b_virtualserver_join_ignore_password: return "Join virtual server ignoring its password";
			case PermissionId.b_virtualserver_notify_register: return "Register for server notifications";
			case PermissionId.b_virtualserver_notify_unregister: return "Unregister from server notifications";
			case PermissionId.b_virtualserver_snapshot_create: return "Create server snapshots";
			case PermissionId.b_virtualserver_snapshot_deploy: return "Deploy server snapshots";
			case PermissionId.b_virtualserver_permission_reset: return "Reset the server permission settings to default values";
			case PermissionId.b_virtualserver_modify_name: return "Modify server name";
			case PermissionId.b_virtualserver_modify_welcomemessage: return "Modify welcome message";
			case PermissionId.b_virtualserver_modify_maxclients: return "Modify servers max clients";
			case PermissionId.b_virtualserver_modify_reserved_slots: return "Modify reserved slots";
			case PermissionId.b_virtualserver_modify_password: return "Modify server password";
			case PermissionId.b_virtualserver_modify_default_servergroup: return "Modify default Server Group";
			case PermissionId.b_virtualserver_modify_default_channelgroup: return "Modify default Channel Group";
			case PermissionId.b_virtualserver_modify_default_channeladmingroup: return "Modify default Channel Admin Group";
			case PermissionId.b_virtualserver_modify_channel_forced_silence: return "Modify channel force silence value";
			case PermissionId.b_virtualserver_modify_complain: return "Modify individual complain settings";
			case PermissionId.b_virtualserver_modify_antiflood: return "Modify individual antiflood settings";
			case PermissionId.b_virtualserver_modify_ft_settings: return "Modify file transfer settings";
			case PermissionId.b_virtualserver_modify_ft_quotas: return "Modify file transfer quotas";
			case PermissionId.b_virtualserver_modify_hostmessage: return "Modify individual hostmessage settings";
			case PermissionId.b_virtualserver_modify_hostbanner: return "Modify individual hostbanner settings";
			case PermissionId.b_virtualserver_modify_hostbutton: return "Modify individual hostbutton settings";
			case PermissionId.b_virtualserver_modify_port: return "Modify server port";
			case PermissionId.b_virtualserver_modify_autostart: return "Modify server autostart";
			case PermissionId.b_virtualserver_modify_needed_identity_security_level: return "Modify required identity security level";
			case PermissionId.b_virtualserver_modify_priority_speaker_dimm_modificator: return "Modify priority speaker dimm modificator";
			case PermissionId.b_virtualserver_modify_log_settings: return "Modify log settings";
			case PermissionId.b_virtualserver_modify_min_client_version: return "Modify min client version";
			case PermissionId.b_virtualserver_modify_icon_id: return "Modify server icon";
			case PermissionId.b_virtualserver_modify_weblist: return "Modify web server list reporting settings";
			case PermissionId.b_virtualserver_modify_codec_encryption_mode: return "Modify codec encryption mode";
			case PermissionId.b_virtualserver_modify_temporary_passwords: return "Modify temporary serverpasswords";
			case PermissionId.b_virtualserver_modify_temporary_passwords_own: return "Modify own temporary serverpasswords";
			case PermissionId.b_virtualserver_modify_channel_temp_delete_delay_default: return "Modify default temporary channel delete delay";
			case PermissionId.i_channel_min_depth: return "Min channel creation depth in hierarchy";
			case PermissionId.i_channel_max_depth: return "Max channel creation depth in hierarchy";
			case PermissionId.b_channel_group_inheritance_end: return "Stop inheritance of channel group permissions";
			case PermissionId.i_channel_permission_modify_power: return "Modify channel permission power";
			case PermissionId.i_channel_needed_permission_modify_power: return "Needed modify channel permission power";
			case PermissionId.b_channel_info_view: return "Retrieve channel information";
			case PermissionId.b_channel_create_child: return "Create sub-channels";
			case PermissionId.b_channel_create_permanent: return "Create permanent channels";
			case PermissionId.b_channel_create_semi_permanent: return "Create semi-permanent channels";
			case PermissionId.b_channel_create_temporary: return "Create temporary channels";
			case PermissionId.b_channel_create_private: return "Create private channel";
			case PermissionId.b_channel_create_with_topic: return "Create channels with a topic";
			case PermissionId.b_channel_create_with_description: return "Create channels with a description";
			case PermissionId.b_channel_create_with_password: return "Create password protected channels";
			case PermissionId.b_channel_create_modify_with_codec_speex8: return "Create channels using Speex Narrowband (8 kHz) codecs";
			case PermissionId.b_channel_create_modify_with_codec_speex16: return "Create channels using Speex Wideband (16 kHz) codecs";
			case PermissionId.b_channel_create_modify_with_codec_speex32: return "Create channels using Speex Ultra-Wideband (32 kHz) codecs";
			case PermissionId.b_channel_create_modify_with_codec_celtmono48: return "Create channels using the CELT Mono (48 kHz) codec ";
			case PermissionId.b_channel_create_modify_with_codec_opusvoice: return "Create channels using OPUS (voice) codec";
			case PermissionId.b_channel_create_modify_with_codec_opusmusic: return "Create channels using OPUS (music) codec";
			case PermissionId.i_channel_create_modify_with_codec_maxquality: return "Create channels with custom codec quality";
			case PermissionId.i_channel_create_modify_with_codec_latency_factor_min: return "Create channels with minimal custom codec latency factor";
			case PermissionId.b_channel_create_with_maxclients: return "Create channels with custom max clients";
			case PermissionId.b_channel_create_with_maxfamilyclients: return "Create channels with custom max family clients";
			case PermissionId.b_channel_create_with_sortorder: return "Create channels with custom sort order";
			case PermissionId.b_channel_create_with_default: return "Create default channels";
			case PermissionId.b_channel_create_with_needed_talk_power: return "Create channels with needed talk power";
			case PermissionId.b_channel_create_modify_with_force_password: return "Create new channels only with password";
			case PermissionId.i_channel_create_modify_with_temp_delete_delay: return "Max delete delay for temporary channels";
			case PermissionId.b_channel_modify_parent: return "Move channels";
			case PermissionId.b_channel_modify_make_default: return "Make channel default";
			case PermissionId.b_channel_modify_make_permanent: return "Make channel permanent";
			case PermissionId.b_channel_modify_make_semi_permanent: return "Make channel semi-permanent";
			case PermissionId.b_channel_modify_make_temporary: return "Make channel temporary";
			case PermissionId.b_channel_modify_name: return "Modify channel name";
			case PermissionId.b_channel_modify_topic: return "Modify channel topic";
			case PermissionId.b_channel_modify_description: return "Modify channel description";
			case PermissionId.b_channel_modify_password: return "Modify channel password";
			case PermissionId.b_channel_modify_codec: return "Modify channel codec";
			case PermissionId.b_channel_modify_codec_quality: return "Modify channel codec quality";
			case PermissionId.b_channel_modify_codec_latency_factor: return "Modify channel codec latency factor";
			case PermissionId.b_channel_modify_maxclients: return "Modify channels max clients";
			case PermissionId.b_channel_modify_maxfamilyclients: return "Modify channels max family clients";
			case PermissionId.b_channel_modify_sortorder: return "Modify channel sort order";
			case PermissionId.b_channel_modify_needed_talk_power: return "Change needed channel talk power";
			case PermissionId.i_channel_modify_power: return "Channel modify power";
			case PermissionId.i_channel_needed_modify_power: return "Needed channel modify power";
			case PermissionId.b_channel_modify_make_codec_encrypted: return "Make channel codec encrypted";
			case PermissionId.b_channel_modify_temp_delete_delay: return "Modify temporary channel delete delay";
			case PermissionId.b_channel_delete_permanent: return "Delete permanent channels";
			case PermissionId.b_channel_delete_semi_permanent: return "Delete semi-permanent channels";
			case PermissionId.b_channel_delete_temporary: return "Delete temporary channels";
			case PermissionId.b_channel_delete_flag_force: return "Force channel delete";
			case PermissionId.i_channel_delete_power: return "Delete channel power";
			case PermissionId.i_channel_needed_delete_power: return "Needed delete channel power";
			case PermissionId.b_channel_join_permanent: return "Join permanent channels";
			case PermissionId.b_channel_join_semi_permanent: return "Join semi-permanent channels";
			case PermissionId.b_channel_join_temporary: return "Join temporary channels";
			case PermissionId.b_channel_join_ignore_password: return "Join channel ignoring its password";
			case PermissionId.b_channel_join_ignore_maxclients: return "Ignore channels max clients limit";
			case PermissionId.i_channel_join_power: return "Channel join power";
			case PermissionId.i_channel_needed_join_power: return "Needed channel join power";
			case PermissionId.i_channel_subscribe_power: return "Channel subscribe power";
			case PermissionId.i_channel_needed_subscribe_power: return "Needed channel subscribe power";
			case PermissionId.i_channel_description_view_power: return "Channel description view power";
			case PermissionId.i_channel_needed_description_view_power: return "Needed channel needed description view power";
			case PermissionId.i_icon_id: return "Group icon identifier";
			case PermissionId.i_max_icon_filesize: return "Max icon filesize in bytes";
			case PermissionId.b_icon_manage: return "Enables icon management";
			case PermissionId.b_group_is_permanent: return "Group is permanent";
			case PermissionId.i_group_auto_update_type: return "Group auto-update type";
			case PermissionId.i_group_auto_update_max_value: return "Group auto-update max value";
			case PermissionId.i_group_sort_id: return "Group sort id";
			case PermissionId.i_group_show_name_in_tree: return "Show group name in tree depending on selected mode";
			case PermissionId.b_virtualserver_servergroup_list: return "List server groups";
			case PermissionId.b_virtualserver_servergroup_permission_list: return "List server group permissions";
			case PermissionId.b_virtualserver_servergroup_client_list: return "List clients from a server group";
			case PermissionId.b_virtualserver_channelgroup_list: return "List channel groups ";
			case PermissionId.b_virtualserver_channelgroup_permission_list: return "List channel group permissions";
			case PermissionId.b_virtualserver_channelgroup_client_list: return "List clients from a channel group";
			case PermissionId.b_virtualserver_client_permission_list: return "List client permissions";
			case PermissionId.b_virtualserver_channel_permission_list: return "List channel permissions";
			case PermissionId.b_virtualserver_channelclient_permission_list: return "List channel client permissions";
			case PermissionId.b_virtualserver_servergroup_create: return "Create server groups";
			case PermissionId.b_virtualserver_channelgroup_create: return "Create channel groups";
			case PermissionId.i_group_modify_power: return "Group modify power";
			case PermissionId.i_group_needed_modify_power: return "Needed group modify power";
			case PermissionId.i_group_member_add_power: return "Group member add power";
			case PermissionId.i_group_needed_member_add_power: return "Needed group member add power";
			case PermissionId.i_group_member_remove_power: return "Group member delete power";
			case PermissionId.i_group_needed_member_remove_power: return "Needed group member delete power";
			case PermissionId.i_permission_modify_power: return "Permission modify power";
			case PermissionId.b_permission_modify_power_ignore: return "Ignore needed permission modify power";
			case PermissionId.b_virtualserver_servergroup_delete: return "Delete server groups";
			case PermissionId.b_virtualserver_channelgroup_delete: return "Delete channel groups";
			case PermissionId.i_client_permission_modify_power: return "Client permission modify power";
			case PermissionId.i_client_needed_permission_modify_power: return "Needed client permission modify power";
			case PermissionId.i_client_max_clones_uid: return "Max additional connections per client identity";
			case PermissionId.i_client_max_idletime: return "Max idle time in seconds";
			case PermissionId.i_client_max_avatar_filesize: return "Max avatar filesize in bytes";
			case PermissionId.i_client_max_channel_subscriptions: return "Max channel subscriptions";
			case PermissionId.b_client_is_priority_speaker: return "Client is priority speaker";
			case PermissionId.b_client_skip_channelgroup_permissions: return "Ignore channel group permissions";
			case PermissionId.b_client_force_push_to_talk: return "Force Push-To-Talk capture mode";
			case PermissionId.b_client_ignore_bans: return "Ignore bans";
			case PermissionId.b_client_ignore_antiflood: return "Ignore antiflood measurements";
			case PermissionId.b_client_issue_client_query_command: return "Issue query commands from client";
			case PermissionId.b_client_use_reserved_slot: return "Use an reserved slot";
			case PermissionId.b_client_use_channel_commander: return "Use channel commander";
			case PermissionId.b_client_request_talker: return "Allow to request talk power";
			case PermissionId.b_client_avatar_delete_other: return "Allow deletion of avatars from other clients";
			case PermissionId.b_client_is_sticky: return "Client will be sticked to current channel";
			case PermissionId.b_client_ignore_sticky: return "Client ignores sticky flag";
			case PermissionId.b_client_info_view: return "Retrieve client information";
			case PermissionId.b_client_permissionoverview_view: return "Retrieve client permissions overview";
			case PermissionId.b_client_permissionoverview_own: return "Retrieve clients own permissions overview";
			case PermissionId.b_client_remoteaddress_view: return "View client IP address and port";
			case PermissionId.i_client_serverquery_view_power: return "ServerQuery view power";
			case PermissionId.i_client_needed_serverquery_view_power: return "Needed ServerQuery view power";
			case PermissionId.b_client_custom_info_view: return "View custom fields";
			case PermissionId.i_client_kick_from_server_power: return "Client kick power from server";
			case PermissionId.i_client_needed_kick_from_server_power: return "Needed client kick power from server";
			case PermissionId.i_client_kick_from_channel_power: return "Client kick power from channel";
			case PermissionId.i_client_needed_kick_from_channel_power: return "Needed client kick power from channel";
			case PermissionId.i_client_ban_power: return "Client ban power";
			case PermissionId.i_client_needed_ban_power: return "Needed client ban power";
			case PermissionId.i_client_move_power: return "Client move power";
			case PermissionId.i_client_needed_move_power: return "Needed client move power";
			case PermissionId.i_client_complain_power: return "Complain power";
			case PermissionId.i_client_needed_complain_power: return "Needed complain power";
			case PermissionId.b_client_complain_list: return "Show complain list";
			case PermissionId.b_client_complain_delete_own: return "Delete own complains";
			case PermissionId.b_client_complain_delete: return "Delete complains";
			case PermissionId.b_client_ban_list: return "Show banlist";
			case PermissionId.b_client_ban_create: return "Add a ban";
			case PermissionId.b_client_ban_delete_own: return "Delete own bans";
			case PermissionId.b_client_ban_delete: return "Delete bans";
			case PermissionId.i_client_ban_max_bantime: return "Max bantime";
			case PermissionId.i_client_private_textmessage_power: return "Client private message power";
			case PermissionId.i_client_needed_private_textmessage_power: return "Needed client private message power";
			case PermissionId.b_client_server_textmessage_send: return "Send text messages to virtual server";
			case PermissionId.b_client_channel_textmessage_send: return "Send text messages to channel";
			case PermissionId.b_client_offline_textmessage_send: return "Send offline messages to clients";
			case PermissionId.i_client_talk_power: return "Client talk power";
			case PermissionId.i_client_needed_talk_power: return "Needed client talk power";
			case PermissionId.i_client_poke_power: return "Client poke power";
			case PermissionId.i_client_needed_poke_power: return "Needed client poke power";
			case PermissionId.b_client_set_flag_talker: return "Set the talker flag for clients and allow them to speak";
			case PermissionId.i_client_whisper_power: return "Client whisper power";
			case PermissionId.i_client_needed_whisper_power: return "Client needed whisper power";
			case PermissionId.b_client_modify_description: return "Edit a clients description";
			case PermissionId.b_client_modify_own_description: return "Allow client to edit own description";
			case PermissionId.b_client_modify_dbproperties: return "Edit a clients properties in the database";
			case PermissionId.b_client_delete_dbproperties: return "Delete a clients properties in the database";
			case PermissionId.b_client_create_modify_serverquery_login: return "Create or modify own ServerQuery account";
			case PermissionId.b_ft_ignore_password: return "Browse files without channel password";
			case PermissionId.b_ft_transfer_list: return "Retrieve list of running filetransfers";
			case PermissionId.i_ft_file_upload_power: return "File upload power";
			case PermissionId.i_ft_needed_file_upload_power: return "Needed file upload power";
			case PermissionId.i_ft_file_download_power: return "File download power";
			case PermissionId.i_ft_needed_file_download_power: return "Needed file download power";
			case PermissionId.i_ft_file_delete_power: return "File delete power";
			case PermissionId.i_ft_needed_file_delete_power: return "Needed file delete power";
			case PermissionId.i_ft_file_rename_power: return "File rename power";
			case PermissionId.i_ft_needed_file_rename_power: return "Needed file rename power";
			case PermissionId.i_ft_file_browse_power: return "File browse power";
			case PermissionId.i_ft_needed_file_browse_power: return "Needed file browse power";
			case PermissionId.i_ft_directory_create_power: return "Create directory power";
			case PermissionId.i_ft_needed_directory_create_power: return "Needed create directory power";
			case PermissionId.i_ft_quota_mb_download_per_client: return "Download quota per client in MByte";
			case PermissionId.i_ft_quota_mb_upload_per_client: return "Upload quota per client in MByte";
			default: throw Util.UnhandledDefault(permid);
			}
		}
	}
}
