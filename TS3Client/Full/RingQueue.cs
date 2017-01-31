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
