namespace TS3Query.Messages
{
	using System;

	[QuerySubInterface]
	public interface ITargetChannelId
	{
		[QuerySerialized("ctid")]
		int TargetChannelId { get; set; }
	}

	[QuerySubInterface]
	public interface IClientId
	{
		[QuerySerialized("clid")]
		ushort ClientId { get; set; }
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
		int SourceChannelId { get; set; }
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

	public interface ClientDbData : IResponse, ClientData, IClientBaseData2
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

		[QuerySerialized("client_lastip")]
		string LastIp { get; set; }
	}

	public interface ClientServerGroup : IResponse, IClientDbId
	{
		[QuerySerialized("name")]
		string Name { get; set; }

		[QuerySerialized("sgid")]
		int ServerGroupId { get; set; }
	}

	[QueryNotification(NotificationType.ClientEnterView)]
	public interface ClientEnterView : INotification, IReason, ITargetChannelId, IInvokedNotification, IClientId, IClientBaseData, ISourceChannelId, IClientUidLong, IClientBaseData2
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
		int ChannelGroupId { get; set; }

		[QuerySerialized("client_servergroups")]
		string ServerGroups { get; set; }

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
		int InheritedChannelGroupFromChannelId { get; set; }

		[QuerySerialized("client_badges")]
		string Badges { get; set; }
	}

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
}
