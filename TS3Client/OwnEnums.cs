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

namespace TS3Client
{
	using System;

	/*
	 * Most important Id datatypes:
	 *
	 * ClientUid: ulong
	 * ClientId: ushort
	 * ChannelId: ulong
	 * ServerGroupId: ulong
	 * ChannelGroupId: ulong
	*/

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
		[Ts3Serializable("textprivate")]
		Private = 1,
		[Ts3Serializable("textchannel")]
		Channel,
		[Ts3Serializable("textserver")]
		Server,
	}

	public enum RequestTarget
	{
		[Ts3Serializable("channel")]
		Channel = 4,
		[Ts3Serializable("server")]
		Server,
	}

	public enum NotificationType
	{
		Unknown,
		// Official notifies, used by client and query
		[Ts3Serializable("notifychannelcreated")]
		ChannelCreated,
		[Ts3Serializable("notifychanneldeleted")]
		ChannelDeleted,
		[Ts3Serializable("notifychannelchanged")]
		ChannelChanged,
		[Ts3Serializable("notifychanneledited")]
		ChannelEdited,
		[Ts3Serializable("notifychannelmoved")]
		ChannelMoved,
		[Ts3Serializable("notifychannelpasswordchanged")]
		ChannelPasswordChanged,
		[Ts3Serializable("notifycliententerview")]
		ClientEnterView,
		[Ts3Serializable("notifyclientleftview")]
		ClientLeftView,
		[Ts3Serializable("notifyclientmoved")]
		ClientMoved,
		[Ts3Serializable("notifyserveredited")]
		ServerEdited,
		[Ts3Serializable("notifytextmessage")]
		TextMessage,
		[Ts3Serializable("notifytokenused")]
		TokenUsed,

		// Internal notifies, used by client
		[Ts3Serializable("initivexpand")]
		InitIvExpand,
		[Ts3Serializable("initserver")]
		InitServer,
		[Ts3Serializable("channellist")]
		ChannelList,
		[Ts3Serializable("channellistfinished")]
		ChannelListFinished,
		[Ts3Serializable("notifyclientneededpermissions")]
		ClientNeededPermissions,
		[Ts3Serializable("notifyclientchannelgroupchanged")]
		ClientChannelGroupChanged,
		[Ts3Serializable("notifyservergroupclientadded")]
		ClientServerGroupAdded,
		[Ts3Serializable("notifyconnectioninforequest")]
		ConnectionInfoRequest,
		[Ts3Serializable("notifychannelsubscribed")]
		ChannelSubscribed,
		[Ts3Serializable("notifychannelunsubscribed")]
		ChannelUnsubscribed,
		[Ts3Serializable("notifyclientchatcomposing")]
		ClientChatComposing,
		// TODO: notifyservergroupsbyclientid
		// TODO: notifyclientchatclosed
		// TODO: notifyclientpoke
		// TODO: notifyclientupdated
		// TODO: notifyclientchannelgroupchanged
		// TODO: notifychannelpasswordchanged
		// TODO: notifychanneldescriptionchanged
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
