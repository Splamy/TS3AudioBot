// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Audio
{
	using System;

	public sealed class InvokerData
	{
		public string ClientUid { get; }
		public ulong? DatabaseId { get; }
		public ulong? ChannelId { get; }
		public ushort? ClientId { get; }
		public string NickName { get; }
		public string Token { get; }
		public TS3Client.TextMessageTargetMode? Visibiliy { get; internal set; }
		// Lazy
		public ulong[] ServerGroups { get; internal set; }
		public bool IsAnonymous => ClientUid == AnonymousUid;

		private const string AnonymousUid = "Anonymous";
		public static readonly InvokerData Anonymous = new InvokerData(AnonymousUid);

		public InvokerData(string clientUid, ulong? databaseId = null, ulong? channelId = null,
			ushort? clientId = null, string nickName = null, string token = null,
			TS3Client.TextMessageTargetMode? visibiliy = null)
		{
			ClientUid = clientUid ?? throw new ArgumentNullException(nameof(ClientUid));
			DatabaseId = databaseId;
			ChannelId = channelId;
			ClientId = clientId;
			NickName = nickName;
			Token = token;
			Visibiliy = visibiliy;
		}

		public override int GetHashCode() => ClientUid.GetHashCode();

		public override bool Equals(object obj)
		{
			if (!(obj is InvokerData other))
				return false;

			return ClientUid == other.ClientUid;
		}
	}
}
