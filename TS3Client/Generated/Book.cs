// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

















namespace TS3Client.Full.Book
{
	using System;
	using System.Collections.Generic;

	using i8  = System.Byte;
	using u8  = System.SByte;
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
	using SocketAddr = System.Net.IPAddress;

	using Uid = System.String;
	using ClientDbId = System.UInt64;
	using ClientId = System.UInt16;
	using ChannelId = System.UInt64;
	using ServerGroupId = System.UInt64;
	using ChannelGroupId = System.UInt64;
	using IconHash = System.Int32;
	using ConnectionId = System.UInt32;

	public sealed partial class ServerGroup
	{
		public ServerGroupId Id { get;  }
		public str Name { get; set; }
		public GroupType GroupType { get; set; }
		public IconHash IconId { get; set; }
		public bool IsPermanent { get; set; }
		public i32 SortId { get; set; }
		public GroupNamingMode NamingMode { get; set; }
		public i32 NeededModifyPower { get; set; }
		public i32 NeededMemberAddPower { get; set; }
		public i32 NeededMemberRemovePower { get; set; }
	}

	public sealed partial class File
	{
		public str Path { get; set; }
		public str Name { get; set; }
		public i64 Size { get; set; }
		public DateTime LastChanged { get; set; }
		public bool IsFile { get; set; }
	}

	public sealed partial class OptionalChannelData
	{
		public str Description { get; set; }
	}

	public sealed partial class Channel
	{
		public ChannelId Id { get;  }
		public ChannelId Parent { get; set; }
		public str Name { get; set; }
		public str Topic { get; set; }
		public Codec Codec { get; set; }
		public u8 CodecQuality { get; set; }
		public u16 MaxClients { get; set; }
		public MaxFamilyClients MaxFamilyClients { get; set; }
		public i32 Order { get; set; }
		public ChannelType ChannelType { get; set; }
		public bool IsDefault { get; set; }
		public bool HasPassword { get; set; }
		public i32 CodecLatencyFactor { get; set; }
		public bool IsUnencrypted { get; set; }
		public Duration DeleteDelay { get; set; }
		public i32 NeededTalkPower { get; set; }
		public bool ForcedSilence { get;  }
		public str PhoneticName { get; set; }
		public IconHash IconId { get; set; }
		public bool IsPrivate { get; set; }
		public OptionalChannelData OptionalData { get;  }
	}

	public sealed partial class OptionalClientData
	{
		public str Version { get; set; }
		public str Platform { get; set; }
		public str LoginName { get;  }
		public DateTime Created { get;  }
		public DateTime LastConnected { get;  }
		public u32 TotalConnection { get;  }
		public u64 MonthBytesUploaded { get;  }
		public u64 MonthBytesDownloaded { get;  }
		public u64 TotalBytesUploaded { get;  }
		public u64 TotalBytesDownloaded { get;  }
	}

