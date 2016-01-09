using System;

namespace TS3AudioBot
{
	interface IPlayerConnection : IDisposable
	{
		void Start();

		//bool SupportsEndCallback { get; }

		int Volume { get; set; }
		int Position { get; set; }
		bool Repeated { get; set; }

		void AudioClear();
		void AudioPlay();
		void AudioStart(string url);
		void AudioStop();
		int GetLength();
		bool IsPlaying();
	}
}
