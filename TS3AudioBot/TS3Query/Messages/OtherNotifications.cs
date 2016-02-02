namespace TS3Query.Messages
{
	using System;

	public abstract class InvokedNotifiction : Notification
	{
		[QuerySerialized("invokerid")]
		public ushort InvokerId;

		[QuerySerialized("invokername")]
		public string InvokerName;

		[QuerySerialized("invokeruid")]
		public string InvokerUid;
	}

	[NotificationName(NotificationType.ServerEdited)]
	public class ServerEdited : InvokedNotifiction
	{
		[QuerySerialized("reasonid")]
		public MoveReason Reason;

		[QuerySerialized("virtualserver_name")]
		public string ServerName;

		[QuerySerialized("virtualserver_codec_encryption_mode")]
		public CodecEncryptionMode CodecEncryptionMode;

		[QuerySerialized("virtualserver_default_server_group")]
		public int DefaultServerGroup;

		[QuerySerialized("virtualserver_default_channel_group")]
		public int DefaultChannelGroup;

		[QuerySerialized("virtualserver_hostbanner_url")]
		public string HostbannerUrl;

		[QuerySerialized("virtualserver_hostbanner_gfx_url")]
		public string HostbannerGfxUrl;

		[QuerySerialized("virtualserver_hostbanner_gfx_interval")]
		public TimeSpan HostbannerGfxInterval;

		[QuerySerialized("virtualserver_priority_speaker_dimm_modificator")]
		public float PrioritySpeakerDimmModificator;

		[QuerySerialized("virtualserver_hostbutton_tooltip")]
		public string HostButtonTooltipText;

		[QuerySerialized("virtualserver_hostbutton_url")]
		public string HostButtonUrl;

		[QuerySerialized("virtualserver_hostbutton_gfx_url")]
		public string HostButtonGfxUrl;

		[QuerySerialized("virtualserver_name_phonetic")]
		public string PhoneticName;

		[QuerySerialized("virtualserver_icon_id")]
		public long IconId;

		[QuerySerialized("virtualserver_hostbanner_mode")]
		public HostBannerMode HostbannerMode;

		[QuerySerialized("virtualserver_channel_temp_delete_delay_default")]
		public TimeSpan TempChannelDefaultDeleteDelay;
	}

	[NotificationName(NotificationType.TextMessage)]
	public class TextMessage : InvokedNotifiction
	{
		[QuerySerialized("targetmode")]
		public MessageTarget Target;

		[QuerySerialized("msg")]
		public string Message;

		[QuerySerialized("target")]
		public int TargetClientId;
	}

	[NotificationName(NotificationType.TokenUsed)]
	public class TokenUsed : Notification
	{
		[QuerySerialized("clid")]
		public ushort ClientId;

		[QuerySerialized("cldbid")]
		public ulong ClientDatabaseId;

		[QuerySerialized("cluid")]
		public string ClientUid;

		[QuerySerialized("token")]
		public string UsedToken;

		[QuerySerialized("tokencustomset")]
		public string TokenCustomSet;

		[QuerySerialized("token1")]
		public string Token1;

		[QuerySerialized("token2")]
		public string Token2;
	}
}
