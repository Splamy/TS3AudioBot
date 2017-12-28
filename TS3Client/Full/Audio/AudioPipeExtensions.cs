namespace TS3Client.Full.Audio
{
	public static class AudioPipeExtensions
	{
		public static TC Chain<TC>(this IAudioActiveProducer producer, TC addConsumer) where TC : IAudioPassiveConsumer
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

		public static T ChainNew<T>(this IAudioActiveProducer producer) where T : IAudioPassiveConsumer, new()
		{
			var addConsumer = new T();
			return producer.Chain(addConsumer);
		}
	}
}
