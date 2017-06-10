// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3Client.Full
{
	using System;

	public class RingQueue<T>
	{
		private int currentStart;
		private int currentLength;
		private T[] ringBuffer;
		private bool[] ringBufferSet;

		private int mappedBaseOffset;
		private readonly int mappedMod;
		private uint generation;

		public int Count => currentLength;

		public RingQueue(int bufferSize, int mod)
		{
			if (bufferSize >= mod)
				throw new ArgumentOutOfRangeException(nameof(mod), "Modulo must be smaller than buffer size");
			ringBuffer = new T[bufferSize];
			ringBufferSet = new bool[bufferSize];
			mappedMod = mod;
			Clear();
		}

		#region mapping ring array to flat [0 - size] array

		private void BufferSet(int index, T value)
		{
			if (index > ringBuffer.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			int local = IndexToLocal(index);
			int newLength = local - currentStart + 1 + (local >= currentStart ? 0 : ringBuffer.Length);
			currentLength = Math.Max(currentLength, newLength);
			ringBuffer[local] = value;
			ringBufferSet[local] = true;
		}

		private T BufferGet(int index)
		{
			if (index > ringBuffer.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			int local = IndexToLocal(index);
			return ringBuffer[local];
		}

		private bool StateGet(int index)
		{
			if (index > ringBuffer.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
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

		private int IndexToLocal(int index) => (currentStart + index) % ringBuffer.Length;

		#endregion

		public void Set(int mappedValue, T value)
		{
			int index = MappedToIndex(mappedValue);
			if (index > ringBuffer.Length)
				throw new ArgumentOutOfRangeException(nameof(mappedValue), "Buffer is not large enough for this object.");
			if (IsSet(mappedValue))
				throw new ArgumentOutOfRangeException(nameof(mappedValue), "Object already set.");

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

		public bool IsSet(int mappedValue)
		{
			int index = MappedToIndex(mappedValue);
			if (index < 0)
				return true;
			if (index > currentLength)
				return false;
			return StateGet(index);
		}

		public bool IsNextGen(int mappedValue) => mappedBaseOffset > mappedMod - ringBuffer.Length && mappedValue < ringBuffer.Length;

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
}
