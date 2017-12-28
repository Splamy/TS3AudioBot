namespace TS3Client.Full.Audio
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
