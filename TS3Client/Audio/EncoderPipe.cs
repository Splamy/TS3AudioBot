// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Audio
{
	using Opus;
	using System;

	public class EncoderPipe : IAudioPipe, IDisposable, ISampleInfo
	{
		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer OutStream { get; set; }

		public Codec Codec { get; }
		public int SampleRate { get; }
		public int Channels { get; }
		public int BitsPerSample { get; }

		public int PacketSize { get; }
		public int Bitrate { get => opusEncoder.Bitrate; set => opusEncoder.Bitrate = value; }

		// opus
		private readonly OpusEncoder opusEncoder;

		private const int SegmentFrames = 960;
		// todo add upper limit to buffer size and drop everying over
		private byte[] notEncodedBuffer = Array.Empty<byte>();
		private int notEncodedLength;
		private readonly byte[] encodedBuffer = new byte[4096];

		public EncoderPipe(Codec codec)
		{
			Codec = codec;

			switch (codec)
			{
			case Codec.Raw:
				throw new InvalidOperationException("Raw is not a valid encoding target");
			case Codec.SpeexNarrowband:
				throw new NotSupportedException();
			case Codec.SpeexWideband:
				throw new NotSupportedException();
			case Codec.SpeexUltraWideband:
				throw new NotSupportedException();
			case Codec.CeltMono:
				throw new NotSupportedException();

			case Codec.OpusVoice:
				SampleRate = 48000;
				Channels = 1;
				opusEncoder = OpusEncoder.Create(SampleRate, Channels, Application.Voip);
				Bitrate = 8192 * 2;
				break;

			case Codec.OpusMusic:
				SampleRate = 48000;
				Channels = 2;
				opusEncoder = OpusEncoder.Create(SampleRate, Channels, Application.Audio);
				Bitrate = 8192 * 4;
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(codec));
			}

			BitsPerSample = 16;
			PacketSize = opusEncoder.FrameByteCount(SegmentFrames);
		}

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream is null)
				return;

			int newSoundBufferLength = data.Length + notEncodedLength;
			if (newSoundBufferLength > notEncodedBuffer.Length)
			{
				var tmpSoundBuffer = new byte[newSoundBufferLength];
				Array.Copy(notEncodedBuffer, 0, tmpSoundBuffer, 0, notEncodedLength);
				notEncodedBuffer = tmpSoundBuffer;
			}

			var soundBuffer = notEncodedBuffer.AsSpan();
			data.CopyTo(soundBuffer.Slice(notEncodedLength));

			int segmentCount = newSoundBufferLength / PacketSize;
			int segmentsEnd = segmentCount * PacketSize;
			notEncodedLength = newSoundBufferLength - segmentsEnd;

			for (int i = 0; i < segmentCount; i++)
			{
				var encodedData = opusEncoder.Encode(soundBuffer.Slice(i * PacketSize, PacketSize), PacketSize, encodedBuffer);
				meta = meta ?? new Meta();
				meta.Codec = Codec; // TODO copy ?
				OutStream?.Write(encodedData, meta);
			}

			soundBuffer.Slice(segmentsEnd, notEncodedLength).CopyTo(soundBuffer);
		}

		public TimeSpan GetPlayLength(int bytes)
		{
			return TimeSpan.FromSeconds(bytes / (double)(SampleRate * (BitsPerSample / 8) * Channels));
		}

		public void Dispose()
		{
			opusEncoder?.Dispose();
		}
	}
}
