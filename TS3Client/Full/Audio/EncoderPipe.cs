namespace TS3Client.Full.Audio
{
	using System;

	public class EncoderPipe : IAudioPipe
	{
		public IAudioPassiveConsumer OutStream { get; set; }

		public void Write(Span<byte> data, Meta meta)
		{
			throw new NotImplementedException();
		}
	}
}
