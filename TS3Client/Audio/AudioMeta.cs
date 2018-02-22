// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Audio
{
	using System.Collections.Generic;
	using ClientIdT = System.UInt16;
	using ChannelIdT = System.UInt64;

	public class Meta
	{
		public Codec? Codec;
		public MetaIn In;
		public MetaOut Out;
	}

	public struct MetaIn
	{
		public ClientIdT Sender { get; set; }
		public bool Whisper { get; set; }
	}

	public class MetaOut
	{
		public TargetSendMode SendMode { get; set; }
		public ulong TargetId { get; set; }
		public GroupWhisperTarget GroupWhisperTarget { get; set; }
		public GroupWhisperType GroupWhisperType { get; set; }
		public IReadOnlyList<ChannelIdT> ChannelIds { get; set; }
		public IReadOnlyList<ClientIdT> ClientIds { get; set; }
	}

	public enum TargetSendMode
	{
		None,
		Voice,
		Whisper,
		WhisperGroup,
	}
}
