// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TSLib;

namespace TS3AudioBot
{
	public class ClientCall : InvokerData
	{
		/// <summary>The original unmodified string which was received by the client.</summary>
		public string TextMessage { get; }

		public ClientDbId? DatabaseId { get; }
		public ChannelId? ChannelId { get; }
		public ClientId? ClientId { get; }
		public string NickName { get; }
		public ServerGroupId[] ServerGroups { get; }
		public ChannelGroupId? ChannelGroup { get; }
		public TextMessageTargetMode? Visibiliy { get; internal set; }

		public ClientCall(Uid clientUid, string textMessage, ClientDbId? databaseId = null,
			ChannelId? channelId = null, ClientId? clientId = null, string nickName = null,
			TextMessageTargetMode? visibiliy = null, ServerGroupId[] serverGroups = null,
			ChannelGroupId? channelGroup = null) : base(clientUid)
		{
			TextMessage = textMessage;
			DatabaseId = databaseId;
			ChannelId = channelId;
			ClientId = clientId;
			NickName = nickName;
			Visibiliy = visibiliy;
			ServerGroups = serverGroups;
			ChannelGroup = channelGroup;
		}
	}
}
