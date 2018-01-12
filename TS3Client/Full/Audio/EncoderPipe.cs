// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full.Audio
{
	using Opus;
	using System;

	public class EncoderPipe : IAudioPipe, IDisposable, ISampleInfo
	{
		public IAudioPassiveConsumer OutStream { get; set; }

		public Codec Codec { get; }
		public int SampleRate { get; }
		public int Channels { get; }
		public int BitsPerSample { get; }

		public int OptimalPacketSize { get; }
		public int Bitrate { get => opusEncoder.Bitrate; set => opusEncoder.Bitrate = value; }

		// opus
		private readonly OpusEncoder opusEncoder;

		private const int SegmentFrames = 960;
		private byte[] soundBuffer = new byte[0];
		private int soundBufferLength;
		private byte[] notEncodedBuffer = new byte[0];
		private int notEncodedBufferLength;
		private readonly byte[] encodedBuffer;

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
			OptimalPacketSize = opusEncoder.FrameByteCount(SegmentFrames);
			encodedBuffer = new byte[opusEncoder.MaxDataBytes];
		}

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream == null)
				return;

			int newSoundBufferLength = data.Length + notEncodedBufferLength;
			if (newSoundBufferLength > soundBuffer.Length)
				soundBuffer = new byte[newSoundBufferLength];
			soundBufferLength = newSoundBufferLength;

			Array.Copy(notEncodedBuffer, 0, soundBuffer, 0, notEncodedBufferLength);
			data.CopyTo(new Span<byte>(soundBuffer, notEncodedBufferLength));

			int packetSize = OptimalPacketSize;
			int segmentCount = soundBufferLength / packetSize;
			int segmentsEnd = segmentCount * packetSize;
			int newNotEncodedBufferLength = soundBufferLength - segmentsEnd;
			if (newNotEncodedBufferLength > notEncodedBuffer.Length)
				notEncodedBuffer = new byte[newNotEncodedBufferLength];
			notEncodedBufferLength = newNotEncodedBufferLength;
			Array.Copy(soundBuffer, segmentsEnd, notEncodedBuffer, 0, notEncodedBufferLength);

			for (int i = 0; i < segmentCount; i++)
			{
				var encodedData = opusEncoder.Encode(soundBuffer.AsSpan().Slice(i * packetSize, packetSize), packetSize, encodedBuffer);
				if (meta != null)
					meta.Codec = Codec; // TODO copy ?
				OutStream?.Write(encodedData, meta);
			}
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
