namespace TS3Client.Full.Audio
{
	using System;
	using System.Collections.Generic;

	public class SplitterPipe : IAudioPassiveConsumer
	{
		private readonly ICollection<IAudioPassiveConsumer> consumerList = new List<IAudioPassiveConsumer>();

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
				consumerList.Add(addConsumer);
			}
		}

		public void Write(ReadOnlySpan<byte> data, Meta meta)
		{
			foreach (var consumer in consumerList)
			{
				consumer.Write(data, meta);
			}
		}
	}
}
