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

namespace TSLib.Audio
{
	public class ClientMixdown : PassiveMergePipe, IAudioPassiveConsumer
	{
		public bool Active => true;

		private const int BufferSize = 4096 * 8;

		private readonly Dictionary<ClientId, ClientMix> mixdownBuffer = new Dictionary<ClientId, ClientMix>();

		public void Write(Span<byte> data, Meta meta)
		{
			if (data.IsEmpty)
				return;

			if (!mixdownBuffer.TryGetValue(meta.In.Sender, out var mix))
			{
				mix = new ClientMix(BufferSize);
				mixdownBuffer.Add(meta.In.Sender, mix);
				Add(mix);
			}

			mix.Write(data, meta);
			/*
			List<KeyValuePair<ushort, ClientMix>> remove = null;
			foreach (var item in mixdownBuffer)
			{
				if (item.Value.Length == 0)
				{
					remove = remove ?? new List<KeyValuePair<ushort, ClientMix>>();
					remove.Add(item);
				}
			}

			if (remove != null)
			{
				foreach (var item in remove)
				{
					mixdownBuffer.Remove(item.Key);
					Remove(item.Value);
				}
			}*/
		}

		public class ClientMix : IAudioPassiveProducer
		{
			public byte[] Buffer { get; }
			public int Length { get; set; } = 0;
			public Meta LastMeta { get; set; }

			private readonly object rwLock = new object();

			public ClientMix(int bufferSize)
			{
				Buffer = new byte[bufferSize];
			}

			public void Write(Span<byte> data, Meta meta)
			{
				lock (rwLock)
				{
					int take = Math.Min(data.Length, Buffer.Length - Length);
					data.Slice(0, take).CopyTo(Buffer.AsSpan(Length));
					Length += take;
					LastMeta = meta;
				}
			}

			public int Read(byte[] buffer, int offset, int length, out Meta meta)
			{
				lock (rwLock)
				{
					int take = Math.Min(Length, length);

					Array.Copy(Buffer, 0, buffer, offset, take);
					Array.Copy(Buffer, take, Buffer, 0, Buffer.Length - take);
					Length -= take;

					meta = default;
					return take;
				}
			}

			public void Dispose() { }
		}
	}
}
