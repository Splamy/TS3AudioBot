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
	using System.Collections.Generic;

	public class DecoderPipe : IAudioPipe, IDisposable, ISampleInfo
	{
		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer OutStream { get; set; }

		public int SampleRate { get; } = 48_000;
		public int Channels { get; } = 2;
		public int BitsPerSample { get; } = 16;

		// opus
		private readonly Dictionary<ushort, OpusDecoder> voiceDecoders = new Dictionary<ushort, OpusDecoder>();
		private readonly Dictionary<ushort, OpusDecoder> musicDecoders = new Dictionary<ushort, OpusDecoder>();

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
					var decoder = GetVoiceDecoder(meta.In.Sender);
					var decodedData = decoder.Decode(data, decodedBuffer.AsSpan(0, decodedBuffer.Length / 2));
					int dataLength = decodedData.Length;
					if (!AudioTools.TryMonoToStereo(decodedBuffer, ref dataLength))
						return;
					OutStream?.Write(decodedBuffer.AsSpan(0, dataLength), meta);
				}
				break;

			case Codec.OpusMusic:
				{
					var decoder = GetMusicDecoder(meta.In.Sender);
					var decodedData = decoder.Decode(data, decodedBuffer);
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
			foreach (var decoder in voiceDecoders.Values)
			{
				decoder.Dispose();
			}

			foreach (var decoder in musicDecoders.Values)
			{
				decoder.Dispose();
			}
		}

		private OpusDecoder GetVoiceDecoder(ushort sender)
		{
			if (voiceDecoders.TryGetValue(sender, out var decoder))
			{
				return decoder;
			}

			decoder = OpusDecoder.Create(SampleRate, 1);
			voiceDecoders.Add(sender, decoder);

			return decoder;
		}

		private OpusDecoder GetMusicDecoder(ushort sender)
		{
			if (musicDecoders.TryGetValue(sender, out var decoder))
			{
				return decoder;
			}

			decoder = OpusDecoder.Create(SampleRate, 2);
			musicDecoders.Add(sender, decoder);

			return decoder;
		}
	}
}
