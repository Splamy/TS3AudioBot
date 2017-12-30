namespace TS3Client.Full.Audio
{
	using System;

	public class VolumePipe : IAudioPipe
	{
		public float Volume { get; set; } = 1;
		public IAudioPassiveConsumer OutStream { get; set; }

		public static void AdjustVolume(Span<byte> audioSamples, float volume)
		{
			if (IsAbout(volume, 1)) { /* Do nothing */ }
			else if (IsAbout(volume, 0))
			{
				audioSamples.Fill(0);
			}
			else if (IsAbout(volume, 0.5f))
			{
				// fast calculation for *0.5 volume
				for (int i = 0; i < audioSamples.Length; i += 2)
				{
					short value = (short)((audioSamples[i + 1]) << 8 | audioSamples[i]);
					var tmpshort = value >> 1;
					audioSamples[i + 0] = (byte)(tmpshort >> 0);
					audioSamples[i + 1] = (byte)(tmpshort >> 8);
				}
			}
			else
			{
				for (int i = 0; i < audioSamples.Length; i += 2)
				{
					short value = (short)((audioSamples[i + 1]) << 8 | audioSamples[i]);
					var tmpshort = (short)(value * volume);
					audioSamples[i + 0] = (byte)(tmpshort >> 0);
					audioSamples[i + 1] = (byte)(tmpshort >> 8);
				}
			}
		}

		private static bool IsAbout(float value, float compare) => Math.Abs(value - compare) < 1E-04f;

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream == null) return;

			AdjustVolume(data, Volume);

			OutStream?.Write(data, meta);
		}
	}
}
