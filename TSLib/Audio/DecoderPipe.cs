// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using TSLib.Audio.Opus;

namespace TSLib.Audio
{
	public class DecoderPipe : IAudioPipe, IDisposable, ISampleInfo
	{
		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer OutStream { get; set; }

		public int SampleRate { get; } = 48_000;
		public int Channels { get; } = 2;
		public int BitsPerSample { get; } = 16;

		// TOOO:
		// - Add some sort of decoder reuse to reduce concurrent amount of decoders (see ctl 'reset')
		// - Clean up decoders after some time (Control: Tick?)
		// - Make dispose threadsafe OR redefine thread safety requirements for pipes.

		private readonly Dictionary<ClientId, (OpusDecoder, Codec)> decoders = new Dictionary<ClientId, (OpusDecoder, Codec)>();
		private readonly byte[] decodedBuffer;

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
			case Codec.OpusVoice:
				{
					var decoder = GetDecoder(meta.In.Sender, Codec.OpusVoice);
					var decodedData = decoder.Decode(data, decodedBuffer.AsSpan(0, decodedBuffer.Length / 2));
					int dataLength = decodedData.Length;
					if (!AudioTools.TryMonoToStereo(decodedBuffer, ref dataLength))
						break;
					OutStream?.Write(decodedBuffer.AsSpan(0, dataLength), meta);
				}
				break;

			case Codec.OpusMusic:
				{
					var decoder = GetDecoder(meta.In.Sender, Codec.OpusMusic);
					var decodedData = decoder.Decode(data, decodedBuffer);
					OutStream?.Write(decodedData, meta);
				}
				break;

			default:
				// Cannot decode
				break;
			}
		}

		private OpusDecoder GetDecoder(ClientId sender, Codec codec)
		{
			if (decoders.TryGetValue(sender, out var decoder))
			{
				if (decoder.Item2 == codec)
					return decoder.Item1;
				else
					decoder.Item1.Dispose();
			}

			var newDecoder = CreateDecoder(codec);
			decoders[sender] = (newDecoder, codec);
			return newDecoder;
		}

		private OpusDecoder CreateDecoder(Codec codec)
		{
			switch (codec)
			{
			case Codec.OpusVoice:
				return OpusDecoder.Create(SampleRate, 1);
			case Codec.OpusMusic:
				return OpusDecoder.Create(SampleRate, 2);
			}
			return null;
		}

		public void Dispose()
		{
			foreach (var (decoder, _) in decoders.Values)
			{
				decoder.Dispose();
			}
		}
	}
}
