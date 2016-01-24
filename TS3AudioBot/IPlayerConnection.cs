namespace TS3AudioBot
{
	using System;

	interface IPlayerConnection : IDisposable
	{
		void Initialize();

		bool SupportsEndCallback { get; }
		event EventHandler OnSongEnd;

		int Volume { get; set; }
		int Position { get; set; }
		bool Repeated { get; set; }
		bool Pause { get; set; }
		int Length { get; }
		bool IsPlaying { get; }

		void AudioStart(string url);
		void AudioStop();
	}
}
