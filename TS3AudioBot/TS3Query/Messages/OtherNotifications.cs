namespace TS3Query.Messages
{
	using System;

	[QuerySubInterface]
	public interface IServerName
	{
		[QuerySerialized("virtualserver_name")]
		string ServerName { get; set; }
	}

	[QueryNotification(NotificationType.ServerEdited)]
	public interface ServerEdited : INotification, IInvokedNotification, IReason, IServerName
	{
		[QuerySerialized("virtualserver_codec_encryption_mode")]
		CodecEncryptionMode CodecEncryptionMode { get; set; }

		[QuerySerialized("virtualserver_default_server_group")]
		int DefaultServerGroup { get; set; }

		[QuerySerialized("virtualserver_default_channel_group")]
		int DefaultChannelGroup { get; set; }

		[QuerySerialized("virtualserver_hostbanner_url")]
		string HostbannerUrl { get; set; }

		[QuerySerialized("virtualserver_hostbanner_gfx_url")]
		string HostbannerGfxUrl { get; set; }

		[QuerySerialized("virtualserver_hostbanner_gfx_interval")]
		TimeSpan HostbannerGfxInterval { get; set; }

		[QuerySerialized("virtualserver_priority_speaker_dimm_modificator")]
		float PrioritySpeakerDimmModificator { get; set; }

		[QuerySerialized("virtualserver_hostbutton_tooltip")]
		string HostButtonTooltipText { get; set; }

		[QuerySerialized("virtualserver_hostbutton_url")]
		string HostButtonUrl { get; set; }

		[QuerySerialized("virtualserver_hostbutton_gfx_url")]
		string HostButtonGfxUrl { get; set; }

		[QuerySerialized("virtualserver_name_phonetic")]
		string PhoneticName { get; set; }

		[QuerySerialized("virtualserver_icon_id")]
		long IconId { get; set; }

		[QuerySerialized("virtualserver_hostbanner_mode")]
		HostBannerMode HostbannerMode { get; set; }

		[QuerySerialized("virtualserver_channel_temp_delete_delay_default")]
		TimeSpan TempChannelDefaultDeleteDelay { get; set; }
	}

	[QueryNotification(NotificationType.TextMessage)]
	public interface TextMessage : INotification, IInvokedNotification
	{
		[QuerySerialized("targetmode")]
		MessageTarget Target { get; set; }

		[QuerySerialized("msg")]
		string Message { get; set; }

		[QuerySerialized("target")]
		int TargetClientId { get; set; }
	}

	[QueryNotification(NotificationType.TokenUsed)]
	public interface TokenUsed : INotification, IClientId
	{
		[QuerySerialized("cldbid")]
		ulong ClientDatabaseId { get; set; }

		[QuerySerialized("cluid")]
		string ClientUid { get; set; }

		[QuerySerialized("token")]
		string UsedToken { get; set; }

		[QuerySerialized("tokencustomset")]
		string TokenCustomSet { get; set; }

		[QuerySerialized("token1")]
		string Token1 { get; set; }

		[QuerySerialized("token2")]
		string Token2 { get; set; }
	}

	[QuerySubInterface]
	public interface ServerBaseData
	{
		[QuerySerialized("virtualserver_id")]
		int VirtualServerId { get; set; }

		[QuerySerialized("virtualserver_unique_identifier")]
		string VirtualServerUid { get; set; }

		[QuerySerialized("virtualserver_port")]
		ushort VirtualServerPort { get; set; }

		[QuerySerialized("virtualserver_status")]
		string VirtualServerStatus { get; set; }
	}
	
	public interface ServerData : IResponse, IServerName, ServerBaseData
	{
		[QuerySerialized("virtualserver_clientsonline")]
		int ClientsOnline { get; set; }

		[QuerySerialized("virtualserver_queryclientsonline")]
		int QueriesOnline { get; set; }

		[QuerySerialized("virtualserver_maxclients")]
		int MaxClients { get; set; }

		[QuerySerialized("virtualserver_uptime")]
		TimeSpan Uptime { get; set; }

		[QuerySerialized("virtualserver_autostart")]
		bool Autostart { get; set; }

		[QuerySerialized("virtualserver_machine_id")]
		string MachineId { get; set; }
	}
	
	public interface WhoAmI : IResponse, ServerBaseData, IClientUidLong
	{
		[QuerySerialized("client_id")]
		ushort ClientId { get; set; }

		[QuerySerialized("client_channel_id")]
		int ChannelId { get; set; }

		[QuerySerialized("client_nickname")]
		string NickName { get; set; }

		[QuerySerialized("client_database_id")]
		ulong DatabaseId { get; set; }

		[QuerySerialized("client_login_name")]
		string LoginName { get; set; }

		[QuerySerialized("client_origin_server_id")]
		int OriginServerId { get; set; }
	}
}
