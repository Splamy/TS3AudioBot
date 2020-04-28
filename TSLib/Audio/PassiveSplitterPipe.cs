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
using System.Linq;

namespace TSLib.Audio
{
	public class PassiveSplitterPipe : IAudioPipe
	{
		public bool Active => consumerList.Count > 0 && consumerList.Any(x => x.Active);
		private readonly List<IAudioPassiveConsumer> safeConsumerList = new List<IAudioPassiveConsumer>();
		private readonly List<IAudioPassiveConsumer> consumerList = new List<IAudioPassiveConsumer>();
		private bool changed;
		private readonly object listLock = new object();
		private byte[] buffer = Array.Empty<byte>();

		public bool CloneMeta { get; set; } = false;

		public IAudioPassiveConsumer? OutStream
		{
			get => this;
			set
			{
				if (value is null)
					Clear();
				else
					Add(value);
			}
		}

		public void Add(IAudioPassiveConsumer consumer)
		{
			lock (listLock)
			{
				if (!consumerList.Contains(consumer) && consumer != this)
				{
					consumerList.Add(consumer);
					changed = true;
				}
			}
		}

		public void Remove(IAudioPassiveConsumer consumer)
		{
			lock (listLock)
			{
				changed |= consumerList.Remove(consumer);
			}
		}

		public void Clear()
		{
			lock (listLock)
			{
				changed |= consumerList.Count > 0;
				consumerList.Clear();
			}
		}

		public void Write(Span<byte> data, Meta? meta)
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

			if (buffer.Length < data.Length)
				buffer = new byte[data.Length];

			var bufSpan = buffer.AsSpan(0, data.Length);
			for (int i = 0; i < safeConsumerList.Count - 1; i++)
			{
				data.CopyTo(bufSpan);
				safeConsumerList[i].Write(bufSpan, meta);
			}
			// safe one memcopy call by passing the last one our original data
			safeConsumerList[^1].Write(data, meta);
		}
	}
}
