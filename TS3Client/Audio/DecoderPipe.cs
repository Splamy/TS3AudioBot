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

	public class DecoderPipe : IAudioPipe, IDisposable, ISampleInfo
	{
		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer OutStream { get; set; }

		public int SampleRate { get; } = 48_000;
		public int Channels { get; } = 2;
		public int BitsPerSample { get; } = 16;

		// opus
		private OpusDecoder opusVoiceDecoder;
		private OpusDecoder opusMusicDecoder;

		private readonly byte[] decodedBuffer;

		public IAudioPassiveConsumer OpusVoicePipeOut { get; set; }
		public IAudioActiveProducer OpusVoicePipeIn { get; set; }

		public DecoderPipe()
		{
			decodedBuffer = new byte[4096 * 2];
		}

		public void Write(Span<byte> data, Meta meta)
		{
			if (OutStream is null || !meta.Codec.HasValue)
				return;

			switch (meta.Codec.Value)
			{
			case Codec.SpeexNarrowband:
				throw new NotSupportedException();
			case Codec.SpeexWideband:
				throw new NotSupportedException();
			case Codec.SpeexUltraWideband:
				throw new NotSupportedException();
			case Codec.CeltMono:
				throw new NotSupportedException();

			case Codec.OpusVoice:
				{
					opusVoiceDecoder = opusVoiceDecoder ?? OpusDecoder.Create(48000, 1);
					var decodedData = opusVoiceDecoder.Decode(data, decodedBuffer.AsSpan(0, decodedBuffer.Length / 2));
					int dataLength = decodedData.Length;
					if (!AudioTools.TryMonoToStereo(decodedBuffer, ref dataLength))
						return;
					OutStream?.Write(decodedBuffer.AsSpan(0, dataLength), meta);
				}
				break;

			case Codec.OpusMusic:
				{
					opusMusicDecoder = opusMusicDecoder ?? OpusDecoder.Create(48000, 2);
					var decodedData = opusMusicDecoder.Decode(data, decodedBuffer);
					OutStream?.Write(decodedData, meta);
				}
				break;

			default:
				// Cannot decode
				return;
			}
		}

		public void Dispose()
		{
			opusVoiceDecoder?.Dispose();
			opusMusicDecoder?.Dispose();
		}
	}
}
