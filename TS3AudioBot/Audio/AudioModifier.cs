namespace TS3AudioBot.Audio
{
	using System;

	static class AudioModifier
	{
		public static void AdjustVolume(byte[] audioSamples, int length, float volume)
		{
			for (int i = 0; i < length; i += 2)
			{
				var res = (short)(BitConverter.ToInt16(audioSamples, i) * volume);
				var bt = BitConverter.GetBytes(res);
				audioSamples[i] = bt[0];
				audioSamples[i + 1] = bt[1];
			}
		}
	}
}
