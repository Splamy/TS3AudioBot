// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Audio
{
	public interface IAudioStream { }

	/// <summary>Passive producer will serve audio data that must be read.</summary>
	public interface IAudioPassiveProducer : IAudioStream, IDisposable
	{
		int Read(byte[] buffer, int offset, int length, out Meta meta);
	}
	/// <summary>Active producer will push audio to the out stream.</summary>
	public interface IAudioActiveProducer : IAudioStream
	{
		IAudioPassiveConsumer OutStream { get; set; }
	}
	/// <summary>Passive consumer will wait for manually passed audio data.</summary>
	public interface IAudioPassiveConsumer : IAudioStream
	{
		bool Active { get; }
		void Write(Span<byte> data, Meta meta);
	}
	/// <summary>Active consumer will pull audio data when required.</summary>
	public interface IAudioActiveConsumer : IAudioStream
	{
		IAudioPassiveProducer InStream { get; set; }
	}

	// Best practices for pipes:
	// - Use Active-Propagiation: `Active => OutStream?.Active ?? false`
	// - Alway check `OutStream != null` at begin of Write(...)
	public interface IAudioPipe : IAudioPassiveConsumer, IAudioActiveProducer { }

	public interface ISampleInfo
	{
		int SampleRate { get; }
		int Channels { get; }
		int BitsPerSample { get; }
	}

	public sealed class SampleInfo : ISampleInfo
	{
		public int SampleRate { get; }
		public int Channels { get; }
		public int BitsPerSample { get; }

		public SampleInfo(int sampleRate, int channels, int bitsPerSample)
		{
			SampleRate = sampleRate;
			Channels = channels;
			BitsPerSample = bitsPerSample;
		}
	}
}
