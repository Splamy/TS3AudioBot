// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TS3AudioBot.ResourceFactories;
using TSLib;
using TSLib.Audio;
using TSLib.Helper;

namespace TS3AudioBot.Audio
{
	public class Player : IDisposable
	{
		private const Codec SendCodec = Codec.OpusMusic;

		public IPlayerSource CurrentPlayerSource { get; private set; }
		public StallCheckPipe StallCheckPipe { get; }
		public VolumePipe VolumePipe { get; }
		public FfmpegProducer FfmpegProducer { get; }
		public PreciseTimedPipe TimePipe { get; }
		public PassiveMergePipe MergePipe { get; }
		public EncoderPipe EncoderPipe { get; }
		public IAudioPassiveConsumer PlayerSink { get; private set; }

		public Player(ConfBot config, Id id)
		{
			FfmpegProducer = new FfmpegProducer(config.GetParent().Tools.Ffmpeg, id);
			StallCheckPipe = new StallCheckPipe();
			VolumePipe = new VolumePipe();
			Volume = config.Audio.Volume.Default;
			EncoderPipe = new EncoderPipe(SendCodec) { Bitrate = ScaleBitrate(config.Audio.Bitrate) };
			TimePipe = new PreciseTimedPipe { ReadBufferSize = EncoderPipe.PacketSize };
			TimePipe.Initialize(EncoderPipe, id);
			MergePipe = new PassiveMergePipe();

			config.Audio.Bitrate.Changed += (s, e) => EncoderPipe.Bitrate = ScaleBitrate(e.NewValue);

			MergePipe.Into(TimePipe).Chain<CheckActivePipe>().Chain(StallCheckPipe).Chain(VolumePipe).Chain(EncoderPipe);
		}

		public void SetTarget(IAudioPassiveConsumer target)
		{
			PlayerSink = target;
			EncoderPipe.Chain(target);
		}

		private static int ScaleBitrate(int value) => Tools.Clamp(value, 1, 255) * 1000;

		public event EventHandler OnSongEnd;
		public event EventHandler<SongInfoChanged> OnSongUpdated;

		private void TriggerSongEnd(object o, EventArgs e) => OnSongEnd?.Invoke(this, EventArgs.Empty);
		private void TriggerSongUpdated(object o, SongInfoChanged e) => OnSongUpdated?.Invoke(this, e);

		public E<string> Play(PlayResource res)
		{
			E<string> result;
			if (res is MediaPlayResource mres && mres.IsIcyStream)
				result = FfmpegProducer.AudioStartIcy(res.PlayUri);
			else
				result = FfmpegProducer.AudioStart(res.PlayUri, res.Meta?.StartOffset);
			if (result)
				Play(FfmpegProducer);
			return result;
		}

		public void Play(IPlayerSource source)
		{
			var oldSource = CurrentPlayerSource;
			if (oldSource != source)
			{
				// Clean up old
				CleanSource(oldSource);
				// Set events
				source.OnSongEnd += TriggerSongEnd;
				source.OnSongUpdated += TriggerSongUpdated;
				// Update pipes
				MergePipe.Add(source);
				CurrentPlayerSource = source;
			}
			// Start Ticker
			TimePipe.Paused = false;
		}

		private void CleanSource(IPlayerSource source)
		{
			if (source is null)
				return;
			source.OnSongEnd -= TriggerSongEnd;
			source.OnSongUpdated -= TriggerSongUpdated;
			MergePipe.Remove(source);
			source.Dispose();
		}

		public void Stop()
		{
			CurrentPlayerSource?.Dispose();
			if (MergePipe.Count <= 1)
				TimePipe.Paused = true;
		}

		public void StopAll()
		{
			Stop();
			TimePipe.Paused = true;
			MergePipe.Dispose();
		}

		public TimeSpan Length => CurrentPlayerSource.Length;

		public TimeSpan Position
		{
			get => CurrentPlayerSource.Position;
			set => CurrentPlayerSource.Position = value;
		}

		public float Volume
		{
			get => AudioValues.FactorToHumanVolume(VolumePipe.Volume);
			set => VolumePipe.Volume = AudioValues.HumanVolumeToFactor(value);
		}

		public bool Paused
		{
			get => TimePipe.Paused;
			set => TimePipe.Paused = value;
		}

		// Extras

		public void SetStall() => StallCheckPipe.SetStall();

		[Obsolete(AttributeStrings.UnderDevelopment)]
		public void MixInStreamOnce(IPlayerSource producer)
		{
			producer.OnSongEnd += (s, e) =>
			{
				MergePipe.Remove(producer);
				producer.Dispose();
			};
			MergePipe.Add(producer);
			TimePipe.Paused = false;
		}

		public void Dispose()
		{
			StopAll();
			CleanSource(CurrentPlayerSource);
			TimePipe.Dispose();
			FfmpegProducer.Dispose();
			EncoderPipe.Dispose();
		}
	}
}
