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

	public enum NotificationType
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
