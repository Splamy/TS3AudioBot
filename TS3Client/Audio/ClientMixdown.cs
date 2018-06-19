namespace TS3Client.Audio
{
	using Helper;
	using System;
	using System.Collections.Generic;

	public class ClientMixdown : PassiveMergePipe, IAudioPassiveConsumer
	{
		public bool Active => true;

		private const int BufferSize = 4096 * 8;

		private readonly Dictionary<ushort, ClientMix> mixdownBuffer;

		public ClientMixdown()
		{
			Util.Init(out mixdownBuffer);
		}

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
		}
	}
}
