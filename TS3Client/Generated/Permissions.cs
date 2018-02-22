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
	using Helper;

	// Source: https://www.tsviewer.com/index.php?page=faq&id=12&newlanguage=en
	public enum PermissionId : int
	{
		// ReSharper disable InconsistentNaming, UnusedMember.Global
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
		// ReSharper restore InconsistentNaming, UnusedMember.Global
	}

	public static class PerissionInfo
	{
		public static string Get(PermissionId permid)
		{
			switch (permid)
			{
			case PermissionId.undefined: return "Undefined permission";
			case PermissionId.unknown : return "May occour on error returns with no associated permission";
			case PermissionId.b_serverinstance_help_view : return "Retrieve information about ServerQuery commands";
			case PermissionId.b_serverinstance_version_view : return "Retrieve global server version (including platform and build number)";
			case PermissionId.b_serverinstance_info_view : return "Retrieve global server information";
			case PermissionId.b_serverinstance_virtualserver_list : return "List virtual servers stored in the database";
			case PermissionId.b_serverinstance_binding_list : return "List active IP bindings on multi-homed machines";
			case PermissionId.b_serverinstance_permission_list : return "List permissions available available on the server instance";
			case PermissionId.b_serverinstance_permission_find : return "Search permission assignments by name or ID";
			case PermissionId.b_virtualserver_create : return "Create virtual servers";
			case PermissionId.b_virtualserver_delete : return "Delete virtual servers";
			case PermissionId.b_virtualserver_start_any : return "Start any virtual server in the server instance";
			case PermissionId.b_virtualserver_stop_any : return "Stop any virtual server in the server instance";
			case PermissionId.b_virtualserver_change_machine_id : return "Change a virtual servers machine ID";
			case PermissionId.b_virtualserver_change_template : return "Edit virtual server default template values";
			case PermissionId.b_serverquery_login : return "Login to ServerQuery";
			case PermissionId.b_serverinstance_textmessage_send : return "Send text messages to all virtual servers at once";
			case PermissionId.b_serverinstance_log_view : return "Retrieve global server log";
			case PermissionId.b_serverinstance_log_add : return "Write to global server log";
			case PermissionId.b_serverinstance_stop : return "Shutdown the server process";
			case PermissionId.b_serverinstance_modify_settings : return "Edit global settings";
			case PermissionId.b_serverinstance_modify_querygroup : return "Edit global ServerQuery groups";
			case PermissionId.b_serverinstance_modify_templates : return "Edit global template groups";
			case PermissionId.b_virtualserver_select : return "Select a virtual server";
			case PermissionId.b_virtualserver_info_view : return "Retrieve virtual server information";
			case PermissionId.b_virtualserver_connectioninfo_view : return "Retrieve virtual server connection information";
			case PermissionId.b_virtualserver_channel_list : return "List channels on a virtual server";
			case PermissionId.b_virtualserver_channel_search : return "Search for channels on a virtual server";
			case PermissionId.b_virtualserver_client_list : return "List clients online on a virtual server";
			case PermissionId.b_virtualserver_client_search : return "Search for clients online on a virtual server";
			case PermissionId.b_virtualserver_client_dblist : return "List client identities known by the virtual server";
			case PermissionId.b_virtualserver_client_dbsearch : return "Search for client identities known by the virtual server";
			case PermissionId.b_virtualserver_client_dbinfo : return "Retrieve client information";
			case PermissionId.b_virtualserver_permission_find : return "Find permissions";
			case PermissionId.b_virtualserver_custom_search : return "Find custom fields";
			case PermissionId.b_virtualserver_start : return "Start own virtual server";
			case PermissionId.b_virtualserver_stop : return "Stop own virtual server";
			case PermissionId.b_virtualserver_token_list : return "List privilege keys available";
			case PermissionId.b_virtualserver_token_add : return "Create new privilege keys";
			case PermissionId.b_virtualserver_token_use : return "Use a privilege keys to gain access to groups";
			case PermissionId.b_virtualserver_token_delete : return "Delete a privilege key";
			case PermissionId.b_virtualserver_log_view : return "Retrieve virtual server log";
			case PermissionId.b_virtualserver_log_add : return "Write to virtual server log";
			case PermissionId.b_virtualserver_join_ignore_password : return "Join virtual server ignoring its password";
			case PermissionId.b_virtualserver_notify_register : return "Register for server notifications";
			case PermissionId.b_virtualserver_notify_unregister : return "Unregister from server notifications";
			case PermissionId.b_virtualserver_snapshot_create : return "Create server snapshots";
			case PermissionId.b_virtualserver_snapshot_deploy : return "Deploy server snapshots";
			case PermissionId.b_virtualserver_permission_reset : return "Reset the server permission settings to default values";
			case PermissionId.b_virtualserver_modify_name : return "Modify server name";
			case PermissionId.b_virtualserver_modify_welcomemessage : return "Modify welcome message";
			case PermissionId.b_virtualserver_modify_maxclients : return "Modify servers max clients";
			case PermissionId.b_virtualserver_modify_reserved_slots : return "Modify reserved slots";
			case PermissionId.b_virtualserver_modify_password : return "Modify server password";
			case PermissionId.b_virtualserver_modify_default_servergroup : return "Modify default Server Group";
			case PermissionId.b_virtualserver_modify_default_channelgroup : return "Modify default Channel Group";
			case PermissionId.b_virtualserver_modify_default_channeladmingroup : return "Modify default Channel Admin Group";
			case PermissionId.b_virtualserver_modify_channel_forced_silence : return "Modify channel force silence value";
			case PermissionId.b_virtualserver_modify_complain : return "Modify individual complain settings";
			case PermissionId.b_virtualserver_modify_antiflood : return "Modify individual antiflood settings";
			case PermissionId.b_virtualserver_modify_ft_settings : return "Modify file transfer settings";
			case PermissionId.b_virtualserver_modify_ft_quotas : return "Modify file transfer quotas";
			case PermissionId.b_virtualserver_modify_hostmessage : return "Modify individual hostmessage settings";
			case PermissionId.b_virtualserver_modify_hostbanner : return "Modify individual hostbanner settings";
			case PermissionId.b_virtualserver_modify_hostbutton : return "Modify individual hostbutton settings";
			case PermissionId.b_virtualserver_modify_port : return "Modify server port";
			case PermissionId.b_virtualserver_modify_autostart : return "Modify server autostart";
			case PermissionId.b_virtualserver_modify_needed_identity_security_level : return "Modify required identity security level";
			case PermissionId.b_virtualserver_modify_priority_speaker_dimm_modificator : return "Modify priority speaker dimm modificator";
			case PermissionId.b_virtualserver_modify_log_settings : return "Modify log settings";
			case PermissionId.b_virtualserver_modify_min_client_version : return "Modify min client version";
			case PermissionId.b_virtualserver_modify_icon_id : return "Modify server icon";
			case PermissionId.b_virtualserver_modify_weblist : return "Modify web server list reporting settings";
			case PermissionId.b_virtualserver_modify_codec_encryption_mode : return "Modify codec encryption mode";
			case PermissionId.b_virtualserver_modify_temporary_passwords : return "Modify temporary serverpasswords";
			case PermissionId.b_virtualserver_modify_temporary_passwords_own : return "Modify own temporary serverpasswords";
			case PermissionId.b_virtualserver_modify_channel_temp_delete_delay_default : return "Modify default temporary channel delete delay";
			case PermissionId.i_channel_min_depth : return "Min channel creation depth in hierarchy";
			case PermissionId.i_channel_max_depth : return "Max channel creation depth in hierarchy";
			case PermissionId.b_channel_group_inheritance_end : return "Stop inheritance of channel group permissions";
			case PermissionId.i_channel_permission_modify_power : return "Modify channel permission power";
			case PermissionId.i_channel_needed_permission_modify_power : return "Needed modify channel permission power";
			case PermissionId.b_channel_info_view : return "Retrieve channel information";
			case PermissionId.b_channel_create_child : return "Create sub-channels";
			case PermissionId.b_channel_create_permanent : return "Create permanent channels";
			case PermissionId.b_channel_create_semi_permanent : return "Create semi-permanent channels";
			case PermissionId.b_channel_create_temporary : return "Create temporary channels";
			case PermissionId.b_channel_create_private : return "Create private channel";
			case PermissionId.b_channel_create_with_topic : return "Create channels with a topic";
			case PermissionId.b_channel_create_with_description : return "Create channels with a description";
			case PermissionId.b_channel_create_with_password : return "Create password protected channels";
			case PermissionId.b_channel_create_modify_with_codec_speex8 : return "Create channels using Speex Narrowband (8 kHz) codecs";
			case PermissionId.b_channel_create_modify_with_codec_speex16 : return "Create channels using Speex Wideband (16 kHz) codecs";
			case PermissionId.b_channel_create_modify_with_codec_speex32 : return "Create channels using Speex Ultra-Wideband (32 kHz) codecs";
			case PermissionId.b_channel_create_modify_with_codec_celtmono48 : return "Create channels using the CELT Mono (48 kHz) codec";
			case PermissionId.b_channel_create_modify_with_codec_opusvoice : return "Create channels using OPUS (voice) codec";
			case PermissionId.b_channel_create_modify_with_codec_opusmusic : return "Create channels using OPUS (music) codec";
			case PermissionId.i_channel_create_modify_with_codec_maxquality : return "Create channels with custom codec quality";
			case PermissionId.i_channel_create_modify_with_codec_latency_factor_min : return "Create channels with minimal custom codec latency factor";
			case PermissionId.b_channel_create_with_maxclients : return "Create channels with custom max clients";
			case PermissionId.b_channel_create_with_maxfamilyclients : return "Create channels with custom max family clients";
			case PermissionId.b_channel_create_with_sortorder : return "Create channels with custom sort order";
			case PermissionId.b_channel_create_with_default : return "Create default channels";
			case PermissionId.b_channel_create_with_needed_talk_power : return "Create channels with needed talk power";
			case PermissionId.b_channel_create_modify_with_force_password : return "Create new channels only with password";
			case PermissionId.i_channel_create_modify_with_temp_delete_delay : return "Max delete delay for temporary channels";
			case PermissionId.b_channel_modify_parent : return "Move channels";
			case PermissionId.b_channel_modify_make_default : return "Make channel default";
			case PermissionId.b_channel_modify_make_permanent : return "Make channel permanent";
			case PermissionId.b_channel_modify_make_semi_permanent : return "Make channel semi-permanent";
			case PermissionId.b_channel_modify_make_temporary : return "Make channel temporary";
			case PermissionId.b_channel_modify_name : return "Modify channel name";
			case PermissionId.b_channel_modify_topic : return "Modify channel topic";
			case PermissionId.b_channel_modify_description : return "Modify channel description";
			case PermissionId.b_channel_modify_password : return "Modify channel password";
			case PermissionId.b_channel_modify_codec : return "Modify channel codec";
			case PermissionId.b_channel_modify_codec_quality : return "Modify channel codec quality";
			case PermissionId.b_channel_modify_codec_latency_factor : return "Modify channel codec latency factor";
			case PermissionId.b_channel_modify_maxclients : return "Modify channels max clients";
			case PermissionId.b_channel_modify_maxfamilyclients : return "Modify channels max family clients";
			case PermissionId.b_channel_modify_sortorder : return "Modify channel sort order";
			case PermissionId.b_channel_modify_needed_talk_power : return "Change needed channel talk power";
			case PermissionId.i_channel_modify_power : return "Channel modify power";
			case PermissionId.i_channel_needed_modify_power : return "Needed channel modify power";
			case PermissionId.b_channel_modify_make_codec_encrypted : return "Make channel codec encrypted";
			case PermissionId.b_channel_modify_temp_delete_delay : return "Modify temporary channel delete delay";
			case PermissionId.b_channel_delete_permanent : return "Delete permanent channels";
			case PermissionId.b_channel_delete_semi_permanent : return "Delete semi-permanent channels";
			case PermissionId.b_channel_delete_temporary : return "Delete temporary channels";
			case PermissionId.b_channel_delete_flag_force : return "Force channel delete";
			case PermissionId.i_channel_delete_power : return "Delete channel power";
			case PermissionId.i_channel_needed_delete_power : return "Needed delete channel power";
			case PermissionId.b_channel_join_permanent : return "Join permanent channels";
			case PermissionId.b_channel_join_semi_permanent : return "Join semi-permanent channels";
			case PermissionId.b_channel_join_temporary : return "Join temporary channels";
			case PermissionId.b_channel_join_ignore_password : return "Join channel ignoring its password";
			case PermissionId.b_channel_join_ignore_maxclients : return "Ignore channels max clients limit";
			case PermissionId.i_channel_join_power : return "Channel join power";
			case PermissionId.i_channel_needed_join_power : return "Needed channel join power";
			case PermissionId.i_channel_subscribe_power : return "Channel subscribe power";
			case PermissionId.i_channel_needed_subscribe_power : return "Needed channel subscribe power";
			case PermissionId.i_channel_description_view_power : return "Channel description view power";
			case PermissionId.i_channel_needed_description_view_power : return "Needed channel needed description view power";
			case PermissionId.i_icon_id : return "Group icon identifier";
			case PermissionId.i_max_icon_filesize : return "Max icon filesize in bytes";
			case PermissionId.b_icon_manage : return "Enables icon management";
			case PermissionId.b_group_is_permanent : return "Group is permanent";
			case PermissionId.i_group_auto_update_type : return "Group auto-update type";
			case PermissionId.i_group_auto_update_max_value : return "Group auto-update max value";
			case PermissionId.i_group_sort_id : return "Group sort id";
			case PermissionId.i_group_show_name_in_tree : return "Show group name in tree depending on selected mode";
			case PermissionId.b_virtualserver_servergroup_list : return "List server groups";
			case PermissionId.b_virtualserver_servergroup_permission_list : return "List server group permissions";
			case PermissionId.b_virtualserver_servergroup_client_list : return "List clients from a server group";
			case PermissionId.b_virtualserver_channelgroup_list : return "List channel groups";
			case PermissionId.b_virtualserver_channelgroup_permission_list : return "List channel group permissions";
			case PermissionId.b_virtualserver_channelgroup_client_list : return "List clients from a channel group";
			case PermissionId.b_virtualserver_client_permission_list : return "List client permissions";
			case PermissionId.b_virtualserver_channel_permission_list : return "List channel permissions";
			case PermissionId.b_virtualserver_channelclient_permission_list : return "List channel client permissions";
			case PermissionId.b_virtualserver_servergroup_create : return "Create server groups";
			case PermissionId.b_virtualserver_channelgroup_create : return "Create channel groups";
			case PermissionId.i_group_modify_power : return "Group modify power";
			case PermissionId.i_group_needed_modify_power : return "Needed group modify power";
			case PermissionId.i_group_member_add_power : return "Group member add power";
			case PermissionId.i_group_needed_member_add_power : return "Needed group member add power";
			case PermissionId.i_group_member_remove_power : return "Group member delete power";
			case PermissionId.i_group_needed_member_remove_power : return "Needed group member delete power";
			case PermissionId.i_permission_modify_power : return "Permission modify power";
			case PermissionId.b_permission_modify_power_ignore : return "Ignore needed permission modify power";
			case PermissionId.b_virtualserver_servergroup_delete : return "Delete server groups";
			case PermissionId.b_virtualserver_channelgroup_delete : return "Delete channel groups";
			case PermissionId.i_client_permission_modify_power : return "Client permission modify power";
			case PermissionId.i_client_needed_permission_modify_power : return "Needed client permission modify power";
			case PermissionId.i_client_max_clones_uid : return "Max additional connections per client identity";
			case PermissionId.i_client_max_idletime : return "Max idle time in seconds";
			case PermissionId.i_client_max_avatar_filesize : return "Max avatar filesize in bytes";
			case PermissionId.i_client_max_channel_subscriptions : return "Max channel subscriptions";
			case PermissionId.b_client_is_priority_speaker : return "Client is priority speaker";
			case PermissionId.b_client_skip_channelgroup_permissions : return "Ignore channel group permissions";
			case PermissionId.b_client_force_push_to_talk : return "Force Push-To-Talk capture mode";
			case PermissionId.b_client_ignore_bans : return "Ignore bans";
			case PermissionId.b_client_ignore_antiflood : return "Ignore antiflood measurements";
			case PermissionId.b_client_issue_client_query_command : return "Issue query commands from client";
			case PermissionId.b_client_use_reserved_slot : return "Use an reserved slot";
			case PermissionId.b_client_use_channel_commander : return "Use channel commander";
			case PermissionId.b_client_request_talker : return "Allow to request talk power";
			case PermissionId.b_client_avatar_delete_other : return "Allow deletion of avatars from other clients";
			case PermissionId.b_client_is_sticky : return "Client will be sticked to current channel";
			case PermissionId.b_client_ignore_sticky : return "Client ignores sticky flag";
			case PermissionId.b_client_info_view : return "Retrieve client information";
			case PermissionId.b_client_permissionoverview_view : return "Retrieve client permissions overview";
			case PermissionId.b_client_permissionoverview_own : return "Retrieve clients own permissions overview";
			case PermissionId.b_client_remoteaddress_view : return "View client IP address and port";
			case PermissionId.i_client_serverquery_view_power : return "ServerQuery view power";
			case PermissionId.i_client_needed_serverquery_view_power : return "Needed ServerQuery view power";
			case PermissionId.b_client_custom_info_view : return "View custom fields";
			case PermissionId.i_client_kick_from_server_power : return "Client kick power from server";
			case PermissionId.i_client_needed_kick_from_server_power : return "Needed client kick power from server";
			case PermissionId.i_client_kick_from_channel_power : return "Client kick power from channel";
			case PermissionId.i_client_needed_kick_from_channel_power : return "Needed client kick power from channel";
			case PermissionId.i_client_ban_power : return "Client ban power";
			case PermissionId.i_client_needed_ban_power : return "Needed client ban power";
			case PermissionId.i_client_move_power : return "Client move power";
			case PermissionId.i_client_needed_move_power : return "Needed client move power";
			case PermissionId.i_client_complain_power : return "Complain power";
			case PermissionId.i_client_needed_complain_power : return "Needed complain power";
			case PermissionId.b_client_complain_list : return "Show complain list";
			case PermissionId.b_client_complain_delete_own : return "Delete own complains";
			case PermissionId.b_client_complain_delete : return "Delete complains";
			case PermissionId.b_client_ban_list : return "Show banlist";
			case PermissionId.b_client_ban_create : return "Add a ban";
			case PermissionId.b_client_ban_delete_own : return "Delete own bans";
			case PermissionId.b_client_ban_delete : return "Delete bans";
			case PermissionId.i_client_ban_max_bantime : return "Max bantime";
			case PermissionId.i_client_private_textmessage_power : return "Client private message power";
			case PermissionId.i_client_needed_private_textmessage_power : return "Needed client private message power";
			case PermissionId.b_client_server_textmessage_send : return "Send text messages to virtual server";
			case PermissionId.b_client_channel_textmessage_send : return "Send text messages to channel";
			case PermissionId.b_client_offline_textmessage_send : return "Send offline messages to clients";
			case PermissionId.i_client_talk_power : return "Client talk power";
			case PermissionId.i_client_needed_talk_power : return "Needed client talk power";
			case PermissionId.i_client_poke_power : return "Client poke power";
			case PermissionId.i_client_needed_poke_power : return "Needed client poke power";
			case PermissionId.b_client_set_flag_talker : return "Set the talker flag for clients and allow them to speak";
			case PermissionId.i_client_whisper_power : return "Client whisper power";
			case PermissionId.i_client_needed_whisper_power : return "Client needed whisper power";
			case PermissionId.b_client_modify_description : return "Edit a clients description";
			case PermissionId.b_client_modify_own_description : return "Allow client to edit own description";
			case PermissionId.b_client_modify_dbproperties : return "Edit a clients properties in the database";
			case PermissionId.b_client_delete_dbproperties : return "Delete a clients properties in the database";
			case PermissionId.b_client_create_modify_serverquery_login : return "Create or modify own ServerQuery account";
			case PermissionId.b_ft_ignore_password : return "Browse files without channel password";
			case PermissionId.b_ft_transfer_list : return "Retrieve list of running filetransfers";
			case PermissionId.i_ft_file_upload_power : return "File upload power";
			case PermissionId.i_ft_needed_file_upload_power : return "Needed file upload power";
			case PermissionId.i_ft_file_download_power : return "File download power";
			case PermissionId.i_ft_needed_file_download_power : return "Needed file download power";
			case PermissionId.i_ft_file_delete_power : return "File delete power";
			case PermissionId.i_ft_needed_file_delete_power : return "Needed file delete power";
			case PermissionId.i_ft_file_rename_power : return "File rename power";
			case PermissionId.i_ft_needed_file_rename_power : return "Needed file rename power";
			case PermissionId.i_ft_file_browse_power : return "File browse power";
			case PermissionId.i_ft_needed_file_browse_power : return "Needed file browse power";
			case PermissionId.i_ft_directory_create_power : return "Create directory power";
			case PermissionId.i_ft_needed_directory_create_power : return "Needed create directory power";
			case PermissionId.i_ft_quota_mb_download_per_client : return "Download quota per client in MByte";
			case PermissionId.i_ft_quota_mb_upload_per_client : return "Upload quota per client in MByte";
			default: throw Util.UnhandledDefault(permid);
			}
		}
	}
}