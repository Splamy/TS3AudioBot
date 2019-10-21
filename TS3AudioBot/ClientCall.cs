// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	public class ClientCall : InvokerData
	{
		/// <summary>The original unmodified string which was received by the client.</summary>
		public string TextMessage { get; }

		public ulong? DatabaseId { get; }
		public ulong? ChannelId { get; }
		public ushort? ClientId { get; }
		public string NickName { get; }
		public ulong[] ServerGroups { get; }
		public ulong? ChannelGroup { get; }
		public TS3Client.TextMessageTargetMode? Visibiliy { get; internal set; }

		public ClientCall(string clientUid, string textMessage, ulong? databaseId = null, ulong? channelId = null,
			ushort? clientId = null, string nickName = null, TS3Client.TextMessageTargetMode? visibiliy = null,
			ulong[] serverGroups = null, ulong? channelGroup = null) : base(clientUid)
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
