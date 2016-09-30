using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3Client.Full
{
	internal class RingQueue<T> : IEnumerable<T>
	{
		private int currentStart;
		private int length;
		private T[] ringBuffer;
		private bool[] ringDoneState;

		public int StartIndex { get; private set; }
		public int EndIndex => StartIndex + length;

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
			length++;
		}

		public bool TryDequeue(out T obj)
		{
			if (ringDoneState[currentStart] != true) { obj = default(T); return false; }

			ringDoneState[currentStart] = false;
			obj = ringBuffer[currentStart];

			StartIndex++;
			length--;
			currentStart = (currentStart + 1) % ringBuffer.Length;
			return true;
		}

		public bool IsSet(int index)
		{
			if (index < StartIndex) return true;
			if (index >= EndIndex) return false;
			return ringDoneState[IndexToLocal(index)];
		}

		private int IndexToLocal(int index) => currentStart + index % ringBuffer.Length;

		public void Clear()
		{
			currentStart = 0;
			StartIndex = 0;
			length = 0;
		}

		#region IEnumerable

		public IEnumerator<T> GetEnumerator() => new RingQueueEnumerator(this, currentStart);
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private class RingQueueEnumerator : IEnumerator<T>
		{
			private readonly RingQueue<T> parent;
			private readonly int startIndex;
			private int index;
			public RingQueueEnumerator(RingQueue<T> parent, int startIndex)
			{
				this.parent = parent;
				this.startIndex = startIndex;
				index = startIndex;
			}

			public void Dispose() { }

			public bool MoveNext()
			{
				if (index > startIndex + parent.ringBuffer.Length) return false;
				index++;
				return true;
			}

			public void Reset() => index = startIndex;

			public T Current => parent.ringBuffer[index % parent.ringBuffer.Length];

			object IEnumerator.Current => Current;
		}

		#endregion
	}
}
