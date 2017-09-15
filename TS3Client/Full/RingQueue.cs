// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full
{
	using System;

	public class RingQueue<T>
	{
		private const int InitialBufferSize = 16;

		private int currentStart;
		private int currentLength;
		private T[] ringBuffer;
		private bool[] ringBufferSet;

		private int mappedBaseOffset;
		private readonly int mappedMod;
		private uint generation;

		public int MaxBufferSize { get; }
		public int Count => currentLength;

		public RingQueue(int maxBufferSize, int mod)
		{
			if (maxBufferSize == -1)
			{
				MaxBufferSize = (mod / 2) - 1;
			}
			else
			{
				if (maxBufferSize >= mod)
					throw new ArgumentOutOfRangeException(nameof(mod), "Modulo must be bigger than buffer size");
				MaxBufferSize = maxBufferSize;
			}
			var setBufferSize = Math.Min(InitialBufferSize, maxBufferSize);
			ringBuffer = new T[setBufferSize];
			ringBufferSet = new bool[setBufferSize];
			mappedMod = mod;
			Clear();
		}

		#region mapping ring array to flat [0 - size] array

		private void BufferSet(int index, T value)
		{
			BufferExtend(index);
			int local = IndexToLocal(index);
			int newLength = local - currentStart + 1 + (local >= currentStart ? 0 : ringBuffer.Length);
			currentLength = Math.Max(currentLength, newLength);
			ringBuffer[local] = value;
			ringBufferSet[local] = true;
		}

		private T BufferGet(int index)
		{
			BufferExtend(index);
			int local = IndexToLocal(index);
			return ringBuffer[local];
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
			// clear data to allow them to be collected by gc
			// when in debug it might be nice to see what was there
#if !DEBUG
			ringBuffer[currentStart] = default(T);
#endif
			currentStart = (currentStart + 1) % ringBuffer.Length;
			currentLength--;
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
			Array.Copy(ringBuffer, 0, extRingBuffer, currentStart, currentStart);
			Array.Copy(ringBufferSet, 0, extRingBufferSet, currentStart, currentStart);
			currentStart = 0;
			ringBuffer = extRingBuffer;
			ringBufferSet = extRingBufferSet;
		}

		private int IndexToLocal(int index) => (currentStart + index) % ringBuffer.Length;

		#endregion

		public void Set(int mappedValue, T value)
		{
			int index = MappedToIndex(mappedValue);
			if (IsSetIndex(index) != ItemSetStatus.InWindowNotSet)
				throw new ArgumentOutOfRangeException(nameof(mappedValue), "Object cannot be set.");

			BufferSet(index, value);
		}

		private int MappedToIndex(int mappedValue)
		{
			if (mappedValue >= mappedMod)
				throw new ArgumentOutOfRangeException(nameof(mappedValue));

			if (IsNextGen(mappedValue))
			{
				// | XX             X>    | <= The part from BaseOffset to MappedMod is small enough to consider packets with wrapped numbers again
				//   /\ NewValue    /\ BaseOffset
				return (mappedValue + mappedMod) - mappedBaseOffset;
			}
			else
			{
				// |  X>             XX   |
				//    /\ BaseOffset  /\ NewValue    // normal case
				return mappedValue - mappedBaseOffset;
			}
		}

		public ItemSetStatus IsSet(int mappedValue)
		{
			int index = MappedToIndex(mappedValue);
			return IsSetIndex(index);
		}

		private ItemSetStatus IsSetIndex(int index)
		{
			if (index < 0)
				return ItemSetStatus.OutOfWindowSet;
			if (index > currentLength && index < MaxBufferSize)
				return ItemSetStatus.InWindowNotSet;
			if (index >= MaxBufferSize)
				return ItemSetStatus.OutOfWindowNotSet;
			return StateGet(index) ? ItemSetStatus.InWindowSet : ItemSetStatus.InWindowNotSet;
		}

		public bool IsNextGen(int mappedValue) => mappedBaseOffset > mappedMod - MaxBufferSize && mappedValue < MaxBufferSize;

		public uint GetGeneration(int mappedValue) => (uint)(generation + (IsNextGen(mappedValue) ? 1 : 0));

		public bool TryDequeue(out T value)
		{
			if (!TryPeekStart(0, out value)) return false;
			BufferPop();
			mappedBaseOffset = (mappedBaseOffset + 1) % mappedMod;
			if (mappedBaseOffset == 0)
				generation++;
			return true;
		}

		public bool TryPeekStart(int index, out T value)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));

			if (index >= Count || currentLength <= 0 || !StateGet(index))
			{
				value = default(T);
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
			currentLength = 0;
			Array.Clear(ringBufferSet, 0, ringBufferSet.Length);
			mappedBaseOffset = 0;
			generation = 0;
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
