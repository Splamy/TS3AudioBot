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
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;

	public class PassiveMergePipe : IAudioPassiveProducer
	{
		private readonly List<IAudioPassiveProducer> safeProducerList = new List<IAudioPassiveProducer>();
		private readonly List<IAudioPassiveProducer> producerList = new List<IAudioPassiveProducer>();
		private readonly object listLock = new object();
		private bool changed;
		private readonly int[] accBuffer = new int[4096];

		public void Add(IAudioPassiveProducer addProducer)
		{
			if (!producerList.Contains(addProducer) && addProducer != this)
			{
				lock (listLock)
				{
					producerList.Add(addProducer);
					changed = true;
				}
			}
		}

		public void Remove(IAudioPassiveProducer removeProducer)
		{
			if (producerList.Contains(removeProducer) && removeProducer != this)
			{
				lock (listLock)
				{
					producerList.Remove(removeProducer);
					changed = true;
				}
			}
		}

		public int Read(byte[] buffer, int offset, int length, out Meta meta)
		{
			if (changed)
			{
				lock (listLock)
				{
					if (changed)
					{
						safeProducerList.Clear();
						safeProducerList.AddRange(producerList);
						changed = false;
					}
				}
			}

			meta = null;

			if (safeProducerList.Count == 0)
				return 0;

			if (safeProducerList.Count == 1)
				return safeProducerList[0].Read(buffer, offset, length, out meta);

			int maxReadLength = Math.Min(accBuffer.Length, length);
			Array.Clear(accBuffer, 0, maxReadLength);

			var pcmBuffer = MemoryMarshal.Cast<byte, short>(buffer);
			int read = 0;

			foreach (var producer in safeProducerList)
			{
				int ppread = producer.Read(buffer, offset, maxReadLength, out meta);
				if (ppread == 0)
					continue;

				read = Math.Max(read, ppread);
				for (int i = 0; i < ppread / 2; i++)
					accBuffer[i] += pcmBuffer[i];
			}

			for (int i = 0; i < read / 2; i++)
				pcmBuffer[i] = (short)Math.Max(Math.Min(accBuffer[i], short.MaxValue), short.MinValue);

			return read;
		}
	}
}
