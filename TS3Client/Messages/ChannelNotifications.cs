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
	using System;

	[QuerySubInterface]
	public interface IChannelId
	{
		[QuerySerialized("cid")]
		ulong ChannelId { get; set; }
	}

	[QuerySubInterface]
	public interface IInvokedNotification
	{
		[QuerySerialized("invokerid")]
		ushort InvokerId { get; set; }

		[QuerySerialized("invokername")]
		string InvokerName { get; set; }

		[QuerySerialized("invokeruid")]
		string InvokerUid { get; set; }
	}

	[QuerySubInterface]
	public interface IChannelBaseData
	{
		[QuerySerialized("channel_order")]
		int Order { get; set; }

		[QuerySerialized("channel_name")]
		string Name { get; set; }

		[QuerySerialized("channel_topic")]
		string Topic { get; set; }

		[QuerySerialized("channel_flag_default")]
		bool IsDefaultChannel { get; set; }

		[QuerySerialized("channel_flag_password")]
		bool HasPassword { get; set; }

		[QuerySerialized("channel_flag_permanent")]
		bool IsPermanent { get; set; }

		[QuerySerialized("channel_flag_semi_permanent")]
		bool IsSemiPermanent { get; set; }

		[QuerySerialized("channel_codec")]
		Codec Codec { get; set; }

		[QuerySerialized("channel_codec_quality")]
		int CodecQuality { get; set; }

		[QuerySerialized("channel_needed_talk_power")]
		int NeededTalkPower { get; set; }

		[QuerySerialized("channel_icon_id")]
		long IconId { get; set; }

		[QuerySerialized("channel_maxclients")]
		int MaxClients { get; set; }

		[QuerySerialized("channel_maxfamilyclients")]
		int MaxFamilyClients { get; set; }
	}

	[QuerySubInterface]
	public interface IChannelNotificationData
	{
		[QuerySerialized("channel_codec_latency_factor")]
		int CodecLatencyFactor { get; set; }

		[QuerySerialized("channel_codec_is_unencrypted")]
		bool IsUnencrypted { get; set; }

		[QuerySerialized("channel_delete_delay")]
		TimeSpan DeleteDelay { get; set; }

		[QuerySerialized("channel_flag_maxclients_unlimited")]
		bool IsMaxClientsUnlimited { get; set; }

		[QuerySerialized("channel_flag_maxfamilyclients_unlimited")]
		bool IsMaxFamilyClientsUnlimited { get; set; }

		[QuerySerialized("channel_flag_maxfamilyclients_inherited")]
		bool IsMaxFamilyClientsInherited { get; set; }

		[QuerySerialized("channel_name_phonetic")]
		string PhoneticName { get; set; }
	}

	[QuerySubInterface]
	public interface IReason
	{
		[QuerySerialized("reasonid")]
		MoveReason Reason { get; set; }
	}

	[QuerySubInterface]
	public interface IChannelParentId
	{
		[QuerySerialized("cpid")]
		int ChannelParentId { get; set; }
	}

	[QueryNotification(NotificationType.ChannelCreated)]
	public interface ChannelCreated : INotification, IChannelId, IInvokedNotification, IChannelBaseData, IChannelNotificationData, IChannelParentId { }

	[QueryNotification(NotificationType.ChannelDeleted)]
	public interface ChannelDeleted : INotification, IChannelId, IInvokedNotification { }

	[QueryNotification(NotificationType.ChannelChanged)]
	public interface ChannelChanged : INotification, IChannelId { }

	[QueryNotification(NotificationType.ChannelEdited)]
	public interface ChannelEdited : INotification, IChannelId, IInvokedNotification, IChannelBaseData, IChannelNotificationData, IReason { }

	[QueryNotification(NotificationType.ChannelMoved)]
	public interface ChannelMoved : INotification, IChannelId, IInvokedNotification, IReason, IChannelParentId
	{
		[QuerySerialized("order")]
		int Order { get; set; }
	}

	[QueryNotification(NotificationType.ChannelPasswordChanged)]
	public interface ChannelPasswordChanged : INotification, IChannelId { }
	
	public interface ChannelData : IResponse, IChannelBaseData
	{
		[QuerySerialized("id")]
		int Id { get; set; }

		[QuerySerialized("pid")]
		int ParentChannelId { get; set; }

		[QuerySerialized("seconds_empty")]
		TimeSpan DurationEmpty { get; set; }

		[QuerySerialized("total_clients_family")]
		int TotalFamilyClients { get; set; }

		[QuerySerialized("total_clients")]
		int TotalClients { get; set; }

		[QuerySerialized("channel_needed_subscribe_power")]
		int NeededSubscribePower { get; set; }
	}
}
