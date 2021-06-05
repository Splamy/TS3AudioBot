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
	public sealed class DecoderPipe : IAudioPipe, IDisposable
	{
		public bool Active => OutStream?.Active ?? false;
		public IAudioPassiveConsumer? OutStream { get; set; }

		// TOOO:
		// - Add some sort of decoder reuse to reduce concurrent amount of decoders (see ctl 'reset')
		// - Clean up decoders after some time (Control: Tick?)
		// - Make dispose threadsafe OR redefine thread safety requirements for pipes.

		private readonly Dictionary<ClientId, (OpusDecoder, Codec)> decoders = new();
		private readonly byte[] decodedBuffer;

		public DecoderPipe()
		{
			decodedBuffer = new byte[4096 * 2];
		}

		public void Write(Span<byte> data, Meta? meta)
		{
			if (OutStream is null || meta?.Codec is null)
				return;

			switch (meta.Codec.GetValueOrDefault())
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

		private static OpusDecoder CreateDecoder(Codec codec)
		{
			return codec switch
			{
				Codec.OpusVoice => OpusDecoder.Create(SampleInfo.OpusVoice),
				Codec.OpusMusic => OpusDecoder.Create(SampleInfo.OpusMusic),
				_ => throw new NotSupportedException(),
			};
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