	public sealed partial class ConnectionClientData
	{
		public Duration Ping { get;  }
		public Duration PingDeviation { get;  }
		public Duration ConnectedTime { get;  }
		public SocketAddr ClientAddress { get;  }
		public u64 PacketsSentSpeech { get;  }
		public u64 PacketsSentKeepalive { get;  }
		public u64 PacketsSentControl { get;  }
		public u64 BytesSentSpeech { get;  }
		public u64 BytesSentKeepalive { get;  }
		public u64 BytesSentControl { get;  }
		public u64 PacketsReceivedSpeech { get;  }
		public u64 PacketsReceivedKeepalive { get;  }
		public u64 PacketsReceivedControl { get;  }
		public u64 BytesReceivedSpeech { get;  }
		public u64 BytesReceivedKeepalive { get;  }
		public u64 BytesReceivedControl { get;  }
		public f32 ServerToClientPacketlossSpeech { get;  }
		public f32 ServerToClientPacketlossKeepalive { get;  }
		public f32 ServerToClientPacketlossControl { get;  }
		public f32 ServerToClientPacketlossTotal { get;  }
		public f32 ClientToServerPacketlossSpeech { get;  }
		public f32 ClientToServerPacketlossKeepalive { get;  }
		public f32 ClientToServerPacketlossControl { get;  }
		public f32 ClientToServerPacketlossTotal { get;  }
		public u64 BandwidthSentLastSecondSpeech { get;  }
		public u64 BandwidthSentLastSecondKeepalive { get;  }
		public u64 BandwidthSentLastSecondControl { get;  }
		public u64 BandwidthSentLastMinuteSpeech { get;  }
		public u64 BandwidthSentLastMinuteKeepalive { get;  }
		public u64 BandwidthSentLastMinuteControl { get;  }
		public u64 BandwidthReceivedLastSecondSpeech { get;  }
		public u64 BandwidthReceivedLastSecondKeepalive { get;  }
		public u64 BandwidthReceivedLastSecondControl { get;  }
		public u64 BandwidthReceivedLastMinuteSpeech { get;  }
		public u64 BandwidthReceivedLastMinuteKeepalive { get;  }
		public u64 BandwidthReceivedLastMinuteControl { get;  }
		public u64 FiletransferBandwidthSent { get;  }
		public u64 FiletransferBandwidthReceived { get;  }
		public Duration IdleTime { get;  }
	}

	public sealed partial class Client
	{
		public ClientId Id { get;  }
		public ChannelId Channel { get; set; }
		public Uid Uid { get;  }
		public str Name { get; set; }
		public bool InputMuted { get; set; }
		public bool OutputMuted { get; set; }
		public bool OutputOnlyMuted { get; set; }
		public bool InputHardwareEnabled { get; set; }
		public bool OutputHardwareEnabled { get; set; }
		public bool TalkPowerGranted { get; set; }
		public str Metadata { get; set; }
		public bool IsRecording { get; set; }
		public ClientDbId DatabaseId { get;  }
		public ChannelGroupId ChannelGroup { get; set; }
		public List<ServerGroupId> ServerGroups { get; set; }
		public str AwayMessage { get; set; }
		public ClientType ClientType { get;  }
		public str AvatarHash { get;  }
		public i32 TalkPower { get;  }
		public TalkPowerRequest TalkPowerRequest { get;  }
		public str Description { get; set; }
		public bool IsPrioritySpeaker { get; set; }
		public u32 UnreadMessages { get;  }
		public str PhoneticName { get; set; }
		public i32 NeededServerqueryViewPower { get;  }
		public IconHash IconId { get;  }
		public bool IsChannelCommander { get; set; }
		public str CountryCode { get;  }
		public ChannelId InheritedChannelGroupFromChannel { get;  }
		public List<str> Badges { get; set; }
		public OptionalClientData OptionalData { get;  }
		public ConnectionClientData ConnectionData { get;  }
	}

	public sealed partial class OptionalServerData
	{
		public u32 ConnectionCount { get;  }
		public u64 ChannelCount { get;  }
		public Duration Uptime { get;  }
		public bool HasPassword { get;  }
		public ChannelGroupId DefaultChannelAdminGroup { get; set; }
		public u64 MaxDownloadTotalBandwith { get; set; }
		public u64 MaxUploadTotalBandwith { get; set; }
		public u32 ComplainAutobanCount { get; set; }
		public Duration ComplainAutobanTime { get; set; }
		public Duration ComplainRemoveTime { get; set; }
		public u16 MinClientsForceSilence { get; set; }
		public u32 AntifloodPointsTickReduce { get; set; }
		public u32 AntifloodPointsNeededCommandBlock { get; set; }
		public u16 ClientCount { get;  }
		public u32 QueryCount { get;  }
		public u32 QueryOnlineCount { get;  }
		public u64 DownloadQuota { get; set; }
		public u64 UploadQuota { get; set; }
		public u64 MonthBytesDownloaded { get;  }
		public u64 MonthBytesUploaded { get;  }
		public u64 TotalBytesDownloaded { get;  }
		public u64 TotalBytesUploaded { get;  }
		public u16 Port { get;  }
		public bool Autostart { get; set; }
		public str MachineId { get;  }
		public u8 NeededIdentitySecurityLevel { get; set; }
		public bool LogClient { get; set; }
		public bool LogQuery { get; set; }
		public bool LogChannel { get; set; }
		public bool LogPermissions { get; set; }
		public bool LogServer { get; set; }
		public bool LogFileTransfer { get; set; }
		public DateTime MinClientVersion { get;  }
		public u16 ReservedSlots { get; set; }
		public f32 TotalPacketlossSpeech { get;  }
		public f32 TotalPacketlossKeepalive { get;  }
		public f32 TotalPacketlossControl { get;  }
		public f32 TotalPacketlossTotal { get;  }
		public Duration TotalPing { get;  }
		public bool WeblistEnabled { get; set; }
		public DateTime MinAndroidVersion { get;  }
		public DateTime MinIosVersion { get;  }
	}

