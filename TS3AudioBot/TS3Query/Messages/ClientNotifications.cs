namespace TS3Query.Messages
{
	using System;

	public abstract class ClientNotifications : Notification
	{
		[QuerySerialized("ctid")]
		public int TargetChannelId;

		[QuerySerialized("reasonid")]
		public MoveReason Reason;
	}

	public abstract class InvokedClientNotification : ClientNotifications
	{
		[QuerySerialized("invokerid")]
		public ushort InvokerId;

		[QuerySerialized("invokername")]
		public string InvokerName;

		[QuerySerialized("invokeruid")]
		public string InvokerUid;
	}

	[NotificationName(NotificationType.ClientEnterView)]
	public class ClientEnterView : ClientNotifications
	{
		[QuerySerialized("clid")]
		public ushort ClientId;

		[QuerySerialized("cfid")]
		public int SourceChannelId;

		[QuerySerialized("client_unique_identifier")]
		public string Uid;

		[QuerySerialized("client_nickname")]
		public string NickName;

		[QuerySerialized("client_input_muted")]
		public bool IsInputMuted;

		[QuerySerialized("client_output_muted")]
		public bool IsOutputMuted;

		[QuerySerialized("client_outputonly_muted")]
		public bool IsOutputOnlyMuted;

		[QuerySerialized("client_input_hardware")]
		public bool IsInputHardware;

		[QuerySerialized("client_output_hardware")]
		public bool IsClientOutputHardware;

		[QuerySerialized("client_meta_data")]
		public string Metadata;

		[QuerySerialized("client_is_recording")]
		public bool IsRecording;

		[QuerySerialized("client_database_id")]
		public ulong DatabaseId;

		[QuerySerialized("client_channel_group_id")]
		public int ChannelGroupId;

		[QuerySerialized("client_servergroups")]
		public string ServerGroups;

		[QuerySerialized("client_away")]
		public bool IsAway;

		[QuerySerialized("client_away_message")]
		public string AwayMessage;

		[QuerySerialized("client_type")]
		public ClientType Type;

		[QuerySerialized("client_flag_avatar")]
		public string AvatarFlag;

		[QuerySerialized("client_talk_power")]
		public int TalkPower;

		[QuerySerialized("client_talk_request")]
		public int RequestedTalkPower;

		[QuerySerialized("client_talk_request_msg")]
		public string TalkPowerRequestMessage;

		[QuerySerialized("client_description")]
		public string Description;

		[QuerySerialized("client_is_talker")]
		public bool IsTalker;

		[QuerySerialized("client_is_priority_speaker")]
		public bool IsPrioritySpeaker;

		[QuerySerialized("client_unread_messages")]
		public int UnreadMessages;

		[QuerySerialized("client_nickname_phonetic")]
		public string PhoneticName;

		[QuerySerialized("client_needed_serverquery_view_power")]
		public bool NeededServerQueryViewPower;

		[QuerySerialized("client_icon_id")]
		public long IconId;

		[QuerySerialized("client_is_channel_commander")]
		public bool IsChannelCommander;

		[QuerySerialized("client_country")]
		public string CountryCode;

		[QuerySerialized("client_channel_group_inherited_channel_id")]
		public int InheritedChannelGroupFromChannelId;

		[QuerySerialized("client_badges")]
		public string Badges;
	}

	[NotificationName(NotificationType.ClientLeftView)]
	public class ClientLeftView : InvokedClientNotification
	{
		[QuerySerialized("clid")]
		public ushort ClientId;

		[QuerySerialized("cfid")]
		public int SourceChannelId;

		[QuerySerialized("reasonmsg")]
		public string ReasonMessage;

		[QuerySerialized("bantime")]
		public TimeSpan BanTime;
	}

	[NotificationName(NotificationType.ClientMoved)]
	public class ClientMoved : InvokedClientNotification
	{
		[QuerySerialized("clid")]
		public ushort[] ClientIds;
	}
}
