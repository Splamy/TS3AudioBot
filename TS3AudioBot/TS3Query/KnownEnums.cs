namespace TS3Query
{
	using System;

	public enum ClientType
	{
		Full = 0,
		Query,
	}

	[Flags]
	public enum ClientListOptions
	{
		uid = 1 << 0,
		away = 1 << 1,
		voice = 1 << 2,
		times = 1 << 3,
		groups = 1 << 4,
		info = 1 << 5,
		icon = 1 << 6,
		country = 1 << 7,
	}

	public enum MessageTarget
	{
		[QueryString("textprivate")]
		Private = 1,
		[QueryString("textchannel")]
		Channel,
		[QueryString("textserver")]
		Server,
	}

	public enum RequestTarget
	{
		[QueryString("channel")]
		Channel = 4,
		[QueryString("server")]
		Server,
	}

	enum NotificationType
	{
		[QueryString("notifychannelcreated")]
		ChannelCreated,
		[QueryString("notifychanneldeleted")]
		ChannelDeleted,
		[QueryString("notifychannelchanged")]
		ChannelChanged,
		[QueryString("notifychanneledited")]
		ChannelEdited,
		[QueryString("notifychannelmoved")]
		ChannelMoved,
		[QueryString("notifychannelpasswordchanged")]
		ChannelPasswordChanged,
		[QueryString("notifycliententerview")]
		ClientEnterView,
		[QueryString("notifyclientleftview")]
		ClientLeftView,
		[QueryString("notifyclientmoved")]
		ClientMoved,
		[QueryString("notifyserveredited")]
		ServerEdited,
		[QueryString("notifytextmessage")]
		TextMessage,
		[QueryString("notifytokenused")]
		TokenUsed,
	}

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
}
