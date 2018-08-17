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
	using System.Collections.Generic;
	using System.Globalization;
	using System.Buffers.Text;

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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelChanged[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "channel_order": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Order = oval; } break;
			case "channel_name": Name = Ts3String.Unescape(value); break;
			case "channel_topic": Topic = Ts3String.Unescape(value); break;
			case "channel_flag_default": IsDefault = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_password": HasPassword = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_permanent": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_semi_permanent": IsSemiPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_codec": { if(Utf8Parser.TryParse(value, out u8 oval, out _)) Codec = (Codec)oval; } break;
			case "channel_codec_quality": { if(Utf8Parser.TryParse(value, out u8 oval, out _)) CodecQuality = oval; } break;
			case "channel_needed_talk_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededTalkPower = oval; } break;
			case "channel_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "channel_maxclients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MaxClients = oval; } break;
			case "channel_maxfamilyclients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MaxFamilyClients = oval; } break;
			case "channel_codec_latency_factor": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) CodecLatencyFactor = oval; } break;
			case "channel_codec_is_unencrypted": IsUnencrypted = value.Length > 0 && value[0] != '0'; break;
			case "channel_delete_delay": { if(Utf8Parser.TryParse(value, out double oval, out _)) DeleteDelay = TimeSpan.FromSeconds(oval); } break;
			case "channel_flag_maxclients_unlimited": IsMaxClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_unlimited": IsMaxFamilyClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_inherited": InheritsMaxFamilyClients = value.Length > 0 && value[0] != '0'; break;
			case "channel_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "cpid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelParentId = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelCreated[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				case "channel_order": foreach(var toi in toc) { toi.Order = Order; } break;
				case "channel_name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "channel_topic": foreach(var toi in toc) { toi.Topic = Topic; } break;
				case "channel_flag_default": foreach(var toi in toc) { toi.IsDefault = IsDefault; } break;
				case "channel_flag_password": foreach(var toi in toc) { toi.HasPassword = HasPassword; } break;
				case "channel_flag_permanent": foreach(var toi in toc) { toi.IsPermanent = IsPermanent; } break;
				case "channel_flag_semi_permanent": foreach(var toi in toc) { toi.IsSemiPermanent = IsSemiPermanent; } break;
				case "channel_codec": foreach(var toi in toc) { toi.Codec = Codec; } break;
				case "channel_codec_quality": foreach(var toi in toc) { toi.CodecQuality = CodecQuality; } break;
				case "channel_needed_talk_power": foreach(var toi in toc) { toi.NeededTalkPower = NeededTalkPower; } break;
				case "channel_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "channel_maxclients": foreach(var toi in toc) { toi.MaxClients = MaxClients; } break;
				case "channel_maxfamilyclients": foreach(var toi in toc) { toi.MaxFamilyClients = MaxFamilyClients; } break;
				case "channel_codec_latency_factor": foreach(var toi in toc) { toi.CodecLatencyFactor = CodecLatencyFactor; } break;
				case "channel_codec_is_unencrypted": foreach(var toi in toc) { toi.IsUnencrypted = IsUnencrypted; } break;
				case "channel_delete_delay": foreach(var toi in toc) { toi.DeleteDelay = DeleteDelay; } break;
				case "channel_flag_maxclients_unlimited": foreach(var toi in toc) { toi.IsMaxClientsUnlimited = IsMaxClientsUnlimited; } break;
				case "channel_flag_maxfamilyclients_unlimited": foreach(var toi in toc) { toi.IsMaxFamilyClientsUnlimited = IsMaxFamilyClientsUnlimited; } break;
				case "channel_flag_maxfamilyclients_inherited": foreach(var toi in toc) { toi.InheritsMaxFamilyClients = InheritsMaxFamilyClients; } break;
				case "channel_name_phonetic": foreach(var toi in toc) { toi.PhoneticName = PhoneticName; } break;
				case "cpid": foreach(var toi in toc) { toi.ChannelParentId = ChannelParentId; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "id": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) Id = oval; } break;
			case "pid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ParentChannelId = oval; } break;
			case "seconds_empty": { if(Utf8Parser.TryParse(value, out double oval, out _)) DurationEmpty = TimeSpan.FromSeconds(oval); } break;
			case "total_clients_family": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) TotalFamilyClients = oval; } break;
			case "total_clients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) TotalClients = oval; } break;
			case "channel_needed_subscribe_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededSubscribePower = oval; } break;
			case "channel_order": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Order = oval; } break;
			case "channel_name": Name = Ts3String.Unescape(value); break;
			case "channel_topic": Topic = Ts3String.Unescape(value); break;
			case "channel_flag_default": IsDefault = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_password": HasPassword = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_permanent": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_semi_permanent": IsSemiPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_codec": { if(Utf8Parser.TryParse(value, out u8 oval, out _)) Codec = (Codec)oval; } break;
			case "channel_codec_quality": { if(Utf8Parser.TryParse(value, out u8 oval, out _)) CodecQuality = oval; } break;
			case "channel_needed_talk_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededTalkPower = oval; } break;
			case "channel_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "channel_maxclients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MaxClients = oval; } break;
			case "channel_maxfamilyclients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MaxFamilyClients = oval; } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelData[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "id": foreach(var toi in toc) { toi.Id = Id; } break;
				case "pid": foreach(var toi in toc) { toi.ParentChannelId = ParentChannelId; } break;
				case "seconds_empty": foreach(var toi in toc) { toi.DurationEmpty = DurationEmpty; } break;
				case "total_clients_family": foreach(var toi in toc) { toi.TotalFamilyClients = TotalFamilyClients; } break;
				case "total_clients": foreach(var toi in toc) { toi.TotalClients = TotalClients; } break;
				case "channel_needed_subscribe_power": foreach(var toi in toc) { toi.NeededSubscribePower = NeededSubscribePower; } break;
				case "channel_order": foreach(var toi in toc) { toi.Order = Order; } break;
				case "channel_name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "channel_topic": foreach(var toi in toc) { toi.Topic = Topic; } break;
				case "channel_flag_default": foreach(var toi in toc) { toi.IsDefault = IsDefault; } break;
				case "channel_flag_password": foreach(var toi in toc) { toi.HasPassword = HasPassword; } break;
				case "channel_flag_permanent": foreach(var toi in toc) { toi.IsPermanent = IsPermanent; } break;
				case "channel_flag_semi_permanent": foreach(var toi in toc) { toi.IsSemiPermanent = IsSemiPermanent; } break;
				case "channel_codec": foreach(var toi in toc) { toi.Codec = Codec; } break;
				case "channel_codec_quality": foreach(var toi in toc) { toi.CodecQuality = CodecQuality; } break;
				case "channel_needed_talk_power": foreach(var toi in toc) { toi.NeededTalkPower = NeededTalkPower; } break;
				case "channel_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "channel_maxclients": foreach(var toi in toc) { toi.MaxClients = MaxClients; } break;
				case "channel_maxfamilyclients": foreach(var toi in toc) { toi.MaxFamilyClients = MaxFamilyClients; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelDeleted[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "channel_order": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Order = oval; } break;
			case "channel_name": Name = Ts3String.Unescape(value); break;
			case "channel_topic": Topic = Ts3String.Unescape(value); break;
			case "channel_flag_default": IsDefault = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_password": HasPassword = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_permanent": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_semi_permanent": IsSemiPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_codec": { if(Utf8Parser.TryParse(value, out u8 oval, out _)) Codec = (Codec)oval; } break;
			case "channel_codec_quality": { if(Utf8Parser.TryParse(value, out u8 oval, out _)) CodecQuality = oval; } break;
			case "channel_needed_talk_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededTalkPower = oval; } break;
			case "channel_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "channel_maxclients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MaxClients = oval; } break;
			case "channel_maxfamilyclients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MaxFamilyClients = oval; } break;
			case "channel_codec_latency_factor": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) CodecLatencyFactor = oval; } break;
			case "channel_codec_is_unencrypted": IsUnencrypted = value.Length > 0 && value[0] != '0'; break;
			case "channel_delete_delay": { if(Utf8Parser.TryParse(value, out double oval, out _)) DeleteDelay = TimeSpan.FromSeconds(oval); } break;
			case "channel_flag_maxclients_unlimited": IsMaxClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_unlimited": IsMaxFamilyClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_inherited": InheritsMaxFamilyClients = value.Length > 0 && value[0] != '0'; break;
			case "channel_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "reasonid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Reason = (Reason)oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelEdited[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				case "channel_order": foreach(var toi in toc) { toi.Order = Order; } break;
				case "channel_name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "channel_topic": foreach(var toi in toc) { toi.Topic = Topic; } break;
				case "channel_flag_default": foreach(var toi in toc) { toi.IsDefault = IsDefault; } break;
				case "channel_flag_password": foreach(var toi in toc) { toi.HasPassword = HasPassword; } break;
				case "channel_flag_permanent": foreach(var toi in toc) { toi.IsPermanent = IsPermanent; } break;
				case "channel_flag_semi_permanent": foreach(var toi in toc) { toi.IsSemiPermanent = IsSemiPermanent; } break;
				case "channel_codec": foreach(var toi in toc) { toi.Codec = Codec; } break;
				case "channel_codec_quality": foreach(var toi in toc) { toi.CodecQuality = CodecQuality; } break;
				case "channel_needed_talk_power": foreach(var toi in toc) { toi.NeededTalkPower = NeededTalkPower; } break;
				case "channel_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "channel_maxclients": foreach(var toi in toc) { toi.MaxClients = MaxClients; } break;
				case "channel_maxfamilyclients": foreach(var toi in toc) { toi.MaxFamilyClients = MaxFamilyClients; } break;
				case "channel_codec_latency_factor": foreach(var toi in toc) { toi.CodecLatencyFactor = CodecLatencyFactor; } break;
				case "channel_codec_is_unencrypted": foreach(var toi in toc) { toi.IsUnencrypted = IsUnencrypted; } break;
				case "channel_delete_delay": foreach(var toi in toc) { toi.DeleteDelay = DeleteDelay; } break;
				case "channel_flag_maxclients_unlimited": foreach(var toi in toc) { toi.IsMaxClientsUnlimited = IsMaxClientsUnlimited; } break;
				case "channel_flag_maxfamilyclients_unlimited": foreach(var toi in toc) { toi.IsMaxFamilyClientsUnlimited = IsMaxFamilyClientsUnlimited; } break;
				case "channel_flag_maxfamilyclients_inherited": foreach(var toi in toc) { toi.InheritsMaxFamilyClients = InheritsMaxFamilyClients; } break;
				case "channel_name_phonetic": foreach(var toi in toc) { toi.PhoneticName = PhoneticName; } break;
				case "reasonid": foreach(var toi in toc) { toi.Reason = Reason; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cgid": { if(Utf8Parser.TryParse(value, out ChannelGroupId oval, out _)) ChannelGroup = oval; } break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "type": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) GroupType = (GroupType)oval; } break;
			case "iconid": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "savedb": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "sortid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) SortId = oval; } break;
			case "namemode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NamingMode = (GroupNamingMode)oval; } break;
			case "n_modifyp": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededModifyPower = oval; } break;
			case "n_member_addp": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededMemberAddPower = oval; } break;
			case "n_member_remove_p": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededMemberRemovePower = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelGroupList[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cgid": foreach(var toi in toc) { toi.ChannelGroup = ChannelGroup; } break;
				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "type": foreach(var toi in toc) { toi.GroupType = GroupType; } break;
				case "iconid": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "savedb": foreach(var toi in toc) { toi.IsPermanent = IsPermanent; } break;
				case "sortid": foreach(var toi in toc) { toi.SortId = SortId; } break;
				case "namemode": foreach(var toi in toc) { toi.NamingMode = NamingMode; } break;
				case "n_modifyp": foreach(var toi in toc) { toi.NeededModifyPower = NeededModifyPower; } break;
				case "n_member_addp": foreach(var toi in toc) { toi.NeededMemberAddPower = NeededMemberAddPower; } break;
				case "n_member_remove_p": foreach(var toi in toc) { toi.NeededMemberRemovePower = NeededMemberRemovePower; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "cpid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelParentId = oval; } break;
			case "channel_name": Name = Ts3String.Unescape(value); break;
			case "channel_topic": Topic = Ts3String.Unescape(value); break;
			case "channel_codec": { if(Utf8Parser.TryParse(value, out u8 oval, out _)) Codec = (Codec)oval; } break;
			case "channel_codec_quality": { if(Utf8Parser.TryParse(value, out u8 oval, out _)) CodecQuality = oval; } break;
			case "channel_maxclients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MaxClients = oval; } break;
			case "channel_maxfamilyclients": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MaxFamilyClients = oval; } break;
			case "channel_order": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Order = oval; } break;
			case "channel_flag_permanent": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_semi_permanent": IsSemiPermanent = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_default": IsDefault = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_password": HasPassword = value.Length > 0 && value[0] != '0'; break;
			case "channel_codec_latency_factor": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) CodecLatencyFactor = oval; } break;
			case "channel_codec_is_unencrypted": IsUnencrypted = value.Length > 0 && value[0] != '0'; break;
			case "channel_delete_delay": { if(Utf8Parser.TryParse(value, out double oval, out _)) DeleteDelay = TimeSpan.FromSeconds(oval); } break;
			case "channel_flag_maxclients_unlimited": IsMaxClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_unlimited": IsMaxFamilyClientsUnlimited = value.Length > 0 && value[0] != '0'; break;
			case "channel_flag_maxfamilyclients_inherited": InheritsMaxFamilyClients = value.Length > 0 && value[0] != '0'; break;
			case "channel_needed_talk_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededTalkPower = oval; } break;
			case "channel_forced_silence": ForcedSilence = value.Length > 0 && value[0] != '0'; break;
			case "channel_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "channel_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "channel_flag_private": IsPrivate = value.Length > 0 && value[0] != '0'; break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelList[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "cpid": foreach(var toi in toc) { toi.ChannelParentId = ChannelParentId; } break;
				case "channel_name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "channel_topic": foreach(var toi in toc) { toi.Topic = Topic; } break;
				case "channel_codec": foreach(var toi in toc) { toi.Codec = Codec; } break;
				case "channel_codec_quality": foreach(var toi in toc) { toi.CodecQuality = CodecQuality; } break;
				case "channel_maxclients": foreach(var toi in toc) { toi.MaxClients = MaxClients; } break;
				case "channel_maxfamilyclients": foreach(var toi in toc) { toi.MaxFamilyClients = MaxFamilyClients; } break;
				case "channel_order": foreach(var toi in toc) { toi.Order = Order; } break;
				case "channel_flag_permanent": foreach(var toi in toc) { toi.IsPermanent = IsPermanent; } break;
				case "channel_flag_semi_permanent": foreach(var toi in toc) { toi.IsSemiPermanent = IsSemiPermanent; } break;
				case "channel_flag_default": foreach(var toi in toc) { toi.IsDefault = IsDefault; } break;
				case "channel_flag_password": foreach(var toi in toc) { toi.HasPassword = HasPassword; } break;
				case "channel_codec_latency_factor": foreach(var toi in toc) { toi.CodecLatencyFactor = CodecLatencyFactor; } break;
				case "channel_codec_is_unencrypted": foreach(var toi in toc) { toi.IsUnencrypted = IsUnencrypted; } break;
				case "channel_delete_delay": foreach(var toi in toc) { toi.DeleteDelay = DeleteDelay; } break;
				case "channel_flag_maxclients_unlimited": foreach(var toi in toc) { toi.IsMaxClientsUnlimited = IsMaxClientsUnlimited; } break;
				case "channel_flag_maxfamilyclients_unlimited": foreach(var toi in toc) { toi.IsMaxFamilyClientsUnlimited = IsMaxFamilyClientsUnlimited; } break;
				case "channel_flag_maxfamilyclients_inherited": foreach(var toi in toc) { toi.InheritsMaxFamilyClients = InheritsMaxFamilyClients; } break;
				case "channel_needed_talk_power": foreach(var toi in toc) { toi.NeededTalkPower = NeededTalkPower; } break;
				case "channel_forced_silence": foreach(var toi in toc) { toi.ForcedSilence = ForcedSilence; } break;
				case "channel_name_phonetic": foreach(var toi in toc) { toi.PhoneticName = PhoneticName; } break;
				case "channel_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "channel_flag_private": foreach(var toi in toc) { toi.IsPrivate = IsPrivate; } break;
				}
			}

		}
	}

	public sealed class ChannelListFinished : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelListFinished;
		


		public void SetField(string name, ReadOnlySpan<byte> value)
		{
		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "order": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Order = oval; } break;
			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "reasonid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Reason = (Reason)oval; } break;
			case "cpid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelParentId = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelMoved[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "order": foreach(var toi in toc) { toi.Order = Order; } break;
				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				case "reasonid": foreach(var toi in toc) { toi.Reason = Reason; } break;
				case "cpid": foreach(var toi in toc) { toi.ChannelParentId = ChannelParentId; } break;
				}
			}

		}
	}

	public sealed class ChannelPasswordChanged : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelPasswordChanged;
		

		public ChannelId ChannelId { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelPasswordChanged[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				}
			}

		}
	}

	public sealed class ChannelSubscribed : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelSubscribed;
		

		public ChannelId ChannelId { get; set; }
		public DurationSeconds EmptySince { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "es": { if(Utf8Parser.TryParse(value, out double oval, out _)) EmptySince = TimeSpan.FromSeconds(oval); } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelSubscribed[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "es": foreach(var toi in toc) { toi.EmptySince = EmptySince; } break;
				}
			}

		}
	}

	public sealed class ChannelUnsubscribed : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ChannelUnsubscribed;
		

		public ChannelId ChannelId { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ChannelUnsubscribed[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "cgid": { if(Utf8Parser.TryParse(value, out ChannelGroupId oval, out _)) ChannelGroup = oval; } break;
			case "cgi": { if(Utf8Parser.TryParse(value, out ChannelGroupId oval, out _)) ChannelGroupIndex = oval; } break;
			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientChannelGroupChanged[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "cgid": foreach(var toi in toc) { toi.ChannelGroup = ChannelGroup; } break;
				case "cgi": foreach(var toi in toc) { toi.ChannelGroupIndex = ChannelGroupIndex; } break;
				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				}
			}

		}
	}

	public sealed class ClientChatComposing : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientChatComposing;
		

		public ClientId ClientId { get; set; }
		public Uid ClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientChatComposing[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "cluid": foreach(var toi in toc) { toi.ClientUid = ClientUid; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "client_database_id": { if(Utf8Parser.TryParse(value, out ClientDbId oval, out _)) DatabaseId = oval; } break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_type": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) ClientType = (ClientType)oval; } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientData[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "client_unique_identifier": foreach(var toi in toc) { toi.Uid = Uid; } break;
				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "client_database_id": foreach(var toi in toc) { toi.DatabaseId = DatabaseId; } break;
				case "client_nickname": foreach(var toi in toc) { toi.Name = Name; } break;
				case "client_type": foreach(var toi in toc) { toi.ClientType = ClientType; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "client_lastip": LastIp = Ts3String.Unescape(value); break;
			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "client_database_id": { if(Utf8Parser.TryParse(value, out ClientDbId oval, out _)) DatabaseId = oval; } break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_type": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) ClientType = (ClientType)oval; } break;
			case "client_flag_avatar": AvatarHash = Ts3String.Unescape(value); break;
			case "client_description": Description = Ts3String.Unescape(value); break;
			case "client_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "client_created": { if(Utf8Parser.TryParse(value, out double oval, out _)) CreationDate = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "client_lastconnected": { if(Utf8Parser.TryParse(value, out double oval, out _)) LastConnected = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "client_totalconnections": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) TotalConnections = oval; } break;
			case "client_month_bytes_uploaded": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) MonthlyUploadQuota = oval; } break;
			case "client_month_bytes_downloaded": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) MonthlyDownloadQuota = oval; } break;
			case "client_total_bytes_uploaded": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) TotalUploadQuota = oval; } break;
			case "client_total_bytes_downloaded": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) TotalDownloadQuota = oval; } break;
			case "client_base64HashClientUID": Base64HashClientUid = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientDbData[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "client_lastip": foreach(var toi in toc) { toi.LastIp = LastIp; } break;
				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "client_unique_identifier": foreach(var toi in toc) { toi.Uid = Uid; } break;
				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "client_database_id": foreach(var toi in toc) { toi.DatabaseId = DatabaseId; } break;
				case "client_nickname": foreach(var toi in toc) { toi.Name = Name; } break;
				case "client_type": foreach(var toi in toc) { toi.ClientType = ClientType; } break;
				case "client_flag_avatar": foreach(var toi in toc) { toi.AvatarHash = AvatarHash; } break;
				case "client_description": foreach(var toi in toc) { toi.Description = Description; } break;
				case "client_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "client_created": foreach(var toi in toc) { toi.CreationDate = CreationDate; } break;
				case "client_lastconnected": foreach(var toi in toc) { toi.LastConnected = LastConnected; } break;
				case "client_totalconnections": foreach(var toi in toc) { toi.TotalConnections = TotalConnections; } break;
				case "client_month_bytes_uploaded": foreach(var toi in toc) { toi.MonthlyUploadQuota = MonthlyUploadQuota; } break;
				case "client_month_bytes_downloaded": foreach(var toi in toc) { toi.MonthlyDownloadQuota = MonthlyDownloadQuota; } break;
				case "client_total_bytes_uploaded": foreach(var toi in toc) { toi.TotalUploadQuota = TotalUploadQuota; } break;
				case "client_total_bytes_downloaded": foreach(var toi in toc) { toi.TotalDownloadQuota = TotalDownloadQuota; } break;
				case "client_base64HashClientUID": foreach(var toi in toc) { toi.Base64HashClientUid = Base64HashClientUid; } break;
				}
			}

		}
	}

	public sealed class ClientDbIdFromUid : INotification, IResponse
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientDbIdFromUid;
		public string ReturnCode { get; set; }

		public Uid ClientUid { get; set; }
		public ClientDbId ClientDbId { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			case "cldbid": { if(Utf8Parser.TryParse(value, out ClientDbId oval, out _)) ClientDbId = oval; } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientDbIdFromUid[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cluid": foreach(var toi in toc) { toi.ClientUid = ClientUid; } break;
				case "cldbid": foreach(var toi in toc) { toi.ClientDbId = ClientDbId; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "reasonid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Reason = (Reason)oval; } break;
			case "ctid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) TargetChannelId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "client_database_id": { if(Utf8Parser.TryParse(value, out ClientDbId oval, out _)) DatabaseId = oval; } break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_type": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) ClientType = (ClientType)oval; } break;
			case "cfid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) SourceChannelId = oval; } break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "client_flag_avatar": AvatarHash = Ts3String.Unescape(value); break;
			case "client_description": Description = Ts3String.Unescape(value); break;
			case "client_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "client_input_muted": InputMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_output_muted": OutputMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_outputonly_muted": OutputOnlyMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_input_hardware": InputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_output_hardware": OutputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_meta_data": Metadata = Ts3String.Unescape(value); break;
			case "client_is_recording": IsRecording = value.Length > 0 && value[0] != '0'; break;
			case "client_channel_group_id": { if(Utf8Parser.TryParse(value, out ChannelGroupId oval, out _)) ChannelGroup = oval; } break;
			case "client_channel_group_inherited_channel_id": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) InheritedChannelGroupFromChannel = oval; } break;
			case "client_servergroups": { if(value.Length == 0) ServerGroups = Array.Empty<ServerGroupId>(); else { var ss = new SpanSplitter<byte>(); ss.First(value, (byte)','); int cnt = 0; for (int i = 0; i < value.Length; i++) if (value[i] == ',') cnt++; ServerGroups = new ServerGroupId[cnt + 1]; for(int i = 0; i < cnt + 1; i++) { { if(Utf8Parser.TryParse(ss.Trim(value), out ServerGroupId oval, out _)) ServerGroups[i] = oval; } if (i < cnt) value = ss.Next(value); } } } break;
			case "client_away": IsAway = value.Length > 0 && value[0] != '0'; break;
			case "client_away_message": AwayMessage = Ts3String.Unescape(value); break;
			case "client_talk_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) TalkPower = oval; } break;
			case "client_talk_request": { if(Utf8Parser.TryParse(value, out double oval, out _)) TalkPowerRequestTime = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "client_talk_request_msg": TalkPowerRequestMessage = Ts3String.Unescape(value); break;
			case "client_is_talker": TalkPowerGranted = value.Length > 0 && value[0] != '0'; break;
			case "client_is_priority_speaker": IsPrioritySpeaker = value.Length > 0 && value[0] != '0'; break;
			case "client_unread_messages": { if(Utf8Parser.TryParse(value, out u32 oval, out _)) UnreadMessages = oval; } break;
			case "client_nickname_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "client_needed_serverquery_view_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededServerqueryViewPower = oval; } break;
			case "client_is_channel_commander": IsChannelCommander = value.Length > 0 && value[0] != '0'; break;
			case "client_country": CountryCode = Ts3String.Unescape(value); break;
			case "client_badges": Badges = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientEnterView[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "reasonid": foreach(var toi in toc) { toi.Reason = Reason; } break;
				case "ctid": foreach(var toi in toc) { toi.TargetChannelId = TargetChannelId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "client_database_id": foreach(var toi in toc) { toi.DatabaseId = DatabaseId; } break;
				case "client_nickname": foreach(var toi in toc) { toi.Name = Name; } break;
				case "client_type": foreach(var toi in toc) { toi.ClientType = ClientType; } break;
				case "cfid": foreach(var toi in toc) { toi.SourceChannelId = SourceChannelId; } break;
				case "client_unique_identifier": foreach(var toi in toc) { toi.Uid = Uid; } break;
				case "client_flag_avatar": foreach(var toi in toc) { toi.AvatarHash = AvatarHash; } break;
				case "client_description": foreach(var toi in toc) { toi.Description = Description; } break;
				case "client_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "client_input_muted": foreach(var toi in toc) { toi.InputMuted = InputMuted; } break;
				case "client_output_muted": foreach(var toi in toc) { toi.OutputMuted = OutputMuted; } break;
				case "client_outputonly_muted": foreach(var toi in toc) { toi.OutputOnlyMuted = OutputOnlyMuted; } break;
				case "client_input_hardware": foreach(var toi in toc) { toi.InputHardwareEnabled = InputHardwareEnabled; } break;
				case "client_output_hardware": foreach(var toi in toc) { toi.OutputHardwareEnabled = OutputHardwareEnabled; } break;
				case "client_meta_data": foreach(var toi in toc) { toi.Metadata = Metadata; } break;
				case "client_is_recording": foreach(var toi in toc) { toi.IsRecording = IsRecording; } break;
				case "client_channel_group_id": foreach(var toi in toc) { toi.ChannelGroup = ChannelGroup; } break;
				case "client_channel_group_inherited_channel_id": foreach(var toi in toc) { toi.InheritedChannelGroupFromChannel = InheritedChannelGroupFromChannel; } break;
				case "client_servergroups": foreach(var toi in toc) { toi.ServerGroups = ServerGroups; } break;
				case "client_away": foreach(var toi in toc) { toi.IsAway = IsAway; } break;
				case "client_away_message": foreach(var toi in toc) { toi.AwayMessage = AwayMessage; } break;
				case "client_talk_power": foreach(var toi in toc) { toi.TalkPower = TalkPower; } break;
				case "client_talk_request": foreach(var toi in toc) { toi.TalkPowerRequestTime = TalkPowerRequestTime; } break;
				case "client_talk_request_msg": foreach(var toi in toc) { toi.TalkPowerRequestMessage = TalkPowerRequestMessage; } break;
				case "client_is_talker": foreach(var toi in toc) { toi.TalkPowerGranted = TalkPowerGranted; } break;
				case "client_is_priority_speaker": foreach(var toi in toc) { toi.IsPrioritySpeaker = IsPrioritySpeaker; } break;
				case "client_unread_messages": foreach(var toi in toc) { toi.UnreadMessages = UnreadMessages; } break;
				case "client_nickname_phonetic": foreach(var toi in toc) { toi.PhoneticName = PhoneticName; } break;
				case "client_needed_serverquery_view_power": foreach(var toi in toc) { toi.NeededServerqueryViewPower = NeededServerqueryViewPower; } break;
				case "client_is_channel_commander": foreach(var toi in toc) { toi.IsChannelCommander = IsChannelCommander; } break;
				case "client_country": foreach(var toi in toc) { toi.CountryCode = CountryCode; } break;
				case "client_badges": foreach(var toi in toc) { toi.Badges = Badges; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientIds[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cluid": foreach(var toi in toc) { toi.ClientUid = ClientUid; } break;
				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "client_idle_time": { if(Utf8Parser.TryParse(value, out double oval, out _)) ClientIdleTime = TimeSpan.FromMilliseconds(oval); } break;
			case "client_version": ClientVersion = Ts3String.Unescape(value); break;
			case "client_version_sign": ClientVersionSign = Ts3String.Unescape(value); break;
			case "client_platform": ClientPlattform = Ts3String.Unescape(value); break;
			case "client_default_channel": DefaultChannel = Ts3String.Unescape(value); break;
			case "client_security_hash": SecurityHash = Ts3String.Unescape(value); break;
			case "client_login_name": LoginName = Ts3String.Unescape(value); break;
			case "client_default_token": DefaultToken = Ts3String.Unescape(value); break;
			case "connection_filetransfer_bandwidth_sent": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) FiletransferBandwidthSent = oval; } break;
			case "connection_filetransfer_bandwidth_received": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) FiletransferBandwidthReceived = oval; } break;
			case "connection_packets_sent_total": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) PacketsSentTotal = oval; } break;
			case "connection_packets_received_total": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) PacketsReceivedTotal = oval; } break;
			case "connection_bytes_sent_total": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BytesSentTotal = oval; } break;
			case "connection_bytes_received_total": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BytesReceivedTotal = oval; } break;
			case "connection_bandwidth_sent_last_second_total": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthSentLastSecondTotal = oval; } break;
			case "connection_bandwidth_received_last_second_total": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthReceivedLastSecondTotal = oval; } break;
			case "connection_bandwidth_sent_last_minute_total": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthSentLastMinuteTotal = oval; } break;
			case "connection_bandwidth_received_last_minute_total": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthReceivedLastMinuteTotal = oval; } break;
			case "connection_connected_time": { if(Utf8Parser.TryParse(value, out double oval, out _)) ConnectedTime = TimeSpan.FromMilliseconds(oval); } break;
			case "connection_client_ip": Ip = Ts3String.Unescape(value); break;
			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "client_database_id": { if(Utf8Parser.TryParse(value, out ClientDbId oval, out _)) DatabaseId = oval; } break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_type": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) ClientType = (ClientType)oval; } break;
			case "client_input_muted": InputMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_output_muted": OutputMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_outputonly_muted": OutputOnlyMuted = value.Length > 0 && value[0] != '0'; break;
			case "client_input_hardware": InputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_output_hardware": OutputHardwareEnabled = value.Length > 0 && value[0] != '0'; break;
			case "client_meta_data": Metadata = Ts3String.Unescape(value); break;
			case "client_is_recording": IsRecording = value.Length > 0 && value[0] != '0'; break;
			case "client_channel_group_id": { if(Utf8Parser.TryParse(value, out ChannelGroupId oval, out _)) ChannelGroup = oval; } break;
			case "client_channel_group_inherited_channel_id": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) InheritedChannelGroupFromChannel = oval; } break;
			case "client_servergroups": { if(value.Length == 0) ServerGroups = Array.Empty<ServerGroupId>(); else { var ss = new SpanSplitter<byte>(); ss.First(value, (byte)','); int cnt = 0; for (int i = 0; i < value.Length; i++) if (value[i] == ',') cnt++; ServerGroups = new ServerGroupId[cnt + 1]; for(int i = 0; i < cnt + 1; i++) { { if(Utf8Parser.TryParse(ss.Trim(value), out ServerGroupId oval, out _)) ServerGroups[i] = oval; } if (i < cnt) value = ss.Next(value); } } } break;
			case "client_away": IsAway = value.Length > 0 && value[0] != '0'; break;
			case "client_away_message": AwayMessage = Ts3String.Unescape(value); break;
			case "client_talk_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) TalkPower = oval; } break;
			case "client_talk_request": { if(Utf8Parser.TryParse(value, out double oval, out _)) TalkPowerRequestTime = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "client_talk_request_msg": TalkPowerRequestMessage = Ts3String.Unescape(value); break;
			case "client_is_talker": TalkPowerGranted = value.Length > 0 && value[0] != '0'; break;
			case "client_is_priority_speaker": IsPrioritySpeaker = value.Length > 0 && value[0] != '0'; break;
			case "client_unread_messages": { if(Utf8Parser.TryParse(value, out u32 oval, out _)) UnreadMessages = oval; } break;
			case "client_nickname_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "client_needed_serverquery_view_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededServerqueryViewPower = oval; } break;
			case "client_is_channel_commander": IsChannelCommander = value.Length > 0 && value[0] != '0'; break;
			case "client_country": CountryCode = Ts3String.Unescape(value); break;
			case "client_badges": Badges = Ts3String.Unescape(value); break;
			case "client_created": { if(Utf8Parser.TryParse(value, out double oval, out _)) CreationDate = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "client_lastconnected": { if(Utf8Parser.TryParse(value, out double oval, out _)) LastConnected = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "client_totalconnections": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) TotalConnections = oval; } break;
			case "client_month_bytes_uploaded": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) MonthlyUploadQuota = oval; } break;
			case "client_month_bytes_downloaded": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) MonthlyDownloadQuota = oval; } break;
			case "client_total_bytes_uploaded": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) TotalUploadQuota = oval; } break;
			case "client_total_bytes_downloaded": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) TotalDownloadQuota = oval; } break;
			case "client_base64HashClientUID": Base64HashClientUid = Ts3String.Unescape(value); break;
			case "client_flag_avatar": AvatarHash = Ts3String.Unescape(value); break;
			case "client_description": Description = Ts3String.Unescape(value); break;
			case "client_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientInfo[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "client_idle_time": foreach(var toi in toc) { toi.ClientIdleTime = ClientIdleTime; } break;
				case "client_version": foreach(var toi in toc) { toi.ClientVersion = ClientVersion; } break;
				case "client_version_sign": foreach(var toi in toc) { toi.ClientVersionSign = ClientVersionSign; } break;
				case "client_platform": foreach(var toi in toc) { toi.ClientPlattform = ClientPlattform; } break;
				case "client_default_channel": foreach(var toi in toc) { toi.DefaultChannel = DefaultChannel; } break;
				case "client_security_hash": foreach(var toi in toc) { toi.SecurityHash = SecurityHash; } break;
				case "client_login_name": foreach(var toi in toc) { toi.LoginName = LoginName; } break;
				case "client_default_token": foreach(var toi in toc) { toi.DefaultToken = DefaultToken; } break;
				case "connection_filetransfer_bandwidth_sent": foreach(var toi in toc) { toi.FiletransferBandwidthSent = FiletransferBandwidthSent; } break;
				case "connection_filetransfer_bandwidth_received": foreach(var toi in toc) { toi.FiletransferBandwidthReceived = FiletransferBandwidthReceived; } break;
				case "connection_packets_sent_total": foreach(var toi in toc) { toi.PacketsSentTotal = PacketsSentTotal; } break;
				case "connection_packets_received_total": foreach(var toi in toc) { toi.PacketsReceivedTotal = PacketsReceivedTotal; } break;
				case "connection_bytes_sent_total": foreach(var toi in toc) { toi.BytesSentTotal = BytesSentTotal; } break;
				case "connection_bytes_received_total": foreach(var toi in toc) { toi.BytesReceivedTotal = BytesReceivedTotal; } break;
				case "connection_bandwidth_sent_last_second_total": foreach(var toi in toc) { toi.BandwidthSentLastSecondTotal = BandwidthSentLastSecondTotal; } break;
				case "connection_bandwidth_received_last_second_total": foreach(var toi in toc) { toi.BandwidthReceivedLastSecondTotal = BandwidthReceivedLastSecondTotal; } break;
				case "connection_bandwidth_sent_last_minute_total": foreach(var toi in toc) { toi.BandwidthSentLastMinuteTotal = BandwidthSentLastMinuteTotal; } break;
				case "connection_bandwidth_received_last_minute_total": foreach(var toi in toc) { toi.BandwidthReceivedLastMinuteTotal = BandwidthReceivedLastMinuteTotal; } break;
				case "connection_connected_time": foreach(var toi in toc) { toi.ConnectedTime = ConnectedTime; } break;
				case "connection_client_ip": foreach(var toi in toc) { toi.Ip = Ip; } break;
				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "client_unique_identifier": foreach(var toi in toc) { toi.Uid = Uid; } break;
				case "client_database_id": foreach(var toi in toc) { toi.DatabaseId = DatabaseId; } break;
				case "client_nickname": foreach(var toi in toc) { toi.Name = Name; } break;
				case "client_type": foreach(var toi in toc) { toi.ClientType = ClientType; } break;
				case "client_input_muted": foreach(var toi in toc) { toi.InputMuted = InputMuted; } break;
				case "client_output_muted": foreach(var toi in toc) { toi.OutputMuted = OutputMuted; } break;
				case "client_outputonly_muted": foreach(var toi in toc) { toi.OutputOnlyMuted = OutputOnlyMuted; } break;
				case "client_input_hardware": foreach(var toi in toc) { toi.InputHardwareEnabled = InputHardwareEnabled; } break;
				case "client_output_hardware": foreach(var toi in toc) { toi.OutputHardwareEnabled = OutputHardwareEnabled; } break;
				case "client_meta_data": foreach(var toi in toc) { toi.Metadata = Metadata; } break;
				case "client_is_recording": foreach(var toi in toc) { toi.IsRecording = IsRecording; } break;
				case "client_channel_group_id": foreach(var toi in toc) { toi.ChannelGroup = ChannelGroup; } break;
				case "client_channel_group_inherited_channel_id": foreach(var toi in toc) { toi.InheritedChannelGroupFromChannel = InheritedChannelGroupFromChannel; } break;
				case "client_servergroups": foreach(var toi in toc) { toi.ServerGroups = ServerGroups; } break;
				case "client_away": foreach(var toi in toc) { toi.IsAway = IsAway; } break;
				case "client_away_message": foreach(var toi in toc) { toi.AwayMessage = AwayMessage; } break;
				case "client_talk_power": foreach(var toi in toc) { toi.TalkPower = TalkPower; } break;
				case "client_talk_request": foreach(var toi in toc) { toi.TalkPowerRequestTime = TalkPowerRequestTime; } break;
				case "client_talk_request_msg": foreach(var toi in toc) { toi.TalkPowerRequestMessage = TalkPowerRequestMessage; } break;
				case "client_is_talker": foreach(var toi in toc) { toi.TalkPowerGranted = TalkPowerGranted; } break;
				case "client_is_priority_speaker": foreach(var toi in toc) { toi.IsPrioritySpeaker = IsPrioritySpeaker; } break;
				case "client_unread_messages": foreach(var toi in toc) { toi.UnreadMessages = UnreadMessages; } break;
				case "client_nickname_phonetic": foreach(var toi in toc) { toi.PhoneticName = PhoneticName; } break;
				case "client_needed_serverquery_view_power": foreach(var toi in toc) { toi.NeededServerqueryViewPower = NeededServerqueryViewPower; } break;
				case "client_is_channel_commander": foreach(var toi in toc) { toi.IsChannelCommander = IsChannelCommander; } break;
				case "client_country": foreach(var toi in toc) { toi.CountryCode = CountryCode; } break;
				case "client_badges": foreach(var toi in toc) { toi.Badges = Badges; } break;
				case "client_created": foreach(var toi in toc) { toi.CreationDate = CreationDate; } break;
				case "client_lastconnected": foreach(var toi in toc) { toi.LastConnected = LastConnected; } break;
				case "client_totalconnections": foreach(var toi in toc) { toi.TotalConnections = TotalConnections; } break;
				case "client_month_bytes_uploaded": foreach(var toi in toc) { toi.MonthlyUploadQuota = MonthlyUploadQuota; } break;
				case "client_month_bytes_downloaded": foreach(var toi in toc) { toi.MonthlyDownloadQuota = MonthlyDownloadQuota; } break;
				case "client_total_bytes_uploaded": foreach(var toi in toc) { toi.TotalUploadQuota = TotalUploadQuota; } break;
				case "client_total_bytes_downloaded": foreach(var toi in toc) { toi.TotalDownloadQuota = TotalDownloadQuota; } break;
				case "client_base64HashClientUID": foreach(var toi in toc) { toi.Base64HashClientUid = Base64HashClientUid; } break;
				case "client_flag_avatar": foreach(var toi in toc) { toi.AvatarHash = AvatarHash; } break;
				case "client_description": foreach(var toi in toc) { toi.Description = Description; } break;
				case "client_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
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
			case "client_key_offset": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) ClientKeyOffset = oval; } break;
			case "client_nickname_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "client_default_token": DefaultToken = Ts3String.Unescape(value); break;
			case "hwid": HardwareId = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientInit[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "client_nickname": foreach(var toi in toc) { toi.Name = Name; } break;
				case "client_version": foreach(var toi in toc) { toi.ClientVersion = ClientVersion; } break;
				case "client_platform": foreach(var toi in toc) { toi.ClientPlattform = ClientPlattform; } break;
				case "client_input_hardware": foreach(var toi in toc) { toi.InputHardwareEnabled = InputHardwareEnabled; } break;
				case "client_output_hardware": foreach(var toi in toc) { toi.OutputHardwareEnabled = OutputHardwareEnabled; } break;
				case "client_default_channel": foreach(var toi in toc) { toi.DefaultChannel = DefaultChannel; } break;
				case "client_default_channel_password": foreach(var toi in toc) { toi.DefaultChannelPassword = DefaultChannelPassword; } break;
				case "client_server_password": foreach(var toi in toc) { toi.ServerPassword = ServerPassword; } break;
				case "client_meta_data": foreach(var toi in toc) { toi.Metadata = Metadata; } break;
				case "client_version_sign": foreach(var toi in toc) { toi.ClientVersionSign = ClientVersionSign; } break;
				case "client_key_offset": foreach(var toi in toc) { toi.ClientKeyOffset = ClientKeyOffset; } break;
				case "client_nickname_phonetic": foreach(var toi in toc) { toi.PhoneticName = PhoneticName; } break;
				case "client_default_token": foreach(var toi in toc) { toi.DefaultToken = DefaultToken; } break;
				case "hwid": foreach(var toi in toc) { toi.HardwareId = HardwareId; } break;
				}
			}

		}
	}

	public sealed class ClientInitIv : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientInitIv;
		

		public str Alpha { get; set; }
		public str Omega { get; set; }
		public str Ip { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "alpha": Alpha = Ts3String.Unescape(value); break;
			case "omega": Omega = Ts3String.Unescape(value); break;
			case "ip": Ip = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientInitIv[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "alpha": foreach(var toi in toc) { toi.Alpha = Alpha; } break;
				case "omega": foreach(var toi in toc) { toi.Omega = Omega; } break;
				case "ip": foreach(var toi in toc) { toi.Ip = Ip; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "reasonmsg": ReasonMessage = Ts3String.Unescape(value); break;
			case "bantime": { if(Utf8Parser.TryParse(value, out double oval, out _)) BanTime = TimeSpan.FromSeconds(oval); } break;
			case "reasonid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Reason = (Reason)oval; } break;
			case "ctid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) TargetChannelId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "cfid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) SourceChannelId = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientLeftView[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "reasonmsg": foreach(var toi in toc) { toi.ReasonMessage = ReasonMessage; } break;
				case "bantime": foreach(var toi in toc) { toi.BanTime = BanTime; } break;
				case "reasonid": foreach(var toi in toc) { toi.Reason = Reason; } break;
				case "ctid": foreach(var toi in toc) { toi.TargetChannelId = TargetChannelId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "cfid": foreach(var toi in toc) { toi.SourceChannelId = SourceChannelId; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "reasonid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Reason = (Reason)oval; } break;
			case "ctid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) TargetChannelId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientMoved[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "reasonid": foreach(var toi in toc) { toi.Reason = Reason; } break;
				case "ctid": foreach(var toi in toc) { toi.TargetChannelId = TargetChannelId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				}
			}

		}
	}

	public sealed class ClientNeededPermissions : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ClientNeededPermissions;
		

		public PermissionId PermissionId { get; set; }
		public i32 PermissionValue { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "permid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) PermissionId = (PermissionId)oval; } break;
			case "permvalue": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) PermissionValue = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientNeededPermissions[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "permid": foreach(var toi in toc) { toi.PermissionId = PermissionId; } break;
				case "permvalue": foreach(var toi in toc) { toi.PermissionValue = PermissionValue; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "msg": Message = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientPoke[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				case "msg": foreach(var toi in toc) { toi.Message = Message; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "name": Name = Ts3String.Unescape(value); break;
			case "sgid": { if(Utf8Parser.TryParse(value, out ServerGroupId oval, out _)) ServerGroupId = oval; } break;
			case "cldbid": { if(Utf8Parser.TryParse(value, out ClientDbId oval, out _)) ClientDbId = oval; } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientServerGroup[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "sgid": foreach(var toi in toc) { toi.ServerGroupId = ServerGroupId; } break;
				case "cldbid": foreach(var toi in toc) { toi.ClientDbId = ClientDbId; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "name": Name = Ts3String.Unescape(value); break;
			case "sgid": { if(Utf8Parser.TryParse(value, out ServerGroupId oval, out _)) ServerGroupId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ClientServerGroupAdded[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "sgid": foreach(var toi in toc) { toi.ServerGroupId = ServerGroupId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "cluid": foreach(var toi in toc) { toi.ClientUid = ClientUid; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "id": { if(Utf8Parser.TryParse(value, out u32 oval, out _)) Id = (Ts3ErrorCode)oval; } break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "failed_permid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) MissingPermissionId = (PermissionId)oval; } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			case "extra_msg": ExtraMessage = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (CommandError[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "id": foreach(var toi in toc) { toi.Id = Id; } break;
				case "msg": foreach(var toi in toc) { toi.Message = Message; } break;
				case "failed_permid": foreach(var toi in toc) { toi.MissingPermissionId = MissingPermissionId; } break;
				case "return_code": foreach(var toi in toc) { toi.ReturnCode = ReturnCode; } break;
				case "extra_msg": foreach(var toi in toc) { toi.ExtraMessage = ExtraMessage; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "connection_ping": { if(Utf8Parser.TryParse(value, out double oval, out _)) Ping = TimeSpan.FromMilliseconds(oval); } break;
			case "connection_ping_deviation": { if(Utf8Parser.TryParse(value, out double oval, out _)) PingDeviation = TimeSpan.FromMilliseconds(oval); } break;
			case "connection_connected_time": { if(Utf8Parser.TryParse(value, out double oval, out _)) ConnectedTime = TimeSpan.FromMilliseconds(oval); } break;
			case "connection_client_ip": Ip = Ts3String.Unescape(value); break;
			case "connection_client_port": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) Port = oval; } break;
			case "connection_packets_sent_speech": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) PacketsSentSpeech = oval; } break;
			case "connection_packets_sent_keepalive": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) PacketsSentKeepalive = oval; } break;
			case "connection_packets_sent_control": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) PacketsSentControl = oval; } break;
			case "connection_bytes_sent_speech": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BytesSentSpeech = oval; } break;
			case "connection_bytes_sent_keepalive": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BytesSentKeepalive = oval; } break;
			case "connection_bytes_sent_control": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BytesSentControl = oval; } break;
			case "connection_packets_received_speech": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) PacketsReceivedSpeech = oval; } break;
			case "connection_packets_received_keepalive": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) PacketsReceivedKeepalive = oval; } break;
			case "connection_packets_received_control": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) PacketsReceivedControl = oval; } break;
			case "connection_bytes_received_speech": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BytesReceivedSpeech = oval; } break;
			case "connection_bytes_received_keepalive": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BytesReceivedKeepalive = oval; } break;
			case "connection_bytes_received_control": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BytesReceivedControl = oval; } break;
			case "connection_server2client_packetloss_speech": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) ServerToClientPacketlossSpeech = oval; } break;
			case "connection_server2client_packetloss_keepalive": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) ServerToClientPacketlossKeepalive = oval; } break;
			case "connection_server2client_packetloss_control": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) ServerToClientPacketlossControl = oval; } break;
			case "connection_server2client_packetloss_total": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) ServerToClientPacketlossTotal = oval; } break;
			case "connection_client2server_packetloss_speech": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) ClientToServerPacketlossSpeech = oval; } break;
			case "connection_client2server_packetloss_keepalive": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) ClientToServerPacketlossKeepalive = oval; } break;
			case "connection_client2server_packetloss_control": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) ClientToServerPacketlossControl = oval; } break;
			case "connection_client2server_packetloss_total": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) ClientToServerPacketlossTotal = oval; } break;
			case "connection_bandwidth_sent_last_second_speech": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthSentLastSecondSpeech = oval; } break;
			case "connection_bandwidth_sent_last_second_keepalive": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthSentLastSecondKeepalive = oval; } break;
			case "connection_bandwidth_sent_last_second_control": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthSentLastSecondControl = oval; } break;
			case "connection_bandwidth_sent_last_minute_speech": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthSentLastMinuteSpeech = oval; } break;
			case "connection_bandwidth_sent_last_minute_keepalive": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthSentLastMinuteKeepalive = oval; } break;
			case "connection_bandwidth_sent_last_minute_control": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthSentLastMinuteControl = oval; } break;
			case "connection_bandwidth_received_last_second_speech": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthReceivedLastSecondSpeech = oval; } break;
			case "connection_bandwidth_received_last_second_keepalive": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthReceivedLastSecondKeepalive = oval; } break;
			case "connection_bandwidth_received_last_second_control": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthReceivedLastSecondControl = oval; } break;
			case "connection_bandwidth_received_last_minute_speech": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthReceivedLastMinuteSpeech = oval; } break;
			case "connection_bandwidth_received_last_minute_keepalive": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthReceivedLastMinuteKeepalive = oval; } break;
			case "connection_bandwidth_received_last_minute_control": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) BandwidthReceivedLastMinuteControl = oval; } break;
			case "connection_filetransfer_bandwidth_sent": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) FiletransferBandwidthSent = oval; } break;
			case "connection_filetransfer_bandwidth_received": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) FiletransferBandwidthReceived = oval; } break;
			case "connection_idle_time": { if(Utf8Parser.TryParse(value, out double oval, out _)) IdleTime = TimeSpan.FromMilliseconds(oval); } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ConnectionInfo[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "connection_ping": foreach(var toi in toc) { toi.Ping = Ping; } break;
				case "connection_ping_deviation": foreach(var toi in toc) { toi.PingDeviation = PingDeviation; } break;
				case "connection_connected_time": foreach(var toi in toc) { toi.ConnectedTime = ConnectedTime; } break;
				case "connection_client_ip": foreach(var toi in toc) { toi.Ip = Ip; } break;
				case "connection_client_port": foreach(var toi in toc) { toi.Port = Port; } break;
				case "connection_packets_sent_speech": foreach(var toi in toc) { toi.PacketsSentSpeech = PacketsSentSpeech; } break;
				case "connection_packets_sent_keepalive": foreach(var toi in toc) { toi.PacketsSentKeepalive = PacketsSentKeepalive; } break;
				case "connection_packets_sent_control": foreach(var toi in toc) { toi.PacketsSentControl = PacketsSentControl; } break;
				case "connection_bytes_sent_speech": foreach(var toi in toc) { toi.BytesSentSpeech = BytesSentSpeech; } break;
				case "connection_bytes_sent_keepalive": foreach(var toi in toc) { toi.BytesSentKeepalive = BytesSentKeepalive; } break;
				case "connection_bytes_sent_control": foreach(var toi in toc) { toi.BytesSentControl = BytesSentControl; } break;
				case "connection_packets_received_speech": foreach(var toi in toc) { toi.PacketsReceivedSpeech = PacketsReceivedSpeech; } break;
				case "connection_packets_received_keepalive": foreach(var toi in toc) { toi.PacketsReceivedKeepalive = PacketsReceivedKeepalive; } break;
				case "connection_packets_received_control": foreach(var toi in toc) { toi.PacketsReceivedControl = PacketsReceivedControl; } break;
				case "connection_bytes_received_speech": foreach(var toi in toc) { toi.BytesReceivedSpeech = BytesReceivedSpeech; } break;
				case "connection_bytes_received_keepalive": foreach(var toi in toc) { toi.BytesReceivedKeepalive = BytesReceivedKeepalive; } break;
				case "connection_bytes_received_control": foreach(var toi in toc) { toi.BytesReceivedControl = BytesReceivedControl; } break;
				case "connection_server2client_packetloss_speech": foreach(var toi in toc) { toi.ServerToClientPacketlossSpeech = ServerToClientPacketlossSpeech; } break;
				case "connection_server2client_packetloss_keepalive": foreach(var toi in toc) { toi.ServerToClientPacketlossKeepalive = ServerToClientPacketlossKeepalive; } break;
				case "connection_server2client_packetloss_control": foreach(var toi in toc) { toi.ServerToClientPacketlossControl = ServerToClientPacketlossControl; } break;
				case "connection_server2client_packetloss_total": foreach(var toi in toc) { toi.ServerToClientPacketlossTotal = ServerToClientPacketlossTotal; } break;
				case "connection_client2server_packetloss_speech": foreach(var toi in toc) { toi.ClientToServerPacketlossSpeech = ClientToServerPacketlossSpeech; } break;
				case "connection_client2server_packetloss_keepalive": foreach(var toi in toc) { toi.ClientToServerPacketlossKeepalive = ClientToServerPacketlossKeepalive; } break;
				case "connection_client2server_packetloss_control": foreach(var toi in toc) { toi.ClientToServerPacketlossControl = ClientToServerPacketlossControl; } break;
				case "connection_client2server_packetloss_total": foreach(var toi in toc) { toi.ClientToServerPacketlossTotal = ClientToServerPacketlossTotal; } break;
				case "connection_bandwidth_sent_last_second_speech": foreach(var toi in toc) { toi.BandwidthSentLastSecondSpeech = BandwidthSentLastSecondSpeech; } break;
				case "connection_bandwidth_sent_last_second_keepalive": foreach(var toi in toc) { toi.BandwidthSentLastSecondKeepalive = BandwidthSentLastSecondKeepalive; } break;
				case "connection_bandwidth_sent_last_second_control": foreach(var toi in toc) { toi.BandwidthSentLastSecondControl = BandwidthSentLastSecondControl; } break;
				case "connection_bandwidth_sent_last_minute_speech": foreach(var toi in toc) { toi.BandwidthSentLastMinuteSpeech = BandwidthSentLastMinuteSpeech; } break;
				case "connection_bandwidth_sent_last_minute_keepalive": foreach(var toi in toc) { toi.BandwidthSentLastMinuteKeepalive = BandwidthSentLastMinuteKeepalive; } break;
				case "connection_bandwidth_sent_last_minute_control": foreach(var toi in toc) { toi.BandwidthSentLastMinuteControl = BandwidthSentLastMinuteControl; } break;
				case "connection_bandwidth_received_last_second_speech": foreach(var toi in toc) { toi.BandwidthReceivedLastSecondSpeech = BandwidthReceivedLastSecondSpeech; } break;
				case "connection_bandwidth_received_last_second_keepalive": foreach(var toi in toc) { toi.BandwidthReceivedLastSecondKeepalive = BandwidthReceivedLastSecondKeepalive; } break;
				case "connection_bandwidth_received_last_second_control": foreach(var toi in toc) { toi.BandwidthReceivedLastSecondControl = BandwidthReceivedLastSecondControl; } break;
				case "connection_bandwidth_received_last_minute_speech": foreach(var toi in toc) { toi.BandwidthReceivedLastMinuteSpeech = BandwidthReceivedLastMinuteSpeech; } break;
				case "connection_bandwidth_received_last_minute_keepalive": foreach(var toi in toc) { toi.BandwidthReceivedLastMinuteKeepalive = BandwidthReceivedLastMinuteKeepalive; } break;
				case "connection_bandwidth_received_last_minute_control": foreach(var toi in toc) { toi.BandwidthReceivedLastMinuteControl = BandwidthReceivedLastMinuteControl; } break;
				case "connection_filetransfer_bandwidth_sent": foreach(var toi in toc) { toi.FiletransferBandwidthSent = FiletransferBandwidthSent; } break;
				case "connection_filetransfer_bandwidth_received": foreach(var toi in toc) { toi.FiletransferBandwidthReceived = FiletransferBandwidthReceived; } break;
				case "connection_idle_time": foreach(var toi in toc) { toi.IdleTime = IdleTime; } break;
				}
			}

		}
	}

	public sealed class ConnectionInfoRequest : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.ConnectionInfoRequest;
		


		public void SetField(string name, ReadOnlySpan<byte> value)
		{
		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "clientftfid": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) ClientFileTransferId = oval; } break;
			case "serverftfid": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) ServerFileTransferId = oval; } break;
			case "ftkey": FileTransferKey = Ts3String.Unescape(value); break;
			case "port": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) Port = oval; } break;
			case "size": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) Size = oval; } break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (FileDownload[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "clientftfid": foreach(var toi in toc) { toi.ClientFileTransferId = ClientFileTransferId; } break;
				case "serverftfid": foreach(var toi in toc) { toi.ServerFileTransferId = ServerFileTransferId; } break;
				case "ftkey": foreach(var toi in toc) { toi.FileTransferKey = FileTransferKey; } break;
				case "port": foreach(var toi in toc) { toi.Port = Port; } break;
				case "size": foreach(var toi in toc) { toi.Size = Size; } break;
				case "msg": foreach(var toi in toc) { toi.Message = Message; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "path": Path = Ts3String.Unescape(value); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "size": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) Size = oval; } break;
			case "datetime": { if(Utf8Parser.TryParse(value, out double oval, out _)) DateTime = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (FileInfoTs[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "path": foreach(var toi in toc) { toi.Path = Path; } break;
				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "size": foreach(var toi in toc) { toi.Size = Size; } break;
				case "datetime": foreach(var toi in toc) { toi.DateTime = DateTime; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "path": Path = Ts3String.Unescape(value); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "size": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) Size = oval; } break;
			case "datetime": { if(Utf8Parser.TryParse(value, out double oval, out _)) DateTime = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "type": IsFile = value.Length > 0 && value[0] != '0'; break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (FileList[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "path": foreach(var toi in toc) { toi.Path = Path; } break;
				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "size": foreach(var toi in toc) { toi.Size = Size; } break;
				case "datetime": foreach(var toi in toc) { toi.DateTime = DateTime; } break;
				case "type": foreach(var toi in toc) { toi.IsFile = IsFile; } break;
				}
			}

		}
	}

	public sealed class FileListFinished : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.FileListFinished;
		

		public ChannelId ChannelId { get; set; }
		public str Path { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cid": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "path": Path = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (FileListFinished[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cid": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "path": foreach(var toi in toc) { toi.Path = Path; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "path": Path = Ts3String.Unescape(value); break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "size": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) Size = oval; } break;
			case "sizedone": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) SizeDone = oval; } break;
			case "clientftfid": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) ClientFileTransferId = oval; } break;
			case "serverftfid": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) ServerFileTransferId = oval; } break;
			case "sender": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) Sender = oval; } break;
			case "status": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Status = oval; } break;
			case "current_speed": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) CurrentSpeed = oval; } break;
			case "average_speed": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) AverageSpeed = oval; } break;
			case "runtime": { if(Utf8Parser.TryParse(value, out double oval, out _)) Runtime = TimeSpan.FromSeconds(oval); } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (FileTransfer[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "path": foreach(var toi in toc) { toi.Path = Path; } break;
				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "size": foreach(var toi in toc) { toi.Size = Size; } break;
				case "sizedone": foreach(var toi in toc) { toi.SizeDone = SizeDone; } break;
				case "clientftfid": foreach(var toi in toc) { toi.ClientFileTransferId = ClientFileTransferId; } break;
				case "serverftfid": foreach(var toi in toc) { toi.ServerFileTransferId = ServerFileTransferId; } break;
				case "sender": foreach(var toi in toc) { toi.Sender = Sender; } break;
				case "status": foreach(var toi in toc) { toi.Status = Status; } break;
				case "current_speed": foreach(var toi in toc) { toi.CurrentSpeed = CurrentSpeed; } break;
				case "average_speed": foreach(var toi in toc) { toi.AverageSpeed = AverageSpeed; } break;
				case "runtime": foreach(var toi in toc) { toi.Runtime = Runtime; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "clientftfid": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) ClientFileTransferId = oval; } break;
			case "status": { if(Utf8Parser.TryParse(value, out u32 oval, out _)) Status = (Ts3ErrorCode)oval; } break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "size": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) Size = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (FileTransferStatus[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "clientftfid": foreach(var toi in toc) { toi.ClientFileTransferId = ClientFileTransferId; } break;
				case "status": foreach(var toi in toc) { toi.Status = Status; } break;
				case "msg": foreach(var toi in toc) { toi.Message = Message; } break;
				case "size": foreach(var toi in toc) { toi.Size = Size; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "clientftfid": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) ClientFileTransferId = oval; } break;
			case "serverftfid": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) ServerFileTransferId = oval; } break;
			case "ftkey": FileTransferKey = Ts3String.Unescape(value); break;
			case "port": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) Port = oval; } break;
			case "seekpos": { if(Utf8Parser.TryParse(value, out i64 oval, out _)) SeekPosistion = oval; } break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (FileUpload[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "clientftfid": foreach(var toi in toc) { toi.ClientFileTransferId = ClientFileTransferId; } break;
				case "serverftfid": foreach(var toi in toc) { toi.ServerFileTransferId = ServerFileTransferId; } break;
				case "ftkey": foreach(var toi in toc) { toi.FileTransferKey = FileTransferKey; } break;
				case "port": foreach(var toi in toc) { toi.Port = Port; } break;
				case "seekpos": foreach(var toi in toc) { toi.SeekPosistion = SeekPosistion; } break;
				case "msg": foreach(var toi in toc) { toi.Message = Message; } break;
				}
			}

		}
	}

	public sealed class GetClientDbIdFromUid : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.GetClientDbIdFromUid;
		

		public Uid ClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (GetClientDbIdFromUid[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cluid": foreach(var toi in toc) { toi.ClientUid = ClientUid; } break;
				}
			}

		}
	}

	public sealed class GetClientIds : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.GetClientIds;
		

		public Uid ClientUid { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (GetClientIds[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "cluid": foreach(var toi in toc) { toi.ClientUid = ClientUid; } break;
				}
			}

		}
	}

	public sealed class InitIvExpand : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.InitIvExpand;
		

		public str Alpha { get; set; }
		public str Beta { get; set; }
		public str Omega { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "alpha": Alpha = Ts3String.Unescape(value); break;
			case "beta": Beta = Ts3String.Unescape(value); break;
			case "omega": Omega = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (InitIvExpand[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "alpha": foreach(var toi in toc) { toi.Alpha = Alpha; } break;
				case "beta": foreach(var toi in toc) { toi.Beta = Beta; } break;
				case "omega": foreach(var toi in toc) { toi.Omega = Omega; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
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

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (InitIvExpand2[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "l": foreach(var toi in toc) { toi.License = License; } break;
				case "beta": foreach(var toi in toc) { toi.Beta = Beta; } break;
				case "omega": foreach(var toi in toc) { toi.Omega = Omega; } break;
				case "ot": foreach(var toi in toc) { toi.Ot = Ot; } break;
				case "proof": foreach(var toi in toc) { toi.Proof = Proof; } break;
				case "tvd": foreach(var toi in toc) { toi.Tvd = Tvd; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "virtualserver_welcomemessage": WelcomeMessage = Ts3String.Unescape(value); break;
			case "virtualserver_platform": ServerPlatform = Ts3String.Unescape(value); break;
			case "virtualserver_version": ServerVersion = Ts3String.Unescape(value); break;
			case "virtualserver_maxclients": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) MaxClients = oval; } break;
			case "virtualserver_created": { if(Utf8Parser.TryParse(value, out double oval, out _)) ServerCreated = Util.UnixTimeStart.AddSeconds(oval); } break;
			case "virtualserver_hostmessage": Hostmessage = Ts3String.Unescape(value); break;
			case "virtualserver_hostmessage_mode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) HostmessageMode = (HostMessageMode)oval; } break;
			case "virtualserver_id": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) VirtualServerId = oval; } break;
			case "virtualserver_ip": { if(value.Length == 0) ServerIp = Array.Empty<str>(); else { var ss = new SpanSplitter<byte>(); ss.First(value, (byte)','); int cnt = 0; for (int i = 0; i < value.Length; i++) if (value[i] == ',') cnt++; ServerIp = new str[cnt + 1]; for(int i = 0; i < cnt + 1; i++) { ServerIp[i] = Ts3String.Unescape(ss.Trim(value)); if (i < cnt) value = ss.Next(value); } } } break;
			case "virtualserver_ask_for_privilegekey": AskForPrivilegekey = value.Length > 0 && value[0] != '0'; break;
			case "acn": ClientName = Ts3String.Unescape(value); break;
			case "aclid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "pv": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) ProtocolVersion = oval; } break;
			case "lt": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) LicenseType = (LicenseType)oval; } break;
			case "client_talk_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) TalkPower = oval; } break;
			case "client_needed_serverquery_view_power": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededServerqueryViewPower = oval; } break;
			case "virtualserver_name": Name = Ts3String.Unescape(value); break;
			case "virtualserver_codec_encryption_mode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) CodecEncryptionMode = (CodecEncryptionMode)oval; } break;
			case "virtualserver_default_server_group": { if(Utf8Parser.TryParse(value, out ServerGroupId oval, out _)) DefaultServerGroup = oval; } break;
			case "virtualserver_default_channel_group": { if(Utf8Parser.TryParse(value, out ChannelGroupId oval, out _)) DefaultChannelGroup = oval; } break;
			case "virtualserver_hostbanner_url": HostbannerUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbanner_gfx_url": HostbannerGfxUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbanner_gfx_interval": { if(Utf8Parser.TryParse(value, out double oval, out _)) HostbannerGfxInterval = TimeSpan.FromSeconds(oval); } break;
			case "virtualserver_priority_speaker_dimm_modificator": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) PrioritySpeakerDimmModificator = oval; } break;
			case "virtualserver_hostbutton_tooltip": HostbuttonTooltip = Ts3String.Unescape(value); break;
			case "virtualserver_hostbutton_url": HostbuttonUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbutton_gfx_url": HostbuttonGfxUrl = Ts3String.Unescape(value); break;
			case "virtualserver_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "virtualserver_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "virtualserver_hostbanner_mode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) HostbannerMode = (HostBannerMode)oval; } break;
			case "virtualserver_channel_temp_delete_delay_default": { if(Utf8Parser.TryParse(value, out double oval, out _)) TempChannelDefaultDeleteDelay = TimeSpan.FromSeconds(oval); } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (InitServer[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "virtualserver_welcomemessage": foreach(var toi in toc) { toi.WelcomeMessage = WelcomeMessage; } break;
				case "virtualserver_platform": foreach(var toi in toc) { toi.ServerPlatform = ServerPlatform; } break;
				case "virtualserver_version": foreach(var toi in toc) { toi.ServerVersion = ServerVersion; } break;
				case "virtualserver_maxclients": foreach(var toi in toc) { toi.MaxClients = MaxClients; } break;
				case "virtualserver_created": foreach(var toi in toc) { toi.ServerCreated = ServerCreated; } break;
				case "virtualserver_hostmessage": foreach(var toi in toc) { toi.Hostmessage = Hostmessage; } break;
				case "virtualserver_hostmessage_mode": foreach(var toi in toc) { toi.HostmessageMode = HostmessageMode; } break;
				case "virtualserver_id": foreach(var toi in toc) { toi.VirtualServerId = VirtualServerId; } break;
				case "virtualserver_ip": foreach(var toi in toc) { toi.ServerIp = ServerIp; } break;
				case "virtualserver_ask_for_privilegekey": foreach(var toi in toc) { toi.AskForPrivilegekey = AskForPrivilegekey; } break;
				case "acn": foreach(var toi in toc) { toi.ClientName = ClientName; } break;
				case "aclid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "pv": foreach(var toi in toc) { toi.ProtocolVersion = ProtocolVersion; } break;
				case "lt": foreach(var toi in toc) { toi.LicenseType = LicenseType; } break;
				case "client_talk_power": foreach(var toi in toc) { toi.TalkPower = TalkPower; } break;
				case "client_needed_serverquery_view_power": foreach(var toi in toc) { toi.NeededServerqueryViewPower = NeededServerqueryViewPower; } break;
				case "virtualserver_name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "virtualserver_codec_encryption_mode": foreach(var toi in toc) { toi.CodecEncryptionMode = CodecEncryptionMode; } break;
				case "virtualserver_default_server_group": foreach(var toi in toc) { toi.DefaultServerGroup = DefaultServerGroup; } break;
				case "virtualserver_default_channel_group": foreach(var toi in toc) { toi.DefaultChannelGroup = DefaultChannelGroup; } break;
				case "virtualserver_hostbanner_url": foreach(var toi in toc) { toi.HostbannerUrl = HostbannerUrl; } break;
				case "virtualserver_hostbanner_gfx_url": foreach(var toi in toc) { toi.HostbannerGfxUrl = HostbannerGfxUrl; } break;
				case "virtualserver_hostbanner_gfx_interval": foreach(var toi in toc) { toi.HostbannerGfxInterval = HostbannerGfxInterval; } break;
				case "virtualserver_priority_speaker_dimm_modificator": foreach(var toi in toc) { toi.PrioritySpeakerDimmModificator = PrioritySpeakerDimmModificator; } break;
				case "virtualserver_hostbutton_tooltip": foreach(var toi in toc) { toi.HostbuttonTooltip = HostbuttonTooltip; } break;
				case "virtualserver_hostbutton_url": foreach(var toi in toc) { toi.HostbuttonUrl = HostbuttonUrl; } break;
				case "virtualserver_hostbutton_gfx_url": foreach(var toi in toc) { toi.HostbuttonGfxUrl = HostbuttonGfxUrl; } break;
				case "virtualserver_name_phonetic": foreach(var toi in toc) { toi.PhoneticName = PhoneticName; } break;
				case "virtualserver_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "virtualserver_hostbanner_mode": foreach(var toi in toc) { toi.HostbannerMode = HostbannerMode; } break;
				case "virtualserver_channel_temp_delete_delay_default": foreach(var toi in toc) { toi.TempChannelDefaultDeleteDelay = TempChannelDefaultDeleteDelay; } break;
				}
			}

		}
	}

	public sealed class PluginCommand : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.PluginCommand;
		

		public str Name { get; set; }
		public str Data { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "name": Name = Ts3String.Unescape(value); break;
			case "data": Data = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (PluginCommand[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "data": foreach(var toi in toc) { toi.Data = Data; } break;
				}
			}

		}
	}

	public sealed class PluginCommandRequest : INotification
	{
		public NotificationType NotifyType { get; } = NotificationType.PluginCommandRequest;
		

		public str Name { get; set; }
		public str Data { get; set; }
		public i32 Target { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "name": Name = Ts3String.Unescape(value); break;
			case "data": Data = Ts3String.Unescape(value); break;
			case "targetmode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Target = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (PluginCommandRequest[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "data": foreach(var toi in toc) { toi.Data = Data; } break;
				case "targetmode": foreach(var toi in toc) { toi.Target = Target; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "virtualserver_clientsonline": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) ClientsOnline = oval; } break;
			case "virtualserver_queryclientsonline": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) QueriesOnline = oval; } break;
			case "virtualserver_maxclients": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) MaxClients = oval; } break;
			case "virtualserver_uptime": { if(Utf8Parser.TryParse(value, out double oval, out _)) Uptime = TimeSpan.FromSeconds(oval); } break;
			case "virtualserver_autostart": Autostart = value.Length > 0 && value[0] != '0'; break;
			case "virtualserver_machine_id": MachineId = Ts3String.Unescape(value); break;
			case "virtualserver_name": Name = Ts3String.Unescape(value); break;
			case "virtualserver_id": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) VirtualServerId = oval; } break;
			case "virtualserver_unique_identifier": VirtualServerUid = Ts3String.Unescape(value); break;
			case "virtualserver_port": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) VirtualServerPort = oval; } break;
			case "virtualserver_status": VirtualServerStatus = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ServerData[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "virtualserver_clientsonline": foreach(var toi in toc) { toi.ClientsOnline = ClientsOnline; } break;
				case "virtualserver_queryclientsonline": foreach(var toi in toc) { toi.QueriesOnline = QueriesOnline; } break;
				case "virtualserver_maxclients": foreach(var toi in toc) { toi.MaxClients = MaxClients; } break;
				case "virtualserver_uptime": foreach(var toi in toc) { toi.Uptime = Uptime; } break;
				case "virtualserver_autostart": foreach(var toi in toc) { toi.Autostart = Autostart; } break;
				case "virtualserver_machine_id": foreach(var toi in toc) { toi.MachineId = MachineId; } break;
				case "virtualserver_name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "virtualserver_id": foreach(var toi in toc) { toi.VirtualServerId = VirtualServerId; } break;
				case "virtualserver_unique_identifier": foreach(var toi in toc) { toi.VirtualServerUid = VirtualServerUid; } break;
				case "virtualserver_port": foreach(var toi in toc) { toi.VirtualServerPort = VirtualServerPort; } break;
				case "virtualserver_status": foreach(var toi in toc) { toi.VirtualServerStatus = VirtualServerStatus; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			case "reasonid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Reason = (Reason)oval; } break;
			case "virtualserver_name": Name = Ts3String.Unescape(value); break;
			case "virtualserver_codec_encryption_mode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) CodecEncryptionMode = (CodecEncryptionMode)oval; } break;
			case "virtualserver_default_server_group": { if(Utf8Parser.TryParse(value, out ServerGroupId oval, out _)) DefaultServerGroup = oval; } break;
			case "virtualserver_default_channel_group": { if(Utf8Parser.TryParse(value, out ChannelGroupId oval, out _)) DefaultChannelGroup = oval; } break;
			case "virtualserver_hostbanner_url": HostbannerUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbanner_gfx_url": HostbannerGfxUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbanner_gfx_interval": { if(Utf8Parser.TryParse(value, out double oval, out _)) HostbannerGfxInterval = TimeSpan.FromSeconds(oval); } break;
			case "virtualserver_priority_speaker_dimm_modificator": { if(Utf8Parser.TryParse(value, out f32 oval, out _)) PrioritySpeakerDimmModificator = oval; } break;
			case "virtualserver_hostbutton_tooltip": HostbuttonTooltip = Ts3String.Unescape(value); break;
			case "virtualserver_hostbutton_url": HostbuttonUrl = Ts3String.Unescape(value); break;
			case "virtualserver_hostbutton_gfx_url": HostbuttonGfxUrl = Ts3String.Unescape(value); break;
			case "virtualserver_name_phonetic": PhoneticName = Ts3String.Unescape(value); break;
			case "virtualserver_icon_id": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "virtualserver_hostbanner_mode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) HostbannerMode = (HostBannerMode)oval; } break;
			case "virtualserver_channel_temp_delete_delay_default": { if(Utf8Parser.TryParse(value, out double oval, out _)) TempChannelDefaultDeleteDelay = TimeSpan.FromSeconds(oval); } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ServerEdited[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				case "reasonid": foreach(var toi in toc) { toi.Reason = Reason; } break;
				case "virtualserver_name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "virtualserver_codec_encryption_mode": foreach(var toi in toc) { toi.CodecEncryptionMode = CodecEncryptionMode; } break;
				case "virtualserver_default_server_group": foreach(var toi in toc) { toi.DefaultServerGroup = DefaultServerGroup; } break;
				case "virtualserver_default_channel_group": foreach(var toi in toc) { toi.DefaultChannelGroup = DefaultChannelGroup; } break;
				case "virtualserver_hostbanner_url": foreach(var toi in toc) { toi.HostbannerUrl = HostbannerUrl; } break;
				case "virtualserver_hostbanner_gfx_url": foreach(var toi in toc) { toi.HostbannerGfxUrl = HostbannerGfxUrl; } break;
				case "virtualserver_hostbanner_gfx_interval": foreach(var toi in toc) { toi.HostbannerGfxInterval = HostbannerGfxInterval; } break;
				case "virtualserver_priority_speaker_dimm_modificator": foreach(var toi in toc) { toi.PrioritySpeakerDimmModificator = PrioritySpeakerDimmModificator; } break;
				case "virtualserver_hostbutton_tooltip": foreach(var toi in toc) { toi.HostbuttonTooltip = HostbuttonTooltip; } break;
				case "virtualserver_hostbutton_url": foreach(var toi in toc) { toi.HostbuttonUrl = HostbuttonUrl; } break;
				case "virtualserver_hostbutton_gfx_url": foreach(var toi in toc) { toi.HostbuttonGfxUrl = HostbuttonGfxUrl; } break;
				case "virtualserver_name_phonetic": foreach(var toi in toc) { toi.PhoneticName = PhoneticName; } break;
				case "virtualserver_icon_id": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "virtualserver_hostbanner_mode": foreach(var toi in toc) { toi.HostbannerMode = HostbannerMode; } break;
				case "virtualserver_channel_temp_delete_delay_default": foreach(var toi in toc) { toi.TempChannelDefaultDeleteDelay = TempChannelDefaultDeleteDelay; } break;
				}
			}

		}
	}

	public sealed class ServerGroupAddResponse : IResponse
	{
		
		public string ReturnCode { get; set; }

		public ServerGroupId ServerGroupId { get; set; }

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "sgid": { if(Utf8Parser.TryParse(value, out ServerGroupId oval, out _)) ServerGroupId = oval; } break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ServerGroupAddResponse[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "sgid": foreach(var toi in toc) { toi.ServerGroupId = ServerGroupId; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "sgid": { if(Utf8Parser.TryParse(value, out ServerGroupId oval, out _)) ServerGroupId = oval; } break;
			case "name": Name = Ts3String.Unescape(value); break;
			case "type": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) GroupType = (GroupType)oval; } break;
			case "iconid": { if(Utf8Parser.TryParse(value, out long oval, out _)) IconId = unchecked((int)oval); } break;
			case "savedb": IsPermanent = value.Length > 0 && value[0] != '0'; break;
			case "sortid": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) SortId = oval; } break;
			case "namemode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NamingMode = (GroupNamingMode)oval; } break;
			case "n_modifyp": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededModifyPower = oval; } break;
			case "n_member_addp": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededMemberAddPower = oval; } break;
			case "n_member_remove_p": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) NeededMemberRemovePower = oval; } break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (ServerGroupList[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "sgid": foreach(var toi in toc) { toi.ServerGroupId = ServerGroupId; } break;
				case "name": foreach(var toi in toc) { toi.Name = Name; } break;
				case "type": foreach(var toi in toc) { toi.GroupType = GroupType; } break;
				case "iconid": foreach(var toi in toc) { toi.IconId = IconId; } break;
				case "savedb": foreach(var toi in toc) { toi.IsPermanent = IsPermanent; } break;
				case "sortid": foreach(var toi in toc) { toi.SortId = SortId; } break;
				case "namemode": foreach(var toi in toc) { toi.NamingMode = NamingMode; } break;
				case "n_modifyp": foreach(var toi in toc) { toi.NeededModifyPower = NeededModifyPower; } break;
				case "n_member_addp": foreach(var toi in toc) { toi.NeededMemberAddPower = NeededMemberAddPower; } break;
				case "n_member_remove_p": foreach(var toi in toc) { toi.NeededMemberRemovePower = NeededMemberRemovePower; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "targetmode": { if(Utf8Parser.TryParse(value, out i32 oval, out _)) Target = (TextMessageTargetMode)oval; } break;
			case "msg": Message = Ts3String.Unescape(value); break;
			case "target": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) TargetClientId = oval; } break;
			case "invokerid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) InvokerId = oval; } break;
			case "invokername": InvokerName = Ts3String.Unescape(value); break;
			case "invokeruid": InvokerUid = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (TextMessage[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "targetmode": foreach(var toi in toc) { toi.Target = Target; } break;
				case "msg": foreach(var toi in toc) { toi.Message = Message; } break;
				case "target": foreach(var toi in toc) { toi.TargetClientId = TargetClientId; } break;
				case "invokerid": foreach(var toi in toc) { toi.InvokerId = InvokerId; } break;
				case "invokername": foreach(var toi in toc) { toi.InvokerName = InvokerName; } break;
				case "invokeruid": foreach(var toi in toc) { toi.InvokerUid = InvokerUid; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "token": UsedToken = Ts3String.Unescape(value); break;
			case "tokencustomset": TokenCustomSet = Ts3String.Unescape(value); break;
			case "token1": Token1 = Ts3String.Unescape(value); break;
			case "token2": Token2 = Ts3String.Unescape(value); break;
			case "clid": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "cldbid": { if(Utf8Parser.TryParse(value, out ClientDbId oval, out _)) ClientDbId = oval; } break;
			case "cluid": ClientUid = Ts3String.Unescape(value); break;
			
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (TokenUsed[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "token": foreach(var toi in toc) { toi.UsedToken = UsedToken; } break;
				case "tokencustomset": foreach(var toi in toc) { toi.TokenCustomSet = TokenCustomSet; } break;
				case "token1": foreach(var toi in toc) { toi.Token1 = Token1; } break;
				case "token2": foreach(var toi in toc) { toi.Token2 = Token2; } break;
				case "clid": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "cldbid": foreach(var toi in toc) { toi.ClientDbId = ClientDbId; } break;
				case "cluid": foreach(var toi in toc) { toi.ClientUid = ClientUid; } break;
				}
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

		public void SetField(string name, ReadOnlySpan<byte> value)
		{
			switch(name)
			{

			case "client_id": { if(Utf8Parser.TryParse(value, out ClientId oval, out _)) ClientId = oval; } break;
			case "client_channel_id": { if(Utf8Parser.TryParse(value, out ChannelId oval, out _)) ChannelId = oval; } break;
			case "client_nickname": Name = Ts3String.Unescape(value); break;
			case "client_database_id": { if(Utf8Parser.TryParse(value, out ClientDbId oval, out _)) DatabaseId = oval; } break;
			case "client_login_name": LoginName = Ts3String.Unescape(value); break;
			case "client_origin_server_id": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) OriginServerId = oval; } break;
			case "virtualserver_id": { if(Utf8Parser.TryParse(value, out u64 oval, out _)) VirtualServerId = oval; } break;
			case "virtualserver_unique_identifier": VirtualServerUid = Ts3String.Unescape(value); break;
			case "virtualserver_port": { if(Utf8Parser.TryParse(value, out u16 oval, out _)) VirtualServerPort = oval; } break;
			case "virtualserver_status": VirtualServerStatus = Ts3String.Unescape(value); break;
			case "client_unique_identifier": Uid = Ts3String.Unescape(value); break;
			case "return_code": ReturnCode = Ts3String.Unescape(value); break;
			}

		}

		public void Expand(IMessage[] to, IEnumerable<string> flds)
		{
			var toc = (WhoAmI[])to;
			foreach (var fld in flds)
			{
				switch(fld)
				{

				case "client_id": foreach(var toi in toc) { toi.ClientId = ClientId; } break;
				case "client_channel_id": foreach(var toi in toc) { toi.ChannelId = ChannelId; } break;
				case "client_nickname": foreach(var toi in toc) { toi.Name = Name; } break;
				case "client_database_id": foreach(var toi in toc) { toi.DatabaseId = DatabaseId; } break;
				case "client_login_name": foreach(var toi in toc) { toi.LoginName = LoginName; } break;
				case "client_origin_server_id": foreach(var toi in toc) { toi.OriginServerId = OriginServerId; } break;
				case "virtualserver_id": foreach(var toi in toc) { toi.VirtualServerId = VirtualServerId; } break;
				case "virtualserver_unique_identifier": foreach(var toi in toc) { toi.VirtualServerUid = VirtualServerUid; } break;
				case "virtualserver_port": foreach(var toi in toc) { toi.VirtualServerPort = VirtualServerPort; } break;
				case "virtualserver_status": foreach(var toi in toc) { toi.VirtualServerStatus = VirtualServerStatus; } break;
				case "client_unique_identifier": foreach(var toi in toc) { toi.Uid = Uid; } break;
				}
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

		public static INotification[] InstatiateNotificationArray(NotificationType name, int len)
		{
			switch(name)
			{
			case NotificationType.ChannelChanged: { var arr = new ChannelChanged[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelChanged(); return arr; }
			case NotificationType.ChannelCreated: { var arr = new ChannelCreated[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelCreated(); return arr; }
			case NotificationType.ChannelDeleted: { var arr = new ChannelDeleted[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelDeleted(); return arr; }
			case NotificationType.ChannelEdited: { var arr = new ChannelEdited[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelEdited(); return arr; }
			case NotificationType.ChannelGroupList: { var arr = new ChannelGroupList[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelGroupList(); return arr; }
			case NotificationType.ChannelList: { var arr = new ChannelList[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelList(); return arr; }
			case NotificationType.ChannelListFinished: { var arr = new ChannelListFinished[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelListFinished(); return arr; }
			case NotificationType.ChannelMoved: { var arr = new ChannelMoved[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelMoved(); return arr; }
			case NotificationType.ChannelPasswordChanged: { var arr = new ChannelPasswordChanged[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelPasswordChanged(); return arr; }
			case NotificationType.ChannelSubscribed: { var arr = new ChannelSubscribed[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelSubscribed(); return arr; }
			case NotificationType.ChannelUnsubscribed: { var arr = new ChannelUnsubscribed[len]; for (int i = 0; i < len; i++) arr[i] = new ChannelUnsubscribed(); return arr; }
			case NotificationType.ClientChannelGroupChanged: { var arr = new ClientChannelGroupChanged[len]; for (int i = 0; i < len; i++) arr[i] = new ClientChannelGroupChanged(); return arr; }
			case NotificationType.ClientChatComposing: { var arr = new ClientChatComposing[len]; for (int i = 0; i < len; i++) arr[i] = new ClientChatComposing(); return arr; }
			case NotificationType.ClientDbIdFromUid: { var arr = new ClientDbIdFromUid[len]; for (int i = 0; i < len; i++) arr[i] = new ClientDbIdFromUid(); return arr; }
			case NotificationType.ClientEnterView: { var arr = new ClientEnterView[len]; for (int i = 0; i < len; i++) arr[i] = new ClientEnterView(); return arr; }
			case NotificationType.ClientIds: { var arr = new ClientIds[len]; for (int i = 0; i < len; i++) arr[i] = new ClientIds(); return arr; }
			case NotificationType.ClientInit: { var arr = new ClientInit[len]; for (int i = 0; i < len; i++) arr[i] = new ClientInit(); return arr; }
			case NotificationType.ClientInitIv: { var arr = new ClientInitIv[len]; for (int i = 0; i < len; i++) arr[i] = new ClientInitIv(); return arr; }
			case NotificationType.ClientLeftView: { var arr = new ClientLeftView[len]; for (int i = 0; i < len; i++) arr[i] = new ClientLeftView(); return arr; }
			case NotificationType.ClientMoved: { var arr = new ClientMoved[len]; for (int i = 0; i < len; i++) arr[i] = new ClientMoved(); return arr; }
			case NotificationType.ClientNeededPermissions: { var arr = new ClientNeededPermissions[len]; for (int i = 0; i < len; i++) arr[i] = new ClientNeededPermissions(); return arr; }
			case NotificationType.ClientPoke: { var arr = new ClientPoke[len]; for (int i = 0; i < len; i++) arr[i] = new ClientPoke(); return arr; }
			case NotificationType.ClientServerGroup: { var arr = new ClientServerGroup[len]; for (int i = 0; i < len; i++) arr[i] = new ClientServerGroup(); return arr; }
			case NotificationType.ClientServerGroupAdded: { var arr = new ClientServerGroupAdded[len]; for (int i = 0; i < len; i++) arr[i] = new ClientServerGroupAdded(); return arr; }
			case NotificationType.CommandError: { var arr = new CommandError[len]; for (int i = 0; i < len; i++) arr[i] = new CommandError(); return arr; }
			case NotificationType.ConnectionInfo: { var arr = new ConnectionInfo[len]; for (int i = 0; i < len; i++) arr[i] = new ConnectionInfo(); return arr; }
			case NotificationType.ConnectionInfoRequest: { var arr = new ConnectionInfoRequest[len]; for (int i = 0; i < len; i++) arr[i] = new ConnectionInfoRequest(); return arr; }
			case NotificationType.FileDownload: { var arr = new FileDownload[len]; for (int i = 0; i < len; i++) arr[i] = new FileDownload(); return arr; }
			case NotificationType.FileInfoTs: { var arr = new FileInfoTs[len]; for (int i = 0; i < len; i++) arr[i] = new FileInfoTs(); return arr; }
			case NotificationType.FileList: { var arr = new FileList[len]; for (int i = 0; i < len; i++) arr[i] = new FileList(); return arr; }
			case NotificationType.FileListFinished: { var arr = new FileListFinished[len]; for (int i = 0; i < len; i++) arr[i] = new FileListFinished(); return arr; }
			case NotificationType.FileTransfer: { var arr = new FileTransfer[len]; for (int i = 0; i < len; i++) arr[i] = new FileTransfer(); return arr; }
			case NotificationType.FileTransferStatus: { var arr = new FileTransferStatus[len]; for (int i = 0; i < len; i++) arr[i] = new FileTransferStatus(); return arr; }
			case NotificationType.FileUpload: { var arr = new FileUpload[len]; for (int i = 0; i < len; i++) arr[i] = new FileUpload(); return arr; }
			case NotificationType.GetClientDbIdFromUid: { var arr = new GetClientDbIdFromUid[len]; for (int i = 0; i < len; i++) arr[i] = new GetClientDbIdFromUid(); return arr; }
			case NotificationType.GetClientIds: { var arr = new GetClientIds[len]; for (int i = 0; i < len; i++) arr[i] = new GetClientIds(); return arr; }
			case NotificationType.InitIvExpand: { var arr = new InitIvExpand[len]; for (int i = 0; i < len; i++) arr[i] = new InitIvExpand(); return arr; }
			case NotificationType.InitIvExpand2: { var arr = new InitIvExpand2[len]; for (int i = 0; i < len; i++) arr[i] = new InitIvExpand2(); return arr; }
			case NotificationType.InitServer: { var arr = new InitServer[len]; for (int i = 0; i < len; i++) arr[i] = new InitServer(); return arr; }
			case NotificationType.PluginCommand: { var arr = new PluginCommand[len]; for (int i = 0; i < len; i++) arr[i] = new PluginCommand(); return arr; }
			case NotificationType.PluginCommandRequest: { var arr = new PluginCommandRequest[len]; for (int i = 0; i < len; i++) arr[i] = new PluginCommandRequest(); return arr; }
			case NotificationType.ServerEdited: { var arr = new ServerEdited[len]; for (int i = 0; i < len; i++) arr[i] = new ServerEdited(); return arr; }
			case NotificationType.ServerGroupList: { var arr = new ServerGroupList[len]; for (int i = 0; i < len; i++) arr[i] = new ServerGroupList(); return arr; }
			case NotificationType.TextMessage: { var arr = new TextMessage[len]; for (int i = 0; i < len; i++) arr[i] = new TextMessage(); return arr; }
			case NotificationType.TokenUsed: { var arr = new TokenUsed[len]; for (int i = 0; i < len; i++) arr[i] = new TokenUsed(); return arr; }
			case NotificationType.Unknown:
			default: throw Util.UnhandledDefault(name);
			}
		}
	}
}