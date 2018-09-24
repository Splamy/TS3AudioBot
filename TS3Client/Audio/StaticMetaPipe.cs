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
	using System;
	using System.Collections.Generic;
	using ClientIdT = System.UInt16;
	using ChannelIdT = System.UInt64;

	public class StaticMetaPipe : IAudioPipe
	{
		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer OutStream { get; set; }

		private MetaOut setMeta = new MetaOut();
		public TargetSendMode SendMode { get; private set; }

		private void ClearData()
		{
			setMeta.ChannelIds = null;
			setMeta.ClientIds = null;
		}

		public void SetNone()
		{
			ClearData();
			SendMode = TargetSendMode.None;
		}

		public void SetVoice()
		{
			ClearData();
			SendMode = TargetSendMode.Voice;
		}

		public void SetWhisper(IReadOnlyList<ChannelIdT> channelIds, IReadOnlyList<ClientIdT> clientIds)
		{
			ClearData();
			SendMode = TargetSendMode.Whisper;
			setMeta.ChannelIds = channelIds;
			setMeta.ClientIds = clientIds;
		}

		public void SetWhisperGroup(GroupWhisperType type, GroupWhisperTarget target, ulong targetId = 0)
		{
			ClearData();
			SendMode = TargetSendMode.WhisperGroup;
			setMeta.GroupWhisperType = type;
			setMeta.GroupWhisperTarget = target;
			setMeta.TargetId = targetId;
		}

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream is null || SendMode == TargetSendMode.None)
				return;

			meta = meta ?? new Meta();
			meta.Out = meta.Out ?? new MetaOut();
			meta.Out.SendMode = SendMode;
			switch (SendMode)
			{
			case TargetSendMode.None: break;
			case TargetSendMode.Voice: break;
			case TargetSendMode.Whisper:
				meta.Out.ChannelIds = setMeta.ChannelIds;
				meta.Out.ClientIds = setMeta.ClientIds;
				break;
			case TargetSendMode.WhisperGroup:
				meta.Out.GroupWhisperTarget = setMeta.GroupWhisperTarget;
				meta.Out.GroupWhisperType = setMeta.GroupWhisperType;
				meta.Out.TargetId = setMeta.TargetId;
				break;
			default: throw new ArgumentOutOfRangeException(nameof(SendMode), SendMode, "SendMode not handled");
			}
			OutStream?.Write(data, meta);
		}
	}
}
