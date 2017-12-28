namespace TS3Client.Full.Audio
{
	using System.IO;

	public class StreamAudioProducer : IAudioPassiveProducer
	{
		private readonly Stream stream;

		public int Read(byte[] buffer, int offset, int length, out Meta meta)
		{
			meta = default(Meta);
			return stream.Read(buffer, offset, length);
		}
		public StreamAudioProducer(Stream stream) { this.stream = stream; }
	}
}
