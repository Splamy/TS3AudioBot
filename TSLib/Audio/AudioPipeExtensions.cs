// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Audio
{
	public static class AudioPipeExtensions
	{
		public static T Chain<T>(this IAudioActiveProducer producer, T addConsumer) where T : IAudioPassiveConsumer
		{
			if (producer.OutStream is null)
			{
				producer.OutStream = addConsumer;
			}
			else if (producer.OutStream is PassiveSplitterPipe splitter)
			{
				splitter.Add(addConsumer);
			}
			else
			{
				splitter = new PassiveSplitterPipe();
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

		public static T Into<T>(this IAudioPassiveProducer producer, T reader) where T : IAudioActiveConsumer, new()
		{
			reader.InStream = producer;
			return reader;
		}

		public static T Into<T>(this IAudioPassiveProducer producer, Action<T> init = null) where T : IAudioActiveConsumer, new()
		{
			var reader = new T();
			init?.Invoke(reader);
			return producer.Into(reader);
		}
	}
}
