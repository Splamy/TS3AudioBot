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
		ConnectionInfo,
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
		/// <summary>Targets all users in the specified server group.
		/// (Requires servergroup targetId)</summary>
		ServerGroup = 0,
		/// <summary>Targets all users in the specified channel group.
		/// (Requires channelgroup targetId)</summary>
		ChannelGroup,
		/// <summary>Targets all users with channel commander.</summary>
		ChannelCommander,
		/// <summary>Targets all users on the server.</summary>
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
}
