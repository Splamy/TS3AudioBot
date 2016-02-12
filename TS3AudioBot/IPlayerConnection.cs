namespace TS3AudioBot
{
	using System;

	public interface IPlayerConnection : IDisposable
	{
		void Initialize();

		bool SupportsEndCallback { get; }
		event EventHandler OnSongEnd;

		int Volume { get; set; }
		TimeSpan Position { get; set; }
		bool Repeated { get; set; }
		bool Pause { get; set; }
		TimeSpan Length { get; }
		bool IsPlaying { get; }

		// TODO check change in uri ??
		void AudioStart(string url);
		void AudioStop();
	}
}
