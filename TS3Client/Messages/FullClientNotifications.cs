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

	[QueryNotification(NotificationType.ClientNeededPermissions)]
	public interface ClientNeededPermissions : INotification
	{
		[QuerySerialized("permid")]
		int PermissionId { get; set; }

		[QuerySerialized("permvalue")]
		int PermissionValue { get; set; }
	}

	[QueryNotification(NotificationType.ClientChannelGroupChanged)]
	public interface ClientChannelGroupChanged : INotification, IChannelId, IClientId
	{
		[QuerySerialized("invokerid")]
		ushort InvokerId { get; set; }

		[QuerySerialized("invokername")]
		string InvokerName { get; set; }

		[QuerySerialized("cgid")]
		ulong ChannelGroupId { get; set; }

		[QuerySerialized("cgi")]
		ulong ChannelGroupIndex { get; set; } // always same as ChannelId ??!?
	}

	[QueryNotification(NotificationType.ClientServerGroupAdded)]
	public interface ClientServerGroupAdded : INotification, IInvokedNotification, IClientId, IClientUid
	{
		[QuerySerialized("name")]
		string Name { get; set; }

		[QuerySerialized("sgid")]
		ulong ServerGroupId { get; set; }
	}

	[QueryNotification(NotificationType.ConnectionInfoRequest)]
	public interface ConnectionInfoRequest : INotification { }

	[QueryNotification(NotificationType.ChannelSubscribed)]
	public interface ChannelSubscribed : INotification, IChannelId { }

	[QueryNotification(NotificationType.ChannelUnsubscribed)]
	public interface ChannelUnsubscribed : INotification, IChannelId { }

	[QueryNotification(NotificationType.ClientChatComposing)]
	public interface ClientChatComposing : INotification, IClientId, IClientUid { }
}
