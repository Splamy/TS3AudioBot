namespace TS3Client.Messages
{
	using System;

	[QueryNotification(NotificationType.InitIvExpand)]
	public interface InitIvExpand : INotification
	{
		[QuerySerialized("alpha")]
		string Alpha { get; set; }

		[QuerySerialized("beta")]
		string Beta { get; set; }

		[QuerySerialized("omega")]
		string Omega { get; set; }
	}

	[QueryNotification(NotificationType.InitServer)]
	public interface InitServer : INotification, IServerName, ServerBaseData2
	{
		[QuerySerialized("virtualserver_welcomemessage")]
		string WelcomeMessage { get; set; }

		[QuerySerialized("virtualserver_platform")]
		string ServerPlatform { get; set; }

		[QuerySerialized("virtualserver_version")]
		string ServerVersion { get; set; }

		[QuerySerialized("virtualserver_maxclients")]
		ushort MaxClients { get; set; }

		[QuerySerialized("virtualserver_created")]
		long ServerCreated { get; set; } // ?

		[QuerySerialized("virtualserver_hostmessage")]
		string Hostmessage { get; set; }

		[QuerySerialized("virtualserver_hostmessage_mode")]
		HostMessageMode HostmessageMode { get; set; }

		[QuerySerialized("virtualserver_id")]
		ulong ServerId { get; set; }

		[QuerySerialized("virtualserver_ip")]
		string ServerIp { get; set; }

		[QuerySerialized("virtualserver_ask_for_privilegekey")]
		bool AskForPrivilege { get; set; }

		[QuerySerialized("acn")]
		string ClientName { get; set; }

		[QuerySerialized("aclid")]
		ushort ClientId { get; set; }

		[QuerySerialized("pv")]
		int Pv { get; set; } // ?

		[QuerySerialized("lt")]
		int Lt { get; set; } // ?

		[QuerySerialized("client_talk_power")]
		int ClientTalkpower { get; set; }

		[QuerySerialized("client_needed_serverquery_view_power")]
		string NeededServerqueryViewpower { get; set; }
	}

	[QueryNotification(NotificationType.ChannelList)]
	public interface ChannelList : INotification, IChannelParentId, IChannelId, IChannelBaseData, IChannelNotificationData
	{
		[QuerySerialized("channel_forced_silence")]
		bool ForcedSilence { get; set; }

		[QuerySerialized("channel_flag_private")]
		bool IsPrivate { get; set; }
	}

	[QueryNotification(NotificationType.ChannelListFinished)]
	public interface ChannelListFinished : INotification { }
}