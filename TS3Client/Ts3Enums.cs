// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using System;

	/*
		* Most important Id datatypes:
		*
		* ClientUid: string
		* ClientDbId: ulong
		* ClientId: ushort
		* ChannelId: ulong
		* ServerGroupId: ulong
		* ChannelGroupId: ulong
		* PermissionIdT: int ???
	*/

	public enum ClientType
	{
		Full = 0,
		Query,
	}

	[Flags]
	public enum ClientListOptions
	{
		// ReSharper disable InconsistentNaming, UnusedMember.Global
		uid = 1 << 0,
		away = 1 << 1,
		voice = 1 << 2,
		times = 1 << 3,
		groups = 1 << 4,
		info = 1 << 5,
		icon = 1 << 6,
		country = 1 << 7,
		// ReSharper restore InconsistentNaming, UnusedMember.Global
	}

	public enum GroupNamingMode
	{
		/// <summary>No group name is displayed.</summary>
		None = 0,
		/// <summary>The group is displayed before the client name.</summary>
		Before,
		/// <summary>The group is displayed after the client name.</summary>
		After
	}

	// ReSharper disable UnusedMember.Global
	public enum Reason
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

	public enum GroupWhisperType : byte
	{
		/// <summary>Targets all users in the specified server group.
		/// (Requires servergroup targetId)</summary>
		ServerGroup = 0,
		/// <summary>Targets all users in the specified channel group.
		/// (Requires channelgroup targetId)</summary>
		ChannelGroup,
		/// <summary>Targets all users with channel commander.</summary>
		ChannelCommander,
		/// <summary>Targets all users on the server.</summary>
		AllClients,
	}

	public enum GroupWhisperTarget : byte
	{
		AllChannels = 0,
		CurrentChannel,
		ParentChannel,
		AllParentChannel,
		ChannelFamily,
		CompleteChannelFamily,
		Subchannels,
	}
	// ReSharper enable UnusedMember.Global

	public enum LicenseType : ushort
	{
		/// <summary>No licence</summary>
		NoLicense = 0,
		///<summary>Authorised TeamSpeak Host Provider License (ATHP)</summary>
		Athp = 1,
		///<summary>Offline/LAN License</summary>
		Lan = 2,
		///<summary>Non-Profit License (NPL)</summary>
		Npl = 3,
		///<summary>Unknown License</summary>
		Unknown = 4,
	}

	// http://media.teamspeak.com/ts3_literature/TeamSpeak%203%20Server%20Query%20Manual.pdf
	// public_definitions.h from the ts3 plugin library

	public enum HostMessageMode
	{
		/// <summary>Dont display anything</summary>
		None = 0,
		/// <summary>Display message in chatlog.</summary>
		Log,
		/// <summary>Display message in modal dialog.</summary>
		Modal,
		/// <summary>Display message in modal dialog and close connection.</summary>
		ModalQuit
	}

	public enum HostBannerMode
	{
		///<summary>Do not adjust.</summary>
		NoAdjust = 0,
		///<summary>Adjust but ignore aspect ratio (like TeamSpeak 2).</summary>
		IgnoreAspect,
		///<summary>Adjust and keep aspect ratio.</summary>
		KeepAspect
	}

	public enum Codec : byte
	{
		///<summary>mono, 16bit, 8kHz</summary>
		SpeexNarrowband = 0,
		///<summary>mono, 16bit, 16kHz</summary>
		SpeexWideband,
		///<summary>mono, 16bit, 32kHz</summary>
		SpeexUltraWideband,
		///<summary>mono, 16bit, 48kHz</summary>
		CeltMono,
		///<summary>mono, 16bit, 48kHz, optimized for voice</summary>
		OpusVoice,
		///<summary>stereo, 16bit, 48kHz, optimized for music</summary>
		OpusMusic,

		/// <summary>PCM S16LE 1/2 Channel (TS3Client extension; not supported by normal TeamSpeak 3 clients!)</summary>
		Raw = 127,
	}

	public enum CodecEncryptionMode
	{
		///<summary>Configure per channel.</summary>
		Individual = 0,
		///<summary>Globally disabled.</summary>
		Disabled,
		///<summary>Globally enabled.</summary>
		Enabled
	}

	public enum TextMessageTargetMode
	{
		/// <summary>Target is a client.</summary>
		Private = 1,
		/// <summary>Target is a channel.</summary>
		Channel,
		/// <summary>Target is a virtual server.</summary>
		Server,
	}

	public enum LogLevel
	{
		///<summary>Everything that is really bad.</summary>
		Error = 1,
		///<summary>Everything that might be bad.</summary>
		Warning,
		///<summary>Output that might help find a problem.</summary>
		Debug,
		///<summary>Informational output.</summary>
		Info
	}

	public enum ReasonIdentifier
	{
		///<summary>Kick client from channel.</summary>
		Channel = 4,
		///<summary>Kick client from server.</summary>
		Server,
	}

	public enum GroupType
	{
		///<summary>Template group (used for new virtual servers).</summary>
		Template = 0,
		///<summary>Regular group (used for regular clients).</summary>
		Regular,
		///<summary>Global query group (used for ServerQuery clients).</summary>
		Query
	}

	public enum PermissionType
	{
		///<summary>Server group permission. (id1: ServerGroupId, id2: 0)</summary>
		ServerGroup = 0,
		///<summary>Client specific permission. (id1: ClientDbId, id2: 0)</summary>
		GlobalClient,
		///<summary>Channel specific permission. (id1: ChannelId, id2: 0)</summary>
		Channel,
		///<summary>Channel group permission. (id1: ChannelId, id2: ChannelGroupId)</summary>
		ChannelGroup,
		///<summary>Channel-client specific permission. (id1: ChannelId, id2: ClientDbId)</summary>
		ChannelClient
	}

	public enum TokenType
	{
		///<summary>Server group token (id1: ServerGroupId, id2: 0).</summary>
		ServerGroup = 0,
		///<summary>Channel group token (id1: ServerGroupId, id2: ChannelId).</summary>
		ChannelGroup
	}

	public enum PluginTargetMode
	{
		///<summary>Send to all clients in current channel.</summary>
		CurrentChannel = 0,
		///<summary>Send to all clients on server.</summary>
		Server,
		///<summary>Send to all given client ids.</summary>
		Client,
		///<summary>Send to all subscribed clients in current channel.</summary>
		CurrentChannelSubscribedClients,
	};
}
