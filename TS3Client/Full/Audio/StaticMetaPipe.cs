namespace TS3Client.Full.Audio
{
	using System;
	using System.Collections.Generic;
	using ClientIdT = System.UInt16;
	using ChannelIdT = System.UInt64;

	public class StaticMetaPipe : IAudioPipe
	{
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

		public void Write(ReadOnlySpan<byte> data, Meta meta)
		{
			if (OutStream == null || SendMode == TargetSendMode.None)
				return;

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
			default: break;
			}
			OutStream?.Write(data, meta);
		}
	}
}