	public sealed partial class ConnectionServerData
	{
		public u64 FileTransferBandwidthSent { get;  }
		public u64 FileTransferBandwidthReceived { get;  }
		public u64 FileTransferBytesSentTotal { get;  }
		public u64 FileTransferBytesReceivedTotal { get;  }
		public u64 PacketsSentTotal { get;  }
		public u64 BytesSentTotal { get;  }
		public u64 PacketsReceivedTotal { get;  }
		public u64 BytesReceivedTotal { get;  }
		public u64 BandwidthSentLastSecondTotal { get;  }
		public u64 BandwidthSentLastMinuteTotal { get;  }
		public u64 BandwidthReceivedLastSecondTotal { get;  }
		public u64 BandwidthReceivedLastMinuteTotal { get;  }
		public Duration ConnectedTime { get;  }
		public f32 PacketlossTotal { get;  }
		public Duration Ping { get;  }
	}

	public sealed partial class Server
	{
		public Uid Uid { get;  }
		public u64 VirtualServerId { get;  }
		public str Name { get;  }
		public str WelcomeMessage { get;  }
		public str Platform { get;  }
		public str Version { get;  }
		public u16 MaxClients { get;  }
		public DateTime Created { get;  }
		public CodecEncryptionMode CodecEncryptionMode { get; set; }
		public str Hostmessage { get; set; }
		public HostMessageMode HostmessageMode { get; set; }
		public ServerGroupId DefaultServerGroup { get; set; }
		public ChannelGroupId DefaultChannelGroup { get; set; }
		public str HostbannerUrl { get; set; }
		public str HostbannerGfxUrl { get; set; }
		public Duration HostbannerGfxInterval { get; set; }
		public f32 PrioritySpeakerDimmModificator { get; set; }
		public str HostbuttonTooltip { get; set; }
		public str HostbuttonUrl { get; set; }
		public str HostbuttonGfxUrl { get; set; }
		public str PhoneticName { get; set; }
		public IconHash IconId { get;  }
		public List<str> Ip { get;  }
		public bool AskForPrivilegekey { get;  }
		public HostBannerMode HostbannerMode { get; set; }
		public Duration TempChannelDefaultDeleteDelay { get; set; }
		public u16 ProtocolVersion { get;  }
		public LicenseType License { get;  }
		public OptionalServerData OptionalData { get;  }
		public ConnectionServerData ConnectionData { get;  }
		public Dictionary<ClientId,Client> Clients { get;  }
		public Dictionary<ChannelId,Channel> Channels { get;  }
		public Dictionary<ServerGroupId,ServerGroup> Groups { get;  }
	}

	public sealed partial class Connection
	{
		public ConnectionId Id { get;  }
		public ClientId OwnClient { get;  }
		public Server Server { get;  }
	}

	public sealed partial class ChatEntry
	{
		public ClientId SenderClient { get;  }
		public str Text { get;  }
		public DateTime Date { get;  }
		public TextMessageTargetMode Mode { get;  }
	}

}