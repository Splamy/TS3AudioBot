namespace TS3Client.Full.Audio
{
	using System;
	using System.Collections.Generic;

	public class SplitterPipe : IAudioPassiveConsumer
	{
		private readonly List<IAudioPassiveConsumer> safeConsumerList = new List<IAudioPassiveConsumer>();
		private readonly List<IAudioPassiveConsumer> consumerList = new List<IAudioPassiveConsumer>();
		private bool changed = false;
		private readonly object listLock = new object();
		private byte[] buffer = new byte[0];

		public bool CloneMeta { get; set; } = false;

		public IAudioPassiveConsumer OutStream
		{
			get => this;
			set => Add(value);
		}

		public void Add(IAudioPassiveConsumer addConsumer)
		{
			if (!consumerList.Contains(addConsumer) && addConsumer != this)
			{
				lock (listLock)
				{
					consumerList.Add(addConsumer);
					changed = true;
				}
			}
		}

		public void Write(Span<byte> data, Meta meta)
		{
			if (changed)
			{
				lock (listLock)
				{
					if (changed)
					{
						safeConsumerList.Clear();
						safeConsumerList.AddRange(consumerList);
						changed = false;
					}
				}
			}

			if (safeConsumerList.Count == 0)
				return;

			if (safeConsumerList.Count == 1)
			{
				safeConsumerList[0].Write(data, meta);
				return;
			}

			if(buffer.Length < data.Length)
				buffer = new byte[data.Length];

			var bufSpan = new Span<byte>(buffer, 0, data.Length);
			for (int i = 0; i < safeConsumerList.Count - 1; i++)
			{
				data.CopyTo(bufSpan);
				safeConsumerList[i].Write(bufSpan, meta);
			}
			// safe one memcopy call by passing the last one our original data
			safeConsumerList[safeConsumerList.Count - 1].Write(data, meta);
		}
	}
}
