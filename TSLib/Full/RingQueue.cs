// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Full
{
	/// <summary>Provides a ring queue with packet offset and direct item access functionality.</summary>
	/// <typeparam name="T">Item type</typeparam>
	public sealed class RingQueue<T>
	{
		private const int InitialBufferSize = 16;

		private int currentStart;
		private T[] ringBuffer;
		private bool[] ringBufferSet;

		public int Count { get; private set; } // = currentLength
		public int MaxBufferSize { get; }
		public GenerationWindow Window { get; }

		public RingQueue(int maxBufferSize, int mod)
		{
			if (maxBufferSize >= mod)
				throw new ArgumentOutOfRangeException(nameof(mod), "Modulo must be bigger than buffer size");
			MaxBufferSize = maxBufferSize;
			var setBufferSize = Math.Min(InitialBufferSize, MaxBufferSize);
			ringBuffer = new T[setBufferSize];
			ringBufferSet = new bool[setBufferSize];
			Window = new GenerationWindow(mod, MaxBufferSize);
			Clear();
		}

		#region mapping ring array to flat [0 - size] array

		private void BufferSet(int index, T value)
		{
			BufferExtend(index);
			int local = IndexToLocal(index);
			int newLength = local - currentStart + 1 + (local >= currentStart ? 0 : ringBuffer.Length);
			Count = Math.Max(Count, newLength);
			ringBuffer[local] = value;
			ringBufferSet[local] = true;
		}

		private ref T BufferGet(int index)
		{
			BufferExtend(index);
			int local = IndexToLocal(index);
			return ref ringBuffer[local];
		}

		private bool StateGet(int index)
		{
			BufferExtend(index);
			int local = IndexToLocal(index);
			return ringBufferSet[local];
		}

		private void BufferPop()
		{
			ringBufferSet[currentStart] = false;
			ringBuffer[currentStart] = default;
			currentStart = (currentStart + 1) % ringBuffer.Length;
			Count--;
		}

		private void BufferExtend(int index)
		{
			if (index < ringBuffer.Length)
				return;
			if (index >= MaxBufferSize)
				throw new ArgumentOutOfRangeException(nameof(index), "The index does not fit into the maximal buffer size");
			int extendTo = index < ringBuffer.Length * 2
				? Math.Min(ringBuffer.Length * 2, MaxBufferSize)
				: Math.Min(index + ringBuffer.Length, MaxBufferSize);
			var extRingBuffer = new T[extendTo];
			var extRingBufferSet = new bool[extendTo];
			Array.Copy(ringBuffer, currentStart, extRingBuffer, 0, ringBuffer.Length - currentStart);
			Array.Copy(ringBufferSet, currentStart, extRingBufferSet, 0, ringBufferSet.Length - currentStart);
			Array.Copy(ringBuffer, 0, extRingBuffer, ringBuffer.Length - currentStart, currentStart);
			Array.Copy(ringBufferSet, 0, extRingBufferSet, ringBufferSet.Length - currentStart, currentStart);
			currentStart = 0;
			ringBuffer = extRingBuffer;
			ringBufferSet = extRingBufferSet;
		}

		private int IndexToLocal(int index) => (currentStart + index) % ringBuffer.Length;

		#endregion

		public void Set(int mappedValue, T value)
		{
			int index = Window.MappedToIndex(mappedValue);
			if (IsSetIndex(index) != ItemSetStatus.InWindowNotSet)
				throw new ArgumentOutOfRangeException(nameof(mappedValue), "Object cannot be set.");

			BufferSet(index, value);
		}

		public ItemSetStatus IsSet(int mappedValue)
		{
			int index = Window.MappedToIndex(mappedValue);
			return IsSetIndex(index);
		}

		private ItemSetStatus IsSetIndex(int index)
		{
			if (index < 0)
				return ItemSetStatus.OutOfWindowSet;
			if (index > Count && index < MaxBufferSize)
				return ItemSetStatus.InWindowNotSet;
			if (index >= MaxBufferSize)
				return ItemSetStatus.OutOfWindowNotSet;
			return StateGet(index) ? ItemSetStatus.InWindowSet : ItemSetStatus.InWindowNotSet;
		}

		public bool TryDequeue(out T value)
		{
			if (!TryPeekStart(0, out value)) return false;
			BufferPop();
			Window.Advance(1);
			return true;
		}

		public bool TryPeekStart(int index, out T value)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));

			if (index >= Count || Count <= 0 || !StateGet(index))
			{
				value = default;
				return false;
			}
			else
			{
				value = BufferGet(index);
				return true;
			}
		}

		public void Clear()
		{
			currentStart = 0;
			Count = 0;
			Array.Clear(ringBufferSet, 0, ringBufferSet.Length);
			Window.Reset();
		}
	}

	//  X   |OXXOOOOOO|  X
	//  |     |   |      |
	//  |     |   |      OutOfWindowNotSet
	//  |     |   InWindowNotSet
	//  |     InWindowSet
	//  OutOfWindowSet

	[Flags]
	public enum ItemSetStatus
	{
		NotSet = 0b00,
		Set = 0b01,
		OutOfWindow = 0b00,
		InWindow = 0b10,

		OutOfWindowNotSet = OutOfWindow | NotSet,
		OutOfWindowSet = OutOfWindow | Set,
		InWindowNotSet = InWindow | NotSet,
		InWindowSet = InWindow | Set,
	}
}
