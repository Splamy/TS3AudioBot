using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3Client.Full
{
	internal class RingQueue<T>
	{
		private int currentStart;
		private T[] ringBuffer;
		private bool[] ringDoneState;

		public int StartIndex { get; private set; }
		public int EndIndex => StartIndex + Count;
		public int Count { get; private set; }

		public RingQueue(int bufferSize)
		{
			ringBuffer = new T[bufferSize];
			ringDoneState = new bool[bufferSize];
			Clear();
		}

		public bool Fits(int index) => index < StartIndex + ringBuffer.Length;

		public void Set(T data, int index)
		{
			if (!Fits(index))
				throw new ArgumentOutOfRangeException(nameof(index), "Buffer is not large enough for this object.");
			if (IsSet(index))
				throw new ArgumentOutOfRangeException(nameof(index), "Object already set.");

			int localIndex = IndexToLocal(index);
			ringBuffer[localIndex] = data;
			ringDoneState[localIndex] = true;
			Count++;
		}

		public bool TryDequeue(out T obj)
		{
			if (!TryPeek(StartIndex, out obj)) return false;

			ringDoneState[currentStart] = false;

			StartIndex++;
			Count--;
			currentStart = (currentStart + 1) % ringBuffer.Length;
			return true;
		}

		public bool TryPeek(int index, out T obj)
		{
			int localIndex = IndexToLocal(index);
			if (ringDoneState[localIndex] != true) { obj = default(T); return false; }
			else { obj = ringBuffer[localIndex]; return true; }
		}

		public bool IsSet(int index)
		{
			if (index < StartIndex) return true;
			if (index >= EndIndex) return false;
			return ringDoneState[IndexToLocal(index)];
		}

		private int IndexToLocal(int index) => (currentStart + index - StartIndex) % ringBuffer.Length;

		public void Clear()
		{
			currentStart = 0;
			StartIndex = 0;
			Count = 0;
		}
	}
}
