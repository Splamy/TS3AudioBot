// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace TSLib.Audio
{
	public class PassiveMergePipe : IAudioPassiveProducer, IEnumerable<IAudioPassiveProducer>, ICollection<IAudioPassiveProducer>
	{
		private IAudioPassiveProducer[] safeProducerList = Array.Empty<IAudioPassiveProducer>();
		private readonly List<IAudioPassiveProducer> producerList = new List<IAudioPassiveProducer>();
		private readonly object listLock = new object();
		private bool changed;
		private readonly int[] accBuffer = new int[4096];

		public int Count => safeProducerList.Length;
		public bool IsReadOnly => false;

		public void Add(IAudioPassiveProducer addProducer)
		{
			if (!producerList.Contains(addProducer) && addProducer != this)
			{
				lock (listLock)
				{
					if (!producerList.Contains(addProducer))
					{
						producerList.Add(addProducer);
						changed = true;
					}
				}
			}
		}

		public bool Remove(IAudioPassiveProducer removeProducer)
		{
			if (producerList.Contains(removeProducer) && removeProducer != this)
			{
				lock (listLock)
				{
					var removed = producerList.Remove(removeProducer);
					changed |= removed;
					return removed;
				}
			}
			return false;
		}

		public void Clear()
		{
			lock (listLock)
			{
				producerList.Clear();
				changed = true;
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
						safeProducerList = producerList.ToArray();
						changed = false;
					}
				}
			}

			meta = null;

			if (safeProducerList.Length == 0)
				return 0;

			if (safeProducerList.Length == 1)
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

		IEnumerator<IAudioPassiveProducer> IEnumerable<IAudioPassiveProducer>.GetEnumerator() => ((IEnumerable<IAudioPassiveProducer>)safeProducerList).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => safeProducerList.GetEnumerator();

		public bool Contains(IAudioPassiveProducer item) => Enumerable.Contains(safeProducerList, item);

		public void CopyTo(IAudioPassiveProducer[] array, int arrayIndex) => Array.Copy(safeProducerList, 0, array, arrayIndex, array.Length);

		public void Dispose()
		{
			var list = safeProducerList;
			Clear();
			foreach (var producer in list)
				producer.Dispose();
		}
	}
}
