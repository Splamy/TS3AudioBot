namespace TS3Client.Full.Audio
{
	using System;

	public class PreciseTimedPipe : IAudioActiveConsumer, IAudioActiveProducer
	{
		public IAudioPassiveProducer InStream { get; set; }
		public IAudioPassiveConsumer OutStream { get; set; }

		public PreciseTimedPipe() { }

		public PreciseTimedPipe(IAudioPassiveProducer inStream)
		{
			InStream = inStream;
		}

		public void ReadTimed()
		{
			byte[] buffer = new byte[256];
			while (true)
			{
				System.Threading.Thread.Sleep(100);
				var inStream = InStream;
				if (inStream == null)
					continue;
				int read = inStream.Read(buffer, 0, buffer.Length, out var meta);
				OutStream?.Write(new Span<byte>(buffer, 0, read), meta);
			}
		}
	}
}
