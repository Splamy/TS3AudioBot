// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3Client.Messages
{
	using System;

	[QuerySubInterface]
	public interface ITargetChannelId
	{
		[QuerySerialized("ctid")]
		ulong TargetChannelId { get; set; }
	}

	[QuerySubInterface]
	public interface IClientId
	{
		[QuerySerialized("clid")]
		ushort ClientId { get; set; }
	}

	[QuerySubInterface]
	public interface IClientUid
	{
		[QuerySerialized("cluid")]
		string ClientUid { get; set; }
	}

	[QuerySubInterface]
	public interface IClientDbId
	{
		[QuerySerialized("cldbid")]
		ulong ClientDbId { get; set; }
	}

	[QuerySubInterface]
	public interface IClientUidLong
	{
		[QuerySerialized("client_unique_identifier")]
		string Uid { get; set; }
	}

	[QuerySubInterface]
	public interface ISourceChannelId
	{
		[QuerySerialized("cfid")]
		ulong SourceChannelId { get; set; }
	}

	[QuerySubInterface]
	public interface IClientBaseData2
	{
		[QuerySerialized("client_flag_avatar")]
		string AvatarFlag { get; set; }

		[QuerySerialized("client_description")]
		string Description { get; set; }

		[QuerySerialized("client_icon_id")]
		long IconId { get; set; }
	}

	[QuerySubInterface]
	public interface IClientBaseData
	{
		[QuerySerialized("client_database_id")]
		ulong DatabaseId { get; set; }

		[QuerySerialized("client_nickname")]
		string NickName { get; set; }

		[QuerySerialized("client_type")]
		ClientType ClientType { get; set; }
	}

	public interface ClientData : IResponse, IClientId, IChannelId, IClientBaseData { }

	public interface IClientDbDataBase
	{
		[QuerySerialized("client_created")]
		DateTime CreationDate { get; set; }

		[QuerySerialized("client_lastconnected")]
		DateTime LastConnected { get; set; }

		[QuerySerialized("client_totalconnections")]
		int TotalConnections { get; set; }

		[QuerySerialized("client_month_bytes_uploaded")]
		long MonthlyUploadQuota { get; set; }

		[QuerySerialized("client_month_bytes_downloaded")]
		long MonthlyDownloadQuota { get; set; }

		[QuerySerialized("client_total_bytes_uploaded")]
		long TotalUploadQuota { get; set; }

		[QuerySerialized("client_total_bytes_downloaded")]
		long TotalDownloadQuota { get; set; }

		[QuerySerialized("client_base64HashClientUID")]
		string Base64HashClientUID { get; set; }
	}

	public interface ClientDbData : IResponse, ClientData, IClientBaseData2, IClientDbDataBase
	{
		[QuerySerialized("client_lastip")]
		string LastIp { get; set; }
	}

	public interface ClientServerGroup : IResponse, IClientDbId
	{
		[QuerySerialized("name")]
		string Name { get; set; }

		[QuerySerialized("sgid")]
		ulong ServerGroupId { get; set; }
	}

	[QuerySubInterface]
	public interface IClientInfoBase
	{
		[QuerySerialized("client_input_muted")]
		bool IsInputMuted { get; set; }

		[QuerySerialized("client_output_muted")]
		bool IsOutputMuted { get; set; }

		[QuerySerialized("client_outputonly_muted")]
		bool IsOutputOnlyMuted { get; set; }

		[QuerySerialized("client_input_hardware")]
		bool IsInputHardware { get; set; }

		[QuerySerialized("client_output_hardware")]
		bool IsClientOutputHardware { get; set; }

		[QuerySerialized("client_meta_data")]
		string Metadata { get; set; }

		[QuerySerialized("client_is_recording")]
		bool IsRecording { get; set; }

		[QuerySerialized("client_channel_group_id")]
		long ChannelGroupId { get; set; }

		[QuerySerialized("client_servergroups")]
		long[] ServerGroups { get; set; }

		[QuerySerialized("client_away")]
		bool IsAway { get; set; }

		[QuerySerialized("client_away_message")]
		string AwayMessage { get; set; }

		[QuerySerialized("client_talk_power")]
		int TalkPower { get; set; }

		[QuerySerialized("client_talk_request")]
		int RequestedTalkPower { get; set; }

		[QuerySerialized("client_talk_request_msg")]
		string TalkPowerRequestMessage { get; set; }

		[QuerySerialized("client_is_talker")]
		bool IsTalker { get; set; }

		[QuerySerialized("client_is_priority_speaker")]
		bool IsPrioritySpeaker { get; set; }

		[QuerySerialized("client_unread_messages")]
		int UnreadMessages { get; set; }

		[QuerySerialized("client_nickname_phonetic")]
		string PhoneticName { get; set; }

		[QuerySerialized("client_needed_serverquery_view_power")]
		bool NeededServerQueryViewPower { get; set; }

		[QuerySerialized("client_is_channel_commander")]
		bool IsChannelCommander { get; set; }

		[QuerySerialized("client_country")]
		string CountryCode { get; set; }

		[QuerySerialized("client_channel_group_inherited_channel_id")]
		long InheritedChannelGroupFromChannelId { get; set; }

		[QuerySerialized("client_badges")]
		string Badges { get; set; }
	}

	[QueryNotification(NotificationType.ClientEnterView)]
	public interface ClientEnterView : INotification, IReason, ITargetChannelId, IInvokedNotification, IClientId, IClientBaseData, ISourceChannelId, IClientUidLong, IClientBaseData2, IClientInfoBase
	{ }

	[QueryNotification(NotificationType.ClientLeftView)]
	public interface ClientLeftView : INotification, IReason, ITargetChannelId, IInvokedNotification, IClientId, ISourceChannelId
	{
		[QuerySerialized("reasonmsg")]
		string ReasonMessage { get; set; }

		[QuerySerialized("bantime")]
		TimeSpan BanTime { get; set; }
	}

	[QueryNotification(NotificationType.ClientMoved)]
	public interface ClientMoved : INotification, IReason, ITargetChannelId, IInvokedNotification
	{
		[QuerySerialized("clid")]
		ushort[] ClientIds { get; set; }
	}

	public interface ClientInfo : IResponse, IChannelId, IClientUidLong, IClientBaseData, IClientInfoBase, IClientDbDataBase, IClientBaseData2
	{
		[QuerySerialized("client_idle_time")]
		long ClientIdleTimeMs { get; set; }

		[QuerySerialized("client_version")]
		string ClientVersion { get; set; }

		[QuerySerialized("client_version_sign")]
		string ClientVersionSign { get; set; }

		[QuerySerialized("client_platform")]
		string ClientPlattform { get; set; }

		[QuerySerialized("client_default_channel")]
		string DefaultChannel { get; set; }

		[QuerySerialized("client_security_hash")]
		string SecurityHash { get; set; }

		[QuerySerialized("client_login_name")]
		string LoginName { get; set; }

		[QuerySerialized("client_default_token")]
		string DefaultToken { get; set; }

		[QuerySerialized("connection_filetransfer_bandwidth_sent")]
		long ConnectionFiletransferSent { get; set; }

		[QuerySerialized("connection_filetransfer_bandwidth_received")]
		long ConnectionFiletransferReceived { get; set; }

		[QuerySerialized("connection_packets_sent_total")]
		long ConnectionPacketsSent { get; set; }

		[QuerySerialized("connection_packets_received_total")]
		long ConnectionPacketsReceived { get; set; }

		[QuerySerialized("connection_bytes_sent_total")]
		long ConnectionBytesSent { get; set; }

		[QuerySerialized("connection_bytes_received_total")]
		long ConnectionBytesReceived { get; set; }

		[QuerySerialized("connection_bandwidth_sent_last_second_total")]
		long ConnectionBandwidtSentLastSecond { get; set; }

		[QuerySerialized("connection_bandwidth_received_last_second_total")]
		long ConnectionBandwidtReceivedLastSecond { get; set; }

		[QuerySerialized("connection_bandwidth_sent_last_minute_total")]
		long ConnectionBandwidtSentLastMinute { get; set; }

		[QuerySerialized("connection_bandwidth_received_last_minute_total")]
		long ConnectionBandwidtReceivedLastMinute { get; set; }

		[QuerySerialized("connection_connected_time")]
		long ConnectionTimeMs { get; set; }

		[QuerySerialized("connection_client_ip")]
		string Ip { get; set; }
	}
}
