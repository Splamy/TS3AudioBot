namespace TS3Client.Full.Audio
{
	using System;

	public static class AudioPipeExtensions
	{
		public static T Chain<T>(this IAudioActiveProducer producer, T addConsumer) where T : IAudioPassiveConsumer
		{
			if (producer.OutStream == null)
			{
				producer.OutStream = addConsumer;
			}
			else if (producer is SplitterPipe splitter)
			{
				splitter.Add(addConsumer);
			}
			else
			{
				splitter = new SplitterPipe();
				splitter.Add(addConsumer);
				splitter.Add(producer.OutStream);
				producer.OutStream = splitter;
			}
			return addConsumer;
		}

		public static T Chain<T>(this IAudioActiveProducer producer, Action<T> init = null) where T : IAudioPassiveConsumer, new()
		{
			var addConsumer = new T();
			init?.Invoke(addConsumer);
			return producer.Chain(addConsumer);
		}
	}
}
