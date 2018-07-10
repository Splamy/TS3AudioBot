// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.















namespace TS3Client.Messages
{
	using Commands;
	using Helper;
	using System;
	using System.Globalization;

	#pragma warning disable CS8019 // Ignore unused imports
	using i8  = System.SByte;
	using u8  = System.Byte;
	using i16 = System.Int16;
	using u16 = System.UInt16;
	using i32 = System.Int32;
	using u32 = System.UInt32;
	using i64 = System.Int64;
	using u64 = System.UInt64;
	using f32 = System.Single;
	using d64 = System.Double;
	using str = System.String;

	using Duration = System.TimeSpan;
	using DurationSeconds = System.TimeSpan;
	using DurationMilliseconds = System.TimeSpan;
	using SocketAddr = System.Net.IPAddress;

	using Uid = System.String;
	using ClientDbId = System.UInt64;
	using ClientId = System.UInt16;
	using ChannelId = System.UInt64;
	using ServerGroupId = System.UInt64;
	using ChannelGroupId = System.UInt64;
	using IconHash = System.Int32;
	using ConnectionId = System.UInt32;
#pragma warning restore CS8019

	public sealed class ChannelChanged : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelChanged;
		

		public ChannelId ChannelId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ChannelCreated : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelCreated;
		

		public ChannelId ChannelId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }
		public i32 Order { get; set; }
		public str Name { get; set; }
		public str Topic { get; set; }
		public bool IsDefault { get; set; }
		public bool HasPassword { get; set; }
		public bool IsPermanent { get; set; }
		public bool IsSemiPermanent { get; set; }
		public Codec Codec { get; set; }
		public u8 CodecQuality { get; set; }
		public i32 NeededTalkPower { get; set; }
		public IconHash IconId { get; set; }
		public i32 MaxClients { get; set; }
		public i32 MaxFamilyClients { get; set; }
		public i32 CodecLatencyFactor { get; set; }
		public bool IsUnencrypted { get; set; }
		public DurationSeconds DeleteDelay { get; set; }
		public bool IsMaxClientsUnlimited { get; set; }
		public bool IsMaxFamilyClientsUnlimited { get; set; }
		public bool InheritsMaxFamilyClients { get; set; }
		public str PhoneticName { get; set; }
		public ChannelId ChannelParentId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "channel_order": Order = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_name": Name = Ts3String.Unescape(value); break;
			case "channel_topic": Topic = Ts3String.Unescape(value); break;
			case "channel_flag_default": IsDefault = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_password": HasPassword = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_permanent": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_semi_permanent": IsSemiPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_codec": Codec = (Codec)u8.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_quality": CodecQuality = u8.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_needed_talk_power": NeededTalkPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "channel_maxclients": MaxClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_maxfamilyclients": MaxFamilyClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_latency_factor": CodecLatencyFactor = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_is_unencrypted": IsUnencrypted = value.Length > 0 && value[0] != '0'; break;
			case "channel_delete_delay": DeleteDelay = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "channel_flag_maxclients_unlimited": IsMaxClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_unlimited": IsMaxFamilyClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_inherited": InheritsMaxFamilyClients = value.Length > 0 && value[0] != '0'; break;
			case "channel_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "cpid": ChannelParentId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ChannelData : IResponse
	{
		
		public string ReturnCode { get; set; }

		public ChannelId Id { get; set; }
		public ChannelId ParentChannelId { get; set; }
		public DurationSeconds DurationEmpty { get; set; }
		public i32 TotalFamilyClients { get; set; }
		public i32 TotalClients { get; set; }
		public i32 NeededSubscribePower { get; set; }
		public i32 Order { get; set; }
		public str Name { get; set; }
		public str Topic { get; set; }
		public bool IsDefault { get; set; }
		public bool HasPassword { get; set; }
		public bool IsPermanent { get; set; }
		public bool IsSemiPermanent { get; set; }
		public Codec Codec { get; set; }
		public u8 CodecQuality { get; set; }
		public i32 NeededTalkPower { get; set; }
		public IconHash IconId { get; set; }
		public i32 MaxClients { get; set; }
		public i32 MaxFamilyClients { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "id": Id = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "pid": ParentChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "seconds_empty": DurationEmpty = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "total_clients_family": TotalFamilyClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "total_clients": TotalClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_needed_subscribe_power": NeededSubscribePower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_order": Order = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_name": Name = Ts3String.Unescape(value); break;
			case "channel_topic": Topic = Ts3String.Unescape(value); break;
			case "channel_flag_default": IsDefault = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_password": HasPassword = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_permanent": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_semi_permanent": IsSemiPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_codec": Codec = (Codec)u8.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_quality": CodecQuality = u8.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_needed_talk_power": NeededTalkPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "channel_maxclients": MaxClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_maxfamilyclients": MaxFamilyClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ChannelDeleted : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelDeleted;
		

		public ChannelId ChannelId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class ChannelEdited : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelEdited;
		

		public ChannelId ChannelId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }
		public i32 Order { get; set; }
		public str Name { get; set; }
		public str Topic { get; set; }
		public bool IsDefault { get; set; }
		public bool HasPassword { get; set; }
		public bool IsPermanent { get; set; }
		public bool IsSemiPermanent { get; set; }
		public Codec Codec { get; set; }
		public u8 CodecQuality { get; set; }
		public i32 NeededTalkPower { get; set; }
		public IconHash IconId { get; set; }
		public i32 MaxClients { get; set; }
		public i32 MaxFamilyClients { get; set; }
		public i32 CodecLatencyFactor { get; set; }
		public bool IsUnencrypted { get; set; }
		public DurationSeconds DeleteDelay { get; set; }
		public bool IsMaxClientsUnlimited { get; set; }
		public bool IsMaxFamilyClientsUnlimited { get; set; }
		public bool InheritsMaxFamilyClients { get; set; }
		public str PhoneticName { get; set; }
		public Reason Reason { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "channel_order": Order = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_name": Name = Ts3String.Unescape(value); break;
			case "channel_topic": Topic = Ts3String.Unescape(value); break;
			case "channel_flag_default": IsDefault = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_password": HasPassword = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_permanent": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_semi_permanent": IsSemiPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_codec": Codec = (Codec)u8.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_quality": CodecQuality = u8.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_needed_talk_power": NeededTalkPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "channel_maxclients": MaxClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_maxfamilyclients": MaxFamilyClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_latency_factor": CodecLatencyFactor = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_is_unencrypted": IsUnencrypted = value.Length > 0 && value[0] != '0'; break;
			case "channel_delete_delay": DeleteDelay = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "channel_flag_maxclients_unlimited": IsMaxClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_unlimited": IsMaxFamilyClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_inherited": InheritsMaxFamilyClients = value.Length > 0 && value[0] != '0'; break;
			case "channel_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "reasonid": { if (!Enum.TryParse(value.NewString(), out Reason val)) throw new FormatException(); Reason = val; } break;
			
			}

		}
	}

	public sealed class ChannelGroupList : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelGroupList;
		

		public ChannelGroupId ChannelGroup { get; set; }
		public str Name { get; set; }
		public GroupType GroupType { get; set; }
		public IconHash IconId { get; set; }
		public bool IsPermanent { get; set; }
		public i32 SortId { get; set; }
		public GroupNamingMode NamingMode { get; set; }
		public i32 NeededModifyPower { get; set; }
		public i32 NeededMemberAddPower { get; set; }
		public i32 NeededMemberRemovePower { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cgid": ChannelGroup = ChannelGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "type": { if (!Enum.TryParse(value.NewString(), out GroupType val)) throw new FormatException(); GroupType = val; } break;
			case "iconid": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "savedb": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "sortid": SortId = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "namemode": { if (!Enum.TryParse(value.NewString(), out GroupNamingMode val)) throw new FormatException(); NamingMode = val; } break;
			case "n_modifyp": NeededModifyPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "n_member_addp": NeededMemberAddPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "n_member_remove_p": NeededMemberRemovePower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ChannelList : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelList;
		

		public ChannelId ChannelId { get; set; }
		public ChannelId ChannelParentId { get; set; }
		public str Name { get; set; }
		public str Topic { get; set; }
		public Codec Codec { get; set; }
		public u8 CodecQuality { get; set; }
		public i32 MaxClients { get; set; }
		public i32 MaxFamilyClients { get; set; }
		public i32 Order { get; set; }
		public bool IsPermanent { get; set; }
		public bool IsSemiPermanent { get; set; }
		public bool IsDefault { get; set; }
		public bool HasPassword { get; set; }
		public i32 CodecLatencyFactor { get; set; }
		public bool IsUnencrypted { get; set; }
		public DurationSeconds DeleteDelay { get; set; }
		public bool IsMaxClientsUnlimited { get; set; }
		public bool IsMaxFamilyClientsUnlimited { get; set; }
		public bool InheritsMaxFamilyClients { get; set; }
		public i32 NeededTalkPower { get; set; }
		public bool ForcedSilence { get; set; }
		public str PhoneticName { get; set; }
		public IconHash IconId { get; set; }
		public bool IsPrivate { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cpid": ChannelParentId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_name": Name = Ts3String.Unescape(value); break;
			case "channel_topic": Topic = Ts3String.Unescape(value); break;
			case "channel_codec": Codec = (Codec)u8.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_quality": CodecQuality = u8.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_maxclients": MaxClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_maxfamilyclients": MaxFamilyClients = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_order": Order = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_flag_permanent": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_semi_permanent": IsSemiPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_default": IsDefault = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_password": HasPassword = value.Length > 0 && value[0] != '0'; break;
			case "channel_codec_latency_factor": CodecLatencyFactor = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_codec_is_unencrypted": IsUnencrypted = value.Length > 0 && value[0] != '0'; break;
			case "channel_delete_delay": DeleteDelay = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "channel_flag_maxclients_unlimited": IsMaxClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_unlimited": IsMaxFamilyClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_inherited": InheritsMaxFamilyClients = value.Length > 0 && value[0] != '0'; break;
			case "channel_needed_talk_power": NeededTalkPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "channel_forced_silence": ForcedSilence = value.Length > 0 && value[0] != '0'; break;
			case "channel_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "channel_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "channel_flag_private": IsPrivate = value.Length > 0 && value[0] != '0'; break;
			
			}

		}
	}

	public sealed class ChannelListFinished : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelListFinished;
		


		public void SetField(string name, ReadOnlySpan<char> value)
		{

		}
	}

	public sealed class ChannelMoved : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelMoved;
		

		public i32 Order { get; set; }
		public ChannelId ChannelId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }
		public Reason Reason { get; set; }
		public ChannelId ChannelParentId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "order": Order = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "reasonid": { if (!Enum.TryParse(value.NewString(), out Reason val)) throw new FormatException(); Reason = val; } break;
			case "cpid": ChannelParentId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ChannelPasswordChanged : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelPasswordChanged;
		

		public ChannelId ChannelId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ChannelSubscribed : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelSubscribed;
		

		public ChannelId ChannelId { get; set; }
		public DurationSeconds EmptySince { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "es": EmptySince = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			
			}

		}
	}

	public sealed class ChannelUnsubscribed : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelUnsubscribed;
		

		public ChannelId ChannelId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ClientChannelGroupChanged : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientChannelGroupChanged;
		

		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public ChannelGroupId ChannelGroup { get; set; }
		public ChannelGroupId ChannelGroupIndex { get; set; }
		public ChannelId ChannelId { get; set; }
		public ClientId ClientId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "cgid": ChannelGroup = ChannelGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cgi": ChannelGroupIndex = ChannelGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ClientChatComposing : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientChatComposing;
		

		public ClientId ClientId { get; set; }
		public Uid ClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class ClientData : IResponse
	{
		
		public string ReturnCode { get; set; }

		public ClientId ClientId { get; set; }
		public Uid Uid { get; set; }
		public ChannelId ChannelId { get; set; }
		public ClientDbId DatabaseId { get; set; }
		public str Name { get; set; }
		public ClientType ClientType { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_database_id": DatabaseId = ClientDbId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_type": { if (!Enum.TryParse(value.NewString(), out ClientType val)) throw new FormatException(); ClientType = val; } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ClientDbData : IResponse
	{
		
		public string ReturnCode { get; set; }

		public str LastIp { get; set; }
		public ClientId ClientId { get; set; }
		public Uid Uid { get; set; }
		public ChannelId ChannelId { get; set; }
		public ClientDbId DatabaseId { get; set; }
		public str Name { get; set; }
		public ClientType ClientType { get; set; }
		public str AvatarHash { get; set; }
		public str Description { get; set; }
		public IconHash IconId { get; set; }
		public DateTime CreationDate { get; set; }
		public DateTime LastConnected { get; set; }
		public i32 TotalConnections { get; set; }
		public i64 MonthlyUploadQuota { get; set; }
		public i64 MonthlyDownloadQuota { get; set; }
		public i64 TotalUploadQuota { get; set; }
		public i64 TotalDownloadQuota { get; set; }
		public str Base64HashClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "client_lastip": LastIp = Ts3String.Unescape(value); break;
			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_database_id": DatabaseId = ClientDbId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_type": { if (!Enum.TryParse(value.NewString(), out ClientType val)) throw new FormatException(); ClientType = val; } break;
			case "client_flag_avatar": AvatarHash = Ts3String.Unescape(value); break;
			case "client_description": Description = Ts3String.Unescape(value); break;
			case "client_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_created": CreationDate = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_lastconnected": LastConnected = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_totalconnections": TotalConnections = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_month_bytes_uploaded": MonthlyUploadQuota = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_month_bytes_downloaded": MonthlyDownloadQuota = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_total_bytes_uploaded": TotalUploadQuota = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_total_bytes_downloaded": TotalDownloadQuota = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_base64HashClientUID": Base64HashClientUid = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ClientDbIdFromUid : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientDbIdFromUid;
		public string ReturnCode { get; set; }

		public Uid ClientUid { get; set; }
		public ClientDbId ClientDbId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			case "cldbid": ClientDbId = ClientDbId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ClientEnterView : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientEnterView;
		

		public Reason Reason { get; set; }
		public ChannelId TargetChannelId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }
		public ClientId ClientId { get; set; }
		public ClientDbId DatabaseId { get; set; }
		public str Name { get; set; }
		public ClientType ClientType { get; set; }
		public ChannelId SourceChannelId { get; set; }
		public Uid Uid { get; set; }
		public str AvatarHash { get; set; }
		public str Description { get; set; }
		public IconHash IconId { get; set; }
		public bool InputMuted { get; set; }
		public bool OutputMuted { get; set; }
		public bool OutputOnlyMuted { get; set; }
		public bool InputHardwareEnabled { get; set; }
		public bool OutputHardwareEnabled { get; set; }
		public str Metadata { get; set; }
		public bool IsRecording { get; set; }
		public ChannelGroupId ChannelGroup { get; set; }
		public ChannelId InheritedChannelGroupFromChannel { get; set; }
		public ServerGroupId[] ServerGroups { get; set; }
		public bool IsAway { get; set; }
		public str AwayMessage { get; set; }
		public i32 TalkPower { get; set; }
		public DateTime TalkPowerRequestTime { get; set; }
		public str TalkPowerRequestMessage { get; set; }
		public bool TalkPowerGranted { get; set; }
		public bool IsPrioritySpeaker { get; set; }
		public u32 UnreadMessages { get; set; }
		public str PhoneticName { get; set; }
		public i32 NeededServerqueryViewPower { get; set; }
		public bool IsChannelCommander { get; set; }
		public str CountryCode { get; set; }
		public str Badges { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "reasonid": { if (!Enum.TryParse(value.NewString(), out Reason val)) throw new FormatException(); Reason = val; } break;
			case "ctid": TargetChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_database_id": DatabaseId = ClientDbId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_type": { if (!Enum.TryParse(value.NewString(), out ClientType val)) throw new FormatException(); ClientType = val; } break;
			case "cfid": SourceChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "client_flag_avatar": AvatarHash = Ts3String.Unescape(value); break;
			case "client_description": Description = Ts3String.Unescape(value); break;
			case "client_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_input_muted": InputMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_output_muted": OutputMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_outputonly_muted": OutputOnlyMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_input_hardware": InputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_output_hardware": OutputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_meta_data": Metadata = Ts3String.Unescape(value); break;
			case "client_is_recording": IsRecording = value.Length > 0 && value[0] != '0'; break;
			case "client_channel_group_id": ChannelGroup = ChannelGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_channel_group_inherited_channel_id": InheritedChannelGroupFromChannel = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_servergroups": { if(value.Length == 0) ServerGroups = Array.Empty<ServerGroupId>(); else { var ss = new SpanSplitter(); ss.First(value, ','); int cnt = 0; for (int i = 0; i < value.Length; i++) if (value[i] == ',') cnt++; ServerGroups = new ServerGroupId[cnt + 1]; for(int i = 0; i < cnt + 1; i++) { ServerGroups[i] = ServerGroupId.Parse(ss.Trim(value).NewString(), CultureInfo.InvariantCulture); if (i < cnt) value = ss.Next(value); } } } break;
			case "client_away": IsAway = value.Length > 0 && value[0] != '0'; break;
			case "client_away_message": AwayMessage = Ts3String.Unescape(value); break;
			case "client_talk_power": TalkPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_talk_request": TalkPowerRequestTime = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_talk_request_msg": TalkPowerRequestMessage = Ts3String.Unescape(value); break;
			case "client_is_talker": TalkPowerGranted = value.Length > 0 && value[0] != '0'; break;
			case "client_is_priority_speaker": IsPrioritySpeaker = value.Length > 0 && value[0] != '0'; break;
			case "client_unread_messages": UnreadMessages = u32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_nickname_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "client_needed_serverquery_view_power": NeededServerqueryViewPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_is_channel_commander": IsChannelCommander = value.Length > 0 && value[0] != '0'; break;
			case "client_country": CountryCode = Ts3String.Unescape(value); break;
			case "client_badges": Badges = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class ClientIds : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientIds;
		public string ReturnCode { get; set; }

		public Uid ClientUid { get; set; }
		public ClientId ClientId { get; set; }
		public str Name { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ClientInfo : IResponse
	{
		
		public string ReturnCode { get; set; }

		public DurationMilliseconds ClientIdleTime { get; set; }
		public str ClientVersion { get; set; }
		public str ClientVersionSign { get; set; }
		public str ClientPlattform { get; set; }
		public str DefaultChannel { get; set; }
		public str SecurityHash { get; set; }
		public str LoginName { get; set; }
		public str DefaultToken { get; set; }
		public u64 FiletransferBandwidthSent { get; set; }
		public u64 FiletransferBandwidthReceived { get; set; }
		public u64 PacketsSentTotal { get; set; }
		public u64 PacketsReceivedTotal { get; set; }
		public u64 BytesSentTotal { get; set; }
		public u64 BytesReceivedTotal { get; set; }
		public u64 BandwidthSentLastSecondTotal { get; set; }
		public u64 BandwidthReceivedLastSecondTotal { get; set; }
		public u64 BandwidthSentLastMinuteTotal { get; set; }
		public u64 BandwidthReceivedLastMinuteTotal { get; set; }
		public DurationMilliseconds ConnectedTime { get; set; }
		public str Ip { get; set; }
		public ChannelId ChannelId { get; set; }
		public Uid Uid { get; set; }
		public ClientDbId DatabaseId { get; set; }
		public str Name { get; set; }
		public ClientType ClientType { get; set; }
		public bool InputMuted { get; set; }
		public bool OutputMuted { get; set; }
		public bool OutputOnlyMuted { get; set; }
		public bool InputHardwareEnabled { get; set; }
		public bool OutputHardwareEnabled { get; set; }
		public str Metadata { get; set; }
		public bool IsRecording { get; set; }
		public ChannelGroupId ChannelGroup { get; set; }
		public ChannelId InheritedChannelGroupFromChannel { get; set; }
		public ServerGroupId[] ServerGroups { get; set; }
		public bool IsAway { get; set; }
		public str AwayMessage { get; set; }
		public i32 TalkPower { get; set; }
		public DateTime TalkPowerRequestTime { get; set; }
		public str TalkPowerRequestMessage { get; set; }
		public bool TalkPowerGranted { get; set; }
		public bool IsPrioritySpeaker { get; set; }
		public u32 UnreadMessages { get; set; }
		public str PhoneticName { get; set; }
		public i32 NeededServerqueryViewPower { get; set; }
		public bool IsChannelCommander { get; set; }
		public str CountryCode { get; set; }
		public str Badges { get; set; }
		public DateTime CreationDate { get; set; }
		public DateTime LastConnected { get; set; }
		public i32 TotalConnections { get; set; }
		public i64 MonthlyUploadQuota { get; set; }
		public i64 MonthlyDownloadQuota { get; set; }
		public i64 TotalUploadQuota { get; set; }
		public i64 TotalDownloadQuota { get; set; }
		public str Base64HashClientUid { get; set; }
		public str AvatarHash { get; set; }
		public str Description { get; set; }
		public IconHash IconId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "client_idle_time": ClientIdleTime = TimeSpan.FromMilliseconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_version": ClientVersion = Ts3String.Unescape(value); break;
			case "client_version_sign": ClientVersionSign = Ts3String.Unescape(value); break;
			case "client_platform": ClientPlattform = Ts3String.Unescape(value); break;
			case "client_default_channel": DefaultChannel = Ts3String.Unescape(value); break;
			case "client_security_hash": SecurityHash = Ts3String.Unescape(value); break;
			case "client_login_name": LoginName = Ts3String.Unescape(value); break;
			case "client_default_token": DefaultToken = Ts3String.Unescape(value); break;
			case "connection_filetransfer_bandwidth_sent": FiletransferBandwidthSent = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_filetransfer_bandwidth_received": FiletransferBandwidthReceived = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_packets_sent_total": PacketsSentTotal = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_packets_received_total": PacketsReceivedTotal = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bytes_sent_total": BytesSentTotal = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bytes_received_total": BytesReceivedTotal = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_sent_last_second_total": BandwidthSentLastSecondTotal = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_received_last_second_total": BandwidthReceivedLastSecondTotal = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_sent_last_minute_total": BandwidthSentLastMinuteTotal = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_received_last_minute_total": BandwidthReceivedLastMinuteTotal = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_connected_time": ConnectedTime = TimeSpan.FromMilliseconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "connection_client_ip": Ip = Ts3String.Unescape(value); break;
			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "client_database_id": DatabaseId = ClientDbId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_type": { if (!Enum.TryParse(value.NewString(), out ClientType val)) throw new FormatException(); ClientType = val; } break;
			case "client_input_muted": InputMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_output_muted": OutputMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_outputonly_muted": OutputOnlyMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_input_hardware": InputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_output_hardware": OutputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_meta_data": Metadata = Ts3String.Unescape(value); break;
			case "client_is_recording": IsRecording = value.Length > 0 && value[0] != '0'; break;
			case "client_channel_group_id": ChannelGroup = ChannelGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_channel_group_inherited_channel_id": InheritedChannelGroupFromChannel = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_servergroups": { if(value.Length == 0) ServerGroups = Array.Empty<ServerGroupId>(); else { var ss = new SpanSplitter(); ss.First(value, ','); int cnt = 0; for (int i = 0; i < value.Length; i++) if (value[i] == ',') cnt++; ServerGroups = new ServerGroupId[cnt + 1]; for(int i = 0; i < cnt + 1; i++) { ServerGroups[i] = ServerGroupId.Parse(ss.Trim(value).NewString(), CultureInfo.InvariantCulture); if (i < cnt) value = ss.Next(value); } } } break;
			case "client_away": IsAway = value.Length > 0 && value[0] != '0'; break;
			case "client_away_message": AwayMessage = Ts3String.Unescape(value); break;
			case "client_talk_power": TalkPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_talk_request": TalkPowerRequestTime = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_talk_request_msg": TalkPowerRequestMessage = Ts3String.Unescape(value); break;
			case "client_is_talker": TalkPowerGranted = value.Length > 0 && value[0] != '0'; break;
			case "client_is_priority_speaker": IsPrioritySpeaker = value.Length > 0 && value[0] != '0'; break;
			case "client_unread_messages": UnreadMessages = u32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_nickname_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "client_needed_serverquery_view_power": NeededServerqueryViewPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_is_channel_commander": IsChannelCommander = value.Length > 0 && value[0] != '0'; break;
			case "client_country": CountryCode = Ts3String.Unescape(value); break;
			case "client_badges": Badges = Ts3String.Unescape(value); break;
			case "client_created": CreationDate = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_lastconnected": LastConnected = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "client_totalconnections": TotalConnections = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_month_bytes_uploaded": MonthlyUploadQuota = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_month_bytes_downloaded": MonthlyDownloadQuota = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_total_bytes_uploaded": TotalUploadQuota = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_total_bytes_downloaded": TotalDownloadQuota = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_base64HashClientUID": Base64HashClientUid = Ts3String.Unescape(value); break;
			case "client_flag_avatar": AvatarHash = Ts3String.Unescape(value); break;
			case "client_description": Description = Ts3String.Unescape(value); break;
			case "client_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ClientInit : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientInit;
		

		public str Name { get; set; }
		public str ClientVersion { get; set; }
		public str ClientPlattform { get; set; }
		public bool InputHardwareEnabled { get; set; }
		public bool OutputHardwareEnabled { get; set; }
		public str DefaultChannel { get; set; }
		public str DefaultChannelPassword { get; set; }
		public str ServerPassword { get; set; }
		public str Metadata { get; set; }
		public str ClientVersionSign { get; set; }
		public u64 ClientKeyOffset { get; set; }
		public str PhoneticName { get; set; }
		public str DefaultToken { get; set; }
		public str HardwareId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_version": ClientVersion = Ts3String.Unescape(value); break;
			case "client_platform": ClientPlattform = Ts3String.Unescape(value); break;
			case "client_input_hardware": InputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_output_hardware": OutputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_default_channel": DefaultChannel = Ts3String.Unescape(value); break;
			case "client_default_channel_password": DefaultChannelPassword = Ts3String.Unescape(value); break;
			case "client_server_password": ServerPassword = Ts3String.Unescape(value); break;
			case "client_meta_data": Metadata = Ts3String.Unescape(value); break;
			case "client_version_sign": ClientVersionSign = Ts3String.Unescape(value); break;
			case "client_key_offset": ClientKeyOffset = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_nickname_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "client_default_token": DefaultToken = Ts3String.Unescape(value); break;
			case "hwid": HardwareId = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class ClientInitIv : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientInitIv;
		

		public str Alpha { get; set; }
		public str Omega { get; set; }
		public str Ip { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "alpha": Alpha = Ts3String.Unescape(value); break;
			case "omega": Omega = Ts3String.Unescape(value); break;
			case "ip": Ip = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class ClientLeftView : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientLeftView;
		

		public str ReasonMessage { get; set; }
		public DurationSeconds BanTime { get; set; }
		public Reason Reason { get; set; }
		public ChannelId TargetChannelId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }
		public ClientId ClientId { get; set; }
		public ChannelId SourceChannelId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "reasonmsg": ReasonMessage = Ts3String.Unescape(value); break;
			case "bantime": BanTime = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "reasonid": { if (!Enum.TryParse(value.NewString(), out Reason val)) throw new FormatException(); Reason = val; } break;
			case "ctid": TargetChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cfid": SourceChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ClientMoved : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientMoved;
		

		public ClientId ClientId { get; set; }
		public Reason Reason { get; set; }
		public ChannelId TargetChannelId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "reasonid": { if (!Enum.TryParse(value.NewString(), out Reason val)) throw new FormatException(); Reason = val; } break;
			case "ctid": TargetChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class ClientNeededPermissions : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientNeededPermissions;
		

		public PermissionId PermissionId { get; set; }
		public i32 PermissionValue { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "permid": PermissionId = (PermissionId)i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "permvalue": PermissionValue = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ClientPoke : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientPoke;
		

		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }
		public str Message { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "msg": Message = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class ClientServerGroup : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientServerGroup;
		public string ReturnCode { get; set; }

		public str Name { get; set; }
		public ServerGroupId ServerGroupId { get; set; }
		public ClientDbId ClientDbId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "name": Name = Ts3String.Unescape(value); break;
			case "sgid": ServerGroupId = ServerGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cldbid": ClientDbId = ClientDbId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ClientServerGroupAdded : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientServerGroupAdded;
		

		public str Name { get; set; }
		public ServerGroupId ServerGroupId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }
		public ClientId ClientId { get; set; }
		public Uid ClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "name": Name = Ts3String.Unescape(value); break;
			case "sgid": ServerGroupId = ServerGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class CommandError : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.CommandError;
		

		public Ts3ErrorCode Id { get; set; }
		public str Message { get; set; }
		public PermissionId MissingPermissionId { get; set; }
		public str ReturnCode { get; set; }
		public str ExtraMessage { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "id": Id = (Ts3ErrorCode)u32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "failed_permid": MissingPermissionId = (PermissionId)i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			case "extra_msg": ExtraMessage = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class ConnectionInfo : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ConnectionInfo;
		

		public ClientId ClientId { get; set; }
		public DurationMilliseconds Ping { get; set; }
		public DurationMilliseconds PingDeviation { get; set; }
		public DurationMilliseconds ConnectedTime { get; set; }
		public str Ip { get; set; }
		public u16 Port { get; set; }
		public u64 PacketsSentSpeech { get; set; }
		public u64 PacketsSentKeepalive { get; set; }
		public u64 PacketsSentControl { get; set; }
		public u64 BytesSentSpeech { get; set; }
		public u64 BytesSentKeepalive { get; set; }
		public u64 BytesSentControl { get; set; }
		public u64 PacketsReceivedSpeech { get; set; }
		public u64 PacketsReceivedKeepalive { get; set; }
		public u64 PacketsReceivedControl { get; set; }
		public u64 BytesReceivedSpeech { get; set; }
		public u64 BytesReceivedKeepalive { get; set; }
		public u64 BytesReceivedControl { get; set; }
		public f32 ServerToClientPacketlossSpeech { get; set; }
		public f32 ServerToClientPacketlossKeepalive { get; set; }
		public f32 ServerToClientPacketlossControl { get; set; }
		public f32 ServerToClientPacketlossTotal { get; set; }
		public f32 ClientToServerPacketlossSpeech { get; set; }
		public f32 ClientToServerPacketlossKeepalive { get; set; }
		public f32 ClientToServerPacketlossControl { get; set; }
		public f32 ClientToServerPacketlossTotal { get; set; }
		public u64 BandwidthSentLastSecondSpeech { get; set; }
		public u64 BandwidthSentLastSecondKeepalive { get; set; }
		public u64 BandwidthSentLastSecondControl { get; set; }
		public u64 BandwidthSentLastMinuteSpeech { get; set; }
		public u64 BandwidthSentLastMinuteKeepalive { get; set; }
		public u64 BandwidthSentLastMinuteControl { get; set; }
		public u64 BandwidthReceivedLastSecondSpeech { get; set; }
		public u64 BandwidthReceivedLastSecondKeepalive { get; set; }
		public u64 BandwidthReceivedLastSecondControl { get; set; }
		public u64 BandwidthReceivedLastMinuteSpeech { get; set; }
		public u64 BandwidthReceivedLastMinuteKeepalive { get; set; }
		public u64 BandwidthReceivedLastMinuteControl { get; set; }
		public u64 FiletransferBandwidthSent { get; set; }
		public u64 FiletransferBandwidthReceived { get; set; }
		public DurationMilliseconds IdleTime { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_ping": Ping = TimeSpan.FromMilliseconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "connection_ping_deviation": PingDeviation = TimeSpan.FromMilliseconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "connection_connected_time": ConnectedTime = TimeSpan.FromMilliseconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "connection_client_ip": Ip = Ts3String.Unescape(value); break;
			case "connection_client_port": Port = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_packets_sent_speech": PacketsSentSpeech = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_packets_sent_keepalive": PacketsSentKeepalive = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_packets_sent_control": PacketsSentControl = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bytes_sent_speech": BytesSentSpeech = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bytes_sent_keepalive": BytesSentKeepalive = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bytes_sent_control": BytesSentControl = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_packets_received_speech": PacketsReceivedSpeech = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_packets_received_keepalive": PacketsReceivedKeepalive = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_packets_received_control": PacketsReceivedControl = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bytes_received_speech": BytesReceivedSpeech = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bytes_received_keepalive": BytesReceivedKeepalive = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bytes_received_control": BytesReceivedControl = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_server2client_packetloss_speech": ServerToClientPacketlossSpeech = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_server2client_packetloss_keepalive": ServerToClientPacketlossKeepalive = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_server2client_packetloss_control": ServerToClientPacketlossControl = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_server2client_packetloss_total": ServerToClientPacketlossTotal = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_client2server_packetloss_speech": ClientToServerPacketlossSpeech = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_client2server_packetloss_keepalive": ClientToServerPacketlossKeepalive = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_client2server_packetloss_control": ClientToServerPacketlossControl = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_client2server_packetloss_total": ClientToServerPacketlossTotal = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_sent_last_second_speech": BandwidthSentLastSecondSpeech = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_sent_last_second_keepalive": BandwidthSentLastSecondKeepalive = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_sent_last_second_control": BandwidthSentLastSecondControl = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_sent_last_minute_speech": BandwidthSentLastMinuteSpeech = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_sent_last_minute_keepalive": BandwidthSentLastMinuteKeepalive = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_sent_last_minute_control": BandwidthSentLastMinuteControl = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_received_last_second_speech": BandwidthReceivedLastSecondSpeech = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_received_last_second_keepalive": BandwidthReceivedLastSecondKeepalive = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_received_last_second_control": BandwidthReceivedLastSecondControl = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_received_last_minute_speech": BandwidthReceivedLastMinuteSpeech = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_received_last_minute_keepalive": BandwidthReceivedLastMinuteKeepalive = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_bandwidth_received_last_minute_control": BandwidthReceivedLastMinuteControl = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_filetransfer_bandwidth_sent": FiletransferBandwidthSent = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_filetransfer_bandwidth_received": FiletransferBandwidthReceived = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "connection_idle_time": IdleTime = TimeSpan.FromMilliseconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			
			}

		}
	}

	public sealed class ConnectionInfoRequest : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ConnectionInfoRequest;
		


		public void SetField(string name, ReadOnlySpan<char> value)
		{

		}
	}

	public sealed class FileDownload : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.FileDownload;
		public string ReturnCode { get; set; }

		public u16 ClientFileTransferId { get; set; }
		public u16 ServerFileTransferId { get; set; }
		public str FileTransferKey { get; set; }
		public u16 Port { get; set; }
		public i64 Size { get; set; }
		public str Message { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "clientftfid": ClientFileTransferId = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "serverftfid": ServerFileTransferId = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "ftkey": FileTransferKey = Ts3String.Unescape(value); break;
			case "port": Port = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "size": Size = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class FileInfoTs : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.FileInfoTs;
		public string ReturnCode { get; set; }

		public ChannelId ChannelId { get; set; }
		public str Path { get; set; }
		public str Name { get; set; }
		public i64 Size { get; set; }
		public DateTime DateTime { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "path": Path = Ts3String.Unescape(value); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "size": Size = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "datetime": DateTime = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class FileList : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.FileList;
		public string ReturnCode { get; set; }

		public ChannelId ChannelId { get; set; }
		public str Path { get; set; }
		public str Name { get; set; }
		public i64 Size { get; set; }
		public DateTime DateTime { get; set; }
		public bool IsFile { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "path": Path = Ts3String.Unescape(value); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "size": Size = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "datetime": DateTime = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "type": IsFile = value.Length > 0 && value[0] != '0'; break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class FileListFinished : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.FileListFinished;
		

		public ChannelId ChannelId { get; set; }
		public str Path { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cid": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "path": Path = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class FileTransfer : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.FileTransfer;
		public string ReturnCode { get; set; }

		public ClientId ClientId { get; set; }
		public str Path { get; set; }
		public str Name { get; set; }
		public i64 Size { get; set; }
		public i64 SizeDone { get; set; }
		public u16 ClientFileTransferId { get; set; }
		public u16 ServerFileTransferId { get; set; }
		public u64 Sender { get; set; }
		public i32 Status { get; set; }
		public f32 CurrentSpeed { get; set; }
		public f32 AverageSpeed { get; set; }
		public DurationSeconds Runtime { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "path": Path = Ts3String.Unescape(value); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "size": Size = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "sizedone": SizeDone = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "clientftfid": ClientFileTransferId = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "serverftfid": ServerFileTransferId = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "sender": Sender = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "status": Status = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "current_speed": CurrentSpeed = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "average_speed": AverageSpeed = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "runtime": Runtime = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class FileTransferStatus : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.FileTransferStatus;
		

		public u16 ClientFileTransferId { get; set; }
		public Ts3ErrorCode Status { get; set; }
		public str Message { get; set; }
		public i64 Size { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "clientftfid": ClientFileTransferId = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "status": Status = (Ts3ErrorCode)u32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "size": Size = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class FileUpload : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.FileUpload;
		public string ReturnCode { get; set; }

		public u16 ClientFileTransferId { get; set; }
		public u16 ServerFileTransferId { get; set; }
		public str FileTransferKey { get; set; }
		public u16 Port { get; set; }
		public i64 SeekPosistion { get; set; }
		public str Message { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "clientftfid": ClientFileTransferId = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "serverftfid": ServerFileTransferId = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "ftkey": FileTransferKey = Ts3String.Unescape(value); break;
			case "port": Port = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "seekpos": SeekPosistion = i64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class GetClientDbIdFromUid : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.GetClientDbIdFromUid;
		

		public Uid ClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class GetClientIds : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.GetClientIds;
		

		public Uid ClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class InitIvExpand : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.InitIvExpand;
		

		public str Alpha { get; set; }
		public str Beta { get; set; }
		public str Omega { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "alpha": Alpha = Ts3String.Unescape(value); break;
			case "beta": Beta = Ts3String.Unescape(value); break;
			case "omega": Omega = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class InitIvExpand2 : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.InitIvExpand2;
		

		public str License { get; set; }
		public str Beta { get; set; }
		public str Omega { get; set; }
		public bool Ot { get; set; }
		public str Proof { get; set; }
		public str Tvd { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "l": License = Ts3String.Unescape(value); break;
			case "beta": Beta = Ts3String.Unescape(value); break;
			case "omega": Omega = Ts3String.Unescape(value); break;
			case "ot": Ot = value.Length > 0 && value[0] != '0'; break;
			case "proof": Proof = Ts3String.Unescape(value); break;
			case "tvd": Tvd = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class InitServer : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.InitServer;
		

		public str WelcomeMessage { get; set; }
		public str ServerPlatform { get; set; }
		public str ServerVersion { get; set; }
		public u16 MaxClients { get; set; }
		public DateTime ServerCreated { get; set; }
		public str Hostmessage { get; set; }
		public HostMessageMode HostmessageMode { get; set; }
		public u64 VirtualServerId { get; set; }
		public str[] ServerIp { get; set; }
		public bool AskForPrivilegekey { get; set; }
		public str ClientName { get; set; }
		public ClientId ClientId { get; set; }
		public u16 ProtocolVersion { get; set; }
		public LicenseType LicenseType { get; set; }
		public i32 TalkPower { get; set; }
		public i32 NeededServerqueryViewPower { get; set; }
		public str Name { get; set; }
		public CodecEncryptionMode CodecEncryptionMode { get; set; }
		public ServerGroupId DefaultServerGroup { get; set; }
		public ChannelGroupId DefaultChannelGroup { get; set; }
		public str HostbannerUrl { get; set; }
		public str HostbannerGfxUrl { get; set; }
		public DurationSeconds HostbannerGfxInterval { get; set; }
		public f32 PrioritySpeakerDimmModificator { get; set; }
		public str HostbuttonTooltip { get; set; }
		public str HostbuttonUrl { get; set; }
		public str HostbuttonGfxUrl { get; set; }
		public str PhoneticName { get; set; }
		public IconHash IconId { get; set; }
		public HostBannerMode HostbannerMode { get; set; }
		public DurationSeconds TempChannelDefaultDeleteDelay { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "virtualserver_welcomemessage": WelcomeMessage = Ts3String.Unescape(value); break;
			case "virtualserver_platform": ServerPlatform = Ts3String.Unescape(value); break;
			case "virtualserver_version": ServerVersion = Ts3String.Unescape(value); break;
			case "virtualserver_maxclients": MaxClients = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_created": ServerCreated = Util.UnixTimeStart.AddSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "virtualserver_hostmessage": Hostmessage = Ts3String.Unescape(value); break;
			case "virtualserver_hostmessage_mode": { if (!Enum.TryParse(value.NewString(), out HostMessageMode val)) throw new FormatException(); HostmessageMode = val; } break;
			case "virtualserver_id": VirtualServerId = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_ip": { if(value.Length == 0) ServerIp = Array.Empty<str>(); else { var ss = new SpanSplitter(); ss.First(value, ','); int cnt = 0; for (int i = 0; i < value.Length; i++) if (value[i] == ',') cnt++; ServerIp = new str[cnt + 1]; for(int i = 0; i < cnt + 1; i++) { ServerIp[i] = Ts3String.Unescape(ss.Trim(value)); if (i < cnt) value = ss.Next(value); } } } break;
			case "virtualserver_ask_for_privilegekey": AskForPrivilegekey = value.Length > 0 && value[0] != '0'; break;
			case "acn": ClientName = Ts3String.Unescape(value); break;
			case "aclid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "pv": ProtocolVersion = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "lt": LicenseType = (LicenseType)u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_talk_power": TalkPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_needed_serverquery_view_power": NeededServerqueryViewPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_name": Name = Ts3String.Unescape(value); break;
			case "virtualserver_codec_encryption_mode": { if (!Enum.TryParse(value.NewString(), out CodecEncryptionMode val)) throw new FormatException(); CodecEncryptionMode = val; } break;
			case "virtualserver_default_server_group": DefaultServerGroup = ServerGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_default_channel_group": DefaultChannelGroup = ChannelGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_hostbanner_url": HostbannerUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbanner_gfx_url": HostbannerGfxUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbanner_gfx_interval": HostbannerGfxInterval = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "virtualserver_priority_speaker_dimm_modificator": PrioritySpeakerDimmModificator = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_hostbutton_tooltip": HostbuttonTooltip = Ts3String.Unescape(value); break;
			case "virtualserver_hostbutton_url": HostbuttonUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbutton_gfx_url": HostbuttonGfxUrl = Ts3String.Unescape(value); break;
			case "virtualserver_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "virtualserver_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "virtualserver_hostbanner_mode": { if (!Enum.TryParse(value.NewString(), out HostBannerMode val)) throw new FormatException(); HostbannerMode = val; } break;
			case "virtualserver_channel_temp_delete_delay_default": TempChannelDefaultDeleteDelay = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			
			}

		}
	}

	public sealed class PluginCommand : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.PluginCommand;
		

		public str Name { get; set; }
		public str Data { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "name": Name = Ts3String.Unescape(value); break;
			case "data": Data = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class PluginCommandRequest : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.PluginCommandRequest;
		

		public str Name { get; set; }
		public str Data { get; set; }
		public i32 Target { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "name": Name = Ts3String.Unescape(value); break;
			case "data": Data = Ts3String.Unescape(value); break;
			case "targetmode": Target = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class ServerData : IResponse
	{
		
		public string ReturnCode { get; set; }

		public i32 ClientsOnline { get; set; }
		public i32 QueriesOnline { get; set; }
		public u16 MaxClients { get; set; }
		public DurationSeconds Uptime { get; set; }
		public bool Autostart { get; set; }
		public str MachineId { get; set; }
		public str Name { get; set; }
		public u64 VirtualServerId { get; set; }
		public Uid VirtualServerUid { get; set; }
		public u16 VirtualServerPort { get; set; }
		public str VirtualServerStatus { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "virtualserver_clientsonline": ClientsOnline = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_queryclientsonline": QueriesOnline = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_maxclients": MaxClients = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_uptime": Uptime = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "virtualserver_autostart": Autostart = value.Length > 0 && value[0] != '0'; break;
			case "virtualserver_machine_id": MachineId = Ts3String.Unescape(value); break;
			case "virtualserver_name": Name = Ts3String.Unescape(value); break;
			case "virtualserver_id": VirtualServerId = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_unique_identifier": VirtualServerUid = Ts3String.Unescape(value); break;
			case "virtualserver_port": VirtualServerPort = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_status": VirtualServerStatus = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ServerEdited : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ServerEdited;
		

		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }
		public Reason Reason { get; set; }
		public str Name { get; set; }
		public CodecEncryptionMode CodecEncryptionMode { get; set; }
		public ServerGroupId DefaultServerGroup { get; set; }
		public ChannelGroupId DefaultChannelGroup { get; set; }
		public str HostbannerUrl { get; set; }
		public str HostbannerGfxUrl { get; set; }
		public DurationSeconds HostbannerGfxInterval { get; set; }
		public f32 PrioritySpeakerDimmModificator { get; set; }
		public str HostbuttonTooltip { get; set; }
		public str HostbuttonUrl { get; set; }
		public str HostbuttonGfxUrl { get; set; }
		public str PhoneticName { get; set; }
		public IconHash IconId { get; set; }
		public HostBannerMode HostbannerMode { get; set; }
		public DurationSeconds TempChannelDefaultDeleteDelay { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "reasonid": { if (!Enum.TryParse(value.NewString(), out Reason val)) throw new FormatException(); Reason = val; } break;
			case "virtualserver_name": Name = Ts3String.Unescape(value); break;
			case "virtualserver_codec_encryption_mode": { if (!Enum.TryParse(value.NewString(), out CodecEncryptionMode val)) throw new FormatException(); CodecEncryptionMode = val; } break;
			case "virtualserver_default_server_group": DefaultServerGroup = ServerGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_default_channel_group": DefaultChannelGroup = ChannelGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_hostbanner_url": HostbannerUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbanner_gfx_url": HostbannerGfxUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbanner_gfx_interval": HostbannerGfxInterval = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "virtualserver_priority_speaker_dimm_modificator": PrioritySpeakerDimmModificator = f32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_hostbutton_tooltip": HostbuttonTooltip = Ts3String.Unescape(value); break;
			case "virtualserver_hostbutton_url": HostbuttonUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbutton_gfx_url": HostbuttonGfxUrl = Ts3String.Unescape(value); break;
			case "virtualserver_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "virtualserver_icon_id": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "virtualserver_hostbanner_mode": { if (!Enum.TryParse(value.NewString(), out HostBannerMode val)) throw new FormatException(); HostbannerMode = val; } break;
			case "virtualserver_channel_temp_delete_delay_default": TempChannelDefaultDeleteDelay = TimeSpan.FromSeconds(double.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			
			}

		}
	}

	public sealed class ServerGroupAddResponse : IResponse
	{
		
		public string ReturnCode { get; set; }

		public ServerGroupId ServerGroupId { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "sgid": ServerGroupId = ServerGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public sealed class ServerGroupList : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ServerGroupList;
		

		public ServerGroupId ServerGroupId { get; set; }
		public str Name { get; set; }
		public GroupType GroupType { get; set; }
		public IconHash IconId { get; set; }
		public bool IsPermanent { get; set; }
		public i32 SortId { get; set; }
		public GroupNamingMode NamingMode { get; set; }
		public i32 NeededModifyPower { get; set; }
		public i32 NeededMemberAddPower { get; set; }
		public i32 NeededMemberRemovePower { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "sgid": ServerGroupId = ServerGroupId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "type": { if (!Enum.TryParse(value.NewString(), out GroupType val)) throw new FormatException(); GroupType = val; } break;
			case "iconid": IconId = unchecked((int)long.Parse(value.NewString(), CultureInfo.InvariantCulture)); break;
			case "savedb": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "sortid": SortId = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "namemode": { if (!Enum.TryParse(value.NewString(), out GroupNamingMode val)) throw new FormatException(); NamingMode = val; } break;
			case "n_modifyp": NeededModifyPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "n_member_addp": NeededMemberAddPower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "n_member_remove_p": NeededMemberRemovePower = i32.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			
			}

		}
	}

	public sealed class TextMessage : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.TextMessage;
		

		public TextMessageTargetMode Target { get; set; }
		public str Message { get; set; }
		public ClientId TargetClientId { get; set; }
		public ClientId InvokerId { get; set; }
		public str InvokerName { get; set; }
		public Uid InvokerUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "targetmode": { if (!Enum.TryParse(value.NewString(), out TextMessageTargetMode val)) throw new FormatException(); Target = val; } break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "target": TargetClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokerid": InvokerId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class TokenUsed : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.TokenUsed;
		

		public str UsedToken { get; set; }
		public str TokenCustomSet { get; set; }
		public str Token1 { get; set; }
		public str Token2 { get; set; }
		public ClientId ClientId { get; set; }
		public ClientDbId ClientDbId { get; set; }
		public Uid ClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "token": UsedToken = Ts3String.Unescape(value); break;
			case "tokencustomset": TokenCustomSet = Ts3String.Unescape(value); break;
			case "token1": Token1 = Ts3String.Unescape(value); break;
			case "token2": Token2 = Ts3String.Unescape(value); break;
			case "clid": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cldbid": ClientDbId = ClientDbId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}
	}

	public sealed class WhoAmI : IResponse
	{
		
		public string ReturnCode { get; set; }

		public ClientId ClientId { get; set; }
		public ChannelId ChannelId { get; set; }
		public str Name { get; set; }
		public ClientDbId DatabaseId { get; set; }
		public str LoginName { get; set; }
		public u64 OriginServerId { get; set; }
		public u64 VirtualServerId { get; set; }
		public Uid VirtualServerUid { get; set; }
		public u16 VirtualServerPort { get; set; }
		public str VirtualServerStatus { get; set; }
		public Uid Uid { get; set; }

		public void SetField(string name, ReadOnlySpan<char> value)
		{

			switch(name)
			{

			case "client_id": ClientId = ClientId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_channel_id": ChannelId = ChannelId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_database_id": DatabaseId = ClientDbId.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "client_login_name": LoginName = Ts3String.Unescape(value); break;
			case "client_origin_server_id": OriginServerId = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_id": VirtualServerId = u64.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_unique_identifier": VirtualServerUid = Ts3String.Unescape(value); break;
			case "virtualserver_port": VirtualServerPort = u16.Parse(value.NewString(), CultureInfo.InvariantCulture); break;
			case "virtualserver_status": VirtualServerStatus = Ts3String.Unescape(value); break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}
	}

	public enum NotificationType
	{
		Unknown,
		///<summary>[S2C] ntfy:notifychannelchanged</summary>
		ChannelChanged,
		///<summary>[S2C] ntfy:notifychannelcreated</summary>
		ChannelCreated,
		///<summary>[S2C] ntfy:notifychanneldeleted</summary>
		ChannelDeleted,
		///<summary>[S2C] ntfy:notifychanneledited</summary>
		ChannelEdited,
		///<summary>[S2C] ntfy:notifychannelgrouplist</summary>
		ChannelGroupList,
		///<summary>[S2C] ntfy:channellist</summary>
		ChannelList,
		///<summary>[S2C] ntfy:channellistfinished</summary>
		ChannelListFinished,
		///<summary>[S2C] ntfy:notifychannelmoved</summary>
		ChannelMoved,
		///<summary>[S2C] ntfy:notifychannelpasswordchanged</summary>
		ChannelPasswordChanged,
		///<summary>[S2C] ntfy:notifychannelsubscribed</summary>
		ChannelSubscribed,
		///<summary>[S2C] ntfy:notifychannelunsubscribed</summary>
		ChannelUnsubscribed,
		///<summary>[S2C] ntfy:notifyclientchannelgroupchanged</summary>
		ClientChannelGroupChanged,
		///<summary>[S2C] ntfy:notifyclientchatcomposing</summary>
		ClientChatComposing,
		///<summary>[S2C] ntfy:notifyclientdbidfromuid</summary>
		ClientDbIdFromUid,
		///<summary>[S2C] ntfy:notifycliententerview</summary>
		ClientEnterView,
		///<summary>[S2C] ntfy:notifyclientids</summary>
		ClientIds,
		///<summary>[C2S] ntfy:clientinit</summary>
		ClientInit,
		///<summary>[C2S] ntfy:clientinitiv</summary>
		ClientInitIv,
		///<summary>[S2C] ntfy:notifyclientleftview</summary>
		ClientLeftView,
		///<summary>[S2C] ntfy:notifyclientmoved</summary>
		ClientMoved,
		///<summary>[S2C] ntfy:notifyclientneededpermissions</summary>
		ClientNeededPermissions,
		///<summary>[S2C] ntfy:notifyclientpoke</summary>
		ClientPoke,
		///<summary>[S2C] ntfy:notifyservergroupsbyclientid</summary>
		ClientServerGroup,
		///<summary>[S2C] ntfy:notifyservergroupclientadded</summary>
		ClientServerGroupAdded,
		///<summary>[S2C] ntfy:error</summary>
		CommandError,
		///<summary>[S2C] ntfy:notifyconnectioninfo</summary>
		ConnectionInfo,
		///<summary>[S2C] ntfy:notifyconnectioninforequest</summary>
		ConnectionInfoRequest,
		///<summary>[S2C] ntfy:notifystartdownload</summary>
		FileDownload,
		///<summary>[S2C] ntfy:notifyfileinfo</summary>
		FileInfoTs,
		///<summary>[S2C] ntfy:notifyfilelist</summary>
		FileList,
		///<summary>[S2C] ntfy:notifyfilelistfinished</summary>
		FileListFinished,
		///<summary>[S2C] ntfy:notifyfiletransferlist</summary>
		FileTransfer,
		///<summary>[S2C] ntfy:notifystatusfiletransfer</summary>
		FileTransferStatus,
		///<summary>[S2C] ntfy:notifystartupload</summary>
		FileUpload,
		///<summary>[C2S] ntfy:clientgetdbidfromuid</summary>
		GetClientDbIdFromUid,
		///<summary>[C2S] ntfy:clientgetids</summary>
		GetClientIds,
		///<summary>[S2C] ntfy:initivexpand</summary>
		InitIvExpand,
		///<summary>[S2C] ntfy:initivexpand2</summary>
		InitIvExpand2,
		///<summary>[S2C] ntfy:initserver</summary>
		InitServer,
		///<summary>[S2C] ntfy:notifyplugincmd</summary>
		PluginCommand,
		///<summary>[C2S] ntfy:plugincmd</summary>
		PluginCommandRequest,
		///<summary>[S2C] ntfy:notifyserveredited</summary>
		ServerEdited,
		///<summary>[S2C] ntfy:notifyservergrouplist</summary>
		ServerGroupList,
		///<summary>[S2C] ntfy:notifytextmessage</summary>
		TextMessage,
		///<summary>[S2C] ntfy:notifytokenused</summary>
		TokenUsed,
	}

	public static class MessageHelper
	{
		public static NotificationType GetNotificationType(string name)
		{
			switch(name)
			{
			case "notifychannelchanged": return NotificationType.ChannelChanged;
			case "notifychannelcreated": return NotificationType.ChannelCreated;
			case "notifychanneldeleted": return NotificationType.ChannelDeleted;
			case "notifychanneledited": return NotificationType.ChannelEdited;
			case "notifychannelgrouplist": return NotificationType.ChannelGroupList;
			case "channellist": return NotificationType.ChannelList;
			case "channellistfinished": return NotificationType.ChannelListFinished;
			case "notifychannelmoved": return NotificationType.ChannelMoved;
			case "notifychannelpasswordchanged": return NotificationType.ChannelPasswordChanged;
			case "notifychannelsubscribed": return NotificationType.ChannelSubscribed;
			case "notifychannelunsubscribed": return NotificationType.ChannelUnsubscribed;
			case "notifyclientchannelgroupchanged": return NotificationType.ClientChannelGroupChanged;
			case "notifyclientchatcomposing": return NotificationType.ClientChatComposing;
			case "notifyclientdbidfromuid": return NotificationType.ClientDbIdFromUid;
			case "notifycliententerview": return NotificationType.ClientEnterView;
			case "notifyclientids": return NotificationType.ClientIds;
			case "clientinit": return NotificationType.ClientInit;
			case "clientinitiv": return NotificationType.ClientInitIv;
			case "notifyclientleftview": return NotificationType.ClientLeftView;
			case "notifyclientmoved": return NotificationType.ClientMoved;
			case "notifyclientneededpermissions": return NotificationType.ClientNeededPermissions;
			case "notifyclientpoke": return NotificationType.ClientPoke;
			case "notifyservergroupsbyclientid": return NotificationType.ClientServerGroup;
			case "notifyservergroupclientadded": return NotificationType.ClientServerGroupAdded;
			case "error": return NotificationType.CommandError;
			case "notifyconnectioninfo": return NotificationType.ConnectionInfo;
			case "notifyconnectioninforequest": return NotificationType.ConnectionInfoRequest;
			case "notifystartdownload": return NotificationType.FileDownload;
			case "notifyfileinfo": return NotificationType.FileInfoTs;
			case "notifyfilelist": return NotificationType.FileList;
			case "notifyfilelistfinished": return NotificationType.FileListFinished;
			case "notifyfiletransferlist": return NotificationType.FileTransfer;
			case "notifystatusfiletransfer": return NotificationType.FileTransferStatus;
			case "notifystartupload": return NotificationType.FileUpload;
			case "clientgetdbidfromuid": return NotificationType.GetClientDbIdFromUid;
			case "clientgetids": return NotificationType.GetClientIds;
			case "initivexpand": return NotificationType.InitIvExpand;
			case "initivexpand2": return NotificationType.InitIvExpand2;
			case "initserver": return NotificationType.InitServer;
			case "notifyplugincmd": return NotificationType.PluginCommand;
			case "plugincmd": return NotificationType.PluginCommandRequest;
			case "notifyserveredited": return NotificationType.ServerEdited;
			case "notifyservergrouplist": return NotificationType.ServerGroupList;
			case "notifytextmessage": return NotificationType.TextMessage;
			case "notifytokenused": return NotificationType.TokenUsed;
			default: return NotificationType.Unknown;
			}
		}

		public static INotification GenerateNotificationType(NotificationType name)
		{
			switch(name)
			{
			case NotificationType.ChannelChanged: return new ChannelChanged();
			case NotificationType.ChannelCreated: return new ChannelCreated();
			case NotificationType.ChannelDeleted: return new ChannelDeleted();
			case NotificationType.ChannelEdited: return new ChannelEdited();
			case NotificationType.ChannelGroupList: return new ChannelGroupList();
			case NotificationType.ChannelList: return new ChannelList();
			case NotificationType.ChannelListFinished: return new ChannelListFinished();
			case NotificationType.ChannelMoved: return new ChannelMoved();
			case NotificationType.ChannelPasswordChanged: return new ChannelPasswordChanged();
			case NotificationType.ChannelSubscribed: return new ChannelSubscribed();
			case NotificationType.ChannelUnsubscribed: return new ChannelUnsubscribed();
			case NotificationType.ClientChannelGroupChanged: return new ClientChannelGroupChanged();
			case NotificationType.ClientChatComposing: return new ClientChatComposing();
			case NotificationType.ClientDbIdFromUid: return new ClientDbIdFromUid();
			case NotificationType.ClientEnterView: return new ClientEnterView();
			case NotificationType.ClientIds: return new ClientIds();
			case NotificationType.ClientInit: return new ClientInit();
			case NotificationType.ClientInitIv: return new ClientInitIv();
			case NotificationType.ClientLeftView: return new ClientLeftView();
			case NotificationType.ClientMoved: return new ClientMoved();
			case NotificationType.ClientNeededPermissions: return new ClientNeededPermissions();
			case NotificationType.ClientPoke: return new ClientPoke();
			case NotificationType.ClientServerGroup: return new ClientServerGroup();
			case NotificationType.ClientServerGroupAdded: return new ClientServerGroupAdded();
			case NotificationType.CommandError: return new CommandError();
			case NotificationType.ConnectionInfo: return new ConnectionInfo();
			case NotificationType.ConnectionInfoRequest: return new ConnectionInfoRequest();
			case NotificationType.FileDownload: return new FileDownload();
			case NotificationType.FileInfoTs: return new FileInfoTs();
			case NotificationType.FileList: return new FileList();
			case NotificationType.FileListFinished: return new FileListFinished();
			case NotificationType.FileTransfer: return new FileTransfer();
			case NotificationType.FileTransferStatus: return new FileTransferStatus();
			case NotificationType.FileUpload: return new FileUpload();
			case NotificationType.GetClientDbIdFromUid: return new GetClientDbIdFromUid();
			case NotificationType.GetClientIds: return new GetClientIds();
			case NotificationType.InitIvExpand: return new InitIvExpand();
			case NotificationType.InitIvExpand2: return new InitIvExpand2();
			case NotificationType.InitServer: return new InitServer();
			case NotificationType.PluginCommand: return new PluginCommand();
			case NotificationType.PluginCommandRequest: return new PluginCommandRequest();
			case NotificationType.ServerEdited: return new ServerEdited();
			case NotificationType.ServerGroupList: return new ServerGroupList();
			case NotificationType.TextMessage: return new TextMessage();
			case NotificationType.TokenUsed: return new TokenUsed();
			case NotificationType.Unknown:
			default: throw Util.UnhandledDefault(name);
			}
		}
	}
}