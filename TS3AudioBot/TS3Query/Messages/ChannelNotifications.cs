namespace TS3Query.Messages
{
	using System;

	public abstract class ChannelNotification : Notification
	{
		[QuerySerialized("cid")]
		public int ChannelId;
	}

	public abstract class InvokedChannelNotification : ChannelNotification
	{
		[QuerySerialized("invokerid")]
		public int InvokerId;

		[QuerySerialized("invokername")]
		public string InvokerName;

		[QuerySerialized("invokeruid")]
		public string InvokerUid;
	}

	public abstract class ChannelDataNotification : InvokedChannelNotification
	{
		[QuerySerialized("channel_name")]
		public string Name;

		[QuerySerialized("channel_topic")]
		public string Topic;

		[QuerySerialized("channel_codec")]
		public Codec Codec;

		[QuerySerialized("channel_codec_quality")]
		public int CodecQuality;

		[QuerySerialized("channel_maxclients")]
		public int MaxClients;

		[QuerySerialized("channel_maxfamilyclients")]
		public int MaxFamilyClients;

		[QuerySerialized("channel_order")]
		public int Order;

		[QuerySerialized("channel_flag_permanent")]
		public bool IsPermanent;

		[QuerySerialized("channel_flag_semi_permanent")]
		public bool IsSemiPermanent;

		[QuerySerialized("channel_flag_default")]
		public bool IsDefaultChannel;

		[QuerySerialized("channel_flag_password")]
		public bool HasPassword;

		[QuerySerialized("channel_codec_latency_factor")]
		public int CodecLatencyFactor;

		[QuerySerialized("channel_codec_is_unencrypted")]
		public bool IsUnencrypted;

		[QuerySerialized("channel_delete_delay")]
		public TimeSpan DeleteDelay;

		[QuerySerialized("channel_flag_maxclients_unlimited")]
		public bool IsMaxClientsUnlimited;

		[QuerySerialized("channel_flag_maxfamilyclients_unlimited")]
		public bool IsMaxFamilyClientsUnlimited;

		[QuerySerialized("channel_flag_maxfamilyclients_inherited")]
		public bool IsMaxFamilyClientsInherited;

		[QuerySerialized("channel_needed_talk_power")]
		public int NeededTalkPower;

		[QuerySerialized("channel_name_phonetic")]
		public string PhoneticName;

		[QuerySerialized("channel_icon_id")]
		public string IconId;
	}

	[NotificationName(NotificationType.ChannelCreated)]
	public class ChannelCreated : ChannelDataNotification
	{
		[QuerySerialized("cpid")]
		public int ChannelParentId;
	}

	[NotificationName(NotificationType.ChannelDeleted)]
	public class ChannelDeleted : InvokedChannelNotification { }

	[NotificationName(NotificationType.ChannelChanged)]
	public class ChannelChanged : ChannelNotification { }

	[NotificationName(NotificationType.ChannelEdited)]
	public class ChannelEdited : ChannelDataNotification
	{
		[QuerySerialized("reasonid")]
		public MoveReason Reason;
	}

	[NotificationName(NotificationType.ChannelMoved)]
	public class ChannelMoved : InvokedChannelNotification
	{
		[QuerySerialized("reasonid")]
		public MoveReason Reason;

		[QuerySerialized("cpid")]
		public int ChannelParentId;

		[QuerySerialized("order")]
		public int Order;
	}

	[NotificationName(NotificationType.ChannelPasswordChanged)]
	public class ChannelPasswordChanged : ChannelNotification { }
}
