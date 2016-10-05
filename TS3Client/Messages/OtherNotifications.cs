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
	public interface IServerName
	{
		[QuerySerialized("virtualserver_name")]
		string ServerName { get; set; }
	}

	[QuerySubInterface]
	public interface ServerBaseData2
	{
		[QuerySerialized("virtualserver_codec_encryption_mode")]
		CodecEncryptionMode CodecEncryptionMode { get; set; }

		[QuerySerialized("virtualserver_default_server_group")]
		ulong DefaultServerGroup { get; set; }

		[QuerySerialized("virtualserver_default_channel_group")]
		ulong DefaultChannelGroup { get; set; }

		[QuerySerialized("virtualserver_hostbanner_url")]
		string HostbannerUrl { get; set; }

		[QuerySerialized("virtualserver_hostbanner_gfx_url")]
		string HostbannerGfxUrl { get; set; }

		[QuerySerialized("virtualserver_hostbanner_gfx_interval")]
		TimeSpan HostbannerGfxInterval { get; set; }

		[QuerySerialized("virtualserver_priority_speaker_dimm_modificator")]
		float PrioritySpeakerDimmModificator { get; set; }

		[QuerySerialized("virtualserver_hostbutton_tooltip")]
		string HostbuttonTooltip { get; set; }

		[QuerySerialized("virtualserver_hostbutton_url")]
		string HostbuttonUrl { get; set; }

		[QuerySerialized("virtualserver_hostbutton_gfx_url")]
		string HostbuttonGfxUrl { get; set; }

		[QuerySerialized("virtualserver_name_phonetic")]
		string PhoneticName { get; set; }

		[QuerySerialized("virtualserver_icon_id")]
		ulong IconId { get; set; }

		[QuerySerialized("virtualserver_hostbanner_mode")]
		HostBannerMode HostbannerMode { get; set; }

		[QuerySerialized("virtualserver_channel_temp_delete_delay_default")]
		TimeSpan DefaultTempChannelDeleteDelay { get; set; }
	}

	[QueryNotification(NotificationType.ServerEdited)]
	public interface ServerEdited : INotification, IInvokedNotification, IReason, IServerName, ServerBaseData2 { }

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
	public interface TokenUsed : INotification, IClientId, IClientDbId
	{
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
		ulong VirtualServerId { get; set; }

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
		ushort MaxClients { get; set; }

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
		ulong ChannelId { get; set; }

		[QuerySerialized("client_nickname")]
		string NickName { get; set; }

		[QuerySerialized("client_database_id")]
		ulong DatabaseId { get; set; }

		[QuerySerialized("client_login_name")]
		string LoginName { get; set; }

		[QuerySerialized("client_origin_server_id")]
		ulong OriginServerId { get; set; }
	}
}
