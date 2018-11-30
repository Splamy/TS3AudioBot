using TS3Client;
using TS3Client.Audio;
using TS3Client.Full;

namespace Ts3ClientTests
{
	public static class Audio
	{
		public static void Main(string[] args)
		{
			// Initialize client
			var client = new Ts3FullClient(EventDispatchType.AutoThreadPooled);
			var data = Ts3Crypt.LoadIdentity("MCkDAgbAAgEgAiBPKKMIrHtAH/FBKchbm4iRWZybdRTk/ZiehtH0gQRg+A==", 64, 0).Unwrap();
			//var data = Ts3Crypt.GenerateNewIdentity();
			var con = new ConnectionDataFull() { Address = "pow.splamy.de", Username = "TestClient", Identity = data };

			// Setup audio
			client
				// Save cpu by not processing the rest of the pipe when the
				// output is not read.
				.Chain<CheckActivePipe>()
				// This reads the packet meta data, checks for packet order
				// and manages packet merging.
				.Chain<AudioPacketReader>()
				// Teamspeak sends audio encoded. This pipe will decode it to
				// simple PCM.
				.Chain<DecoderPipe>()
				// This will merge multiple clients talking into one audio stream
				.Chain<ClientMixdown>()
				// Reads from the ClientMixdown buffer with a fixed timing
				.Into<PreciseTimedPipe>(x => x.Initialize(new SampleInfo(48_000, 2, 16)))
				// Reencode to the codec of our choice
				.Chain(new EncoderPipe(Codec.OpusMusic))
				// Define where to send to.
				.Chain<StaticMetaPipe>(x => x.SetVoice())
				// Send it with our client.
				.Chain(client);

			// Connect
			client.Connect(con);
		}
	}
}
