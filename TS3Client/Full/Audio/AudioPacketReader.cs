namespace TS3Client.Full.Audio
{
	using System;

	class AudioPacketReader : IAudioPipe
	{
		public IAudioPassiveConsumer OutStream { get; set; }

		public void Write(ReadOnlySpan<byte> data, Meta meta)
		{
			if (OutStream == null)
				return;

			if (data.Length < 5) // Invalid packet
				return;

			// Skip [0,2) Voice Packet Id for now
			// TODO add packet id order checking
			meta.In.Sender = NetUtil.N2Hushort(data.Slice(2, 2));
			meta.Codec = (Codec)data[5];
			OutStream?.Write(data.Slice(5), meta);
		}
	}
}
