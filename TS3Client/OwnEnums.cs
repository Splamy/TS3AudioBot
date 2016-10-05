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
		[TS3Serializable("textprivate")]
		Private = 1,
		[TS3Serializable("textchannel")]
		Channel,
		[TS3Serializable("textserver")]
		Server,
	}

	public enum RequestTarget
	{
		[TS3Serializable("channel")]
		Channel = 4,
		[TS3Serializable("server")]
		Server,
	}

	public enum NotificationType
	{
		// Official notifies, used by client and query
		[TS3Serializable("notifychannelcreated")]
		ChannelCreated,
		[TS3Serializable("notifychanneldeleted")]
		ChannelDeleted,
		[TS3Serializable("notifychannelchanged")]
		ChannelChanged,
		[TS3Serializable("notifychanneledited")]
		ChannelEdited,
		[TS3Serializable("notifychannelmoved")]
		ChannelMoved,
		[TS3Serializable("notifychannelpasswordchanged")]
		ChannelPasswordChanged,
		[TS3Serializable("notifycliententerview")]
		ClientEnterView,
		[TS3Serializable("notifyclientleftview")]
		ClientLeftView,
		[TS3Serializable("notifyclientmoved")]
		ClientMoved,
		[TS3Serializable("notifyserveredited")]
		ServerEdited,
		[TS3Serializable("notifytextmessage")]
		TextMessage,
		[TS3Serializable("notifytokenused")]
		TokenUsed,

		// Internal notifies, used by client
		[TS3Serializable("initivexpand")]
		InitIvExpand,
		[TS3Serializable("initserver")]
		InitServer,
		[TS3Serializable("channellist")]
		ChannelList,
		[TS3Serializable("channellistfinished")]
		ChannelListFinished,
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
