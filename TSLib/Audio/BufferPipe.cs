using System;
using TSLib.Helper;
using TSLib.Scheduler;

namespace TSLib.Audio
{
	public class BufferPipe : IAudioPassiveConsumer, IAudioPassiveProducer
	{
		public bool Active => true;
		private readonly ByteQueue queue = new ByteQueue();
		private int upperBufferByteSize;
		private int lowerBufferByteSize;
		private TimeSpan upperBufferDuration;
		private TimeSpan lowerBufferDuration;

		/// <summary>Up to where data should be buffered.</summary>
		public TimeSpan UpperBufferDuration
		{
			get => upperBufferDuration;
			set
			{
				upperBufferDuration = value;
				upperBufferByteSize = SampleInfo.TimeToByteCount(value);
			}
		}
		/// <summary>At which lower size the buffer task should be started again.</summary>
		public TimeSpan LowerBufferDuration
		{
			get => lowerBufferDuration;
			set
			{
				lowerBufferDuration = value;
				lowerBufferByteSize = SampleInfo.TimeToByteCount(value);
			}
		}

		public IAudioPassiveProducer? InStream { get; set; }
		public SampleInfo SampleInfo { get; }
		private Mode BufferMode;
		private readonly object bufferLock = new object();
		private readonly DedicatedTaskScheduler scheduler;
		private const int ReadSize = 4096;

		public BufferPipe(SampleInfo sampleInfo, DedicatedTaskScheduler scheduler)
		{
			this.scheduler = scheduler;
			SampleInfo = sampleInfo;
			BufferMode = Mode.BufferFill;
			UpperBufferDuration = TimeSpan.FromSeconds(1);
		}

		public void Write(Span<byte> data, Meta? meta)
		{
			lock (bufferLock)
			{
				switch (BufferMode)
				{
				case Mode.BufferFill:
				case Mode.Hybrid:
					queue.Enqueue(data);
					if (queue.Length > upperBufferByteSize)
						BufferMode = Mode.BufferRead;
					break;
				case Mode.BufferRead:
					if (queue.Length == 0)
					{
						BufferMode = Mode.BufferFill;
						// goto ?
					}
					else if (queue.Length < lowerBufferByteSize)
					{
						BufferMode = Mode.Hybrid;
						// goto ?
					}
					break;

				default:
					throw Tools.UnhandledDefault(BufferMode);
				}
			}
		}

		public int Read(Span<byte> data, out Meta? meta)
		{
			meta = null;
			lock (bufferLock)
			{
				if (BufferMode == Mode.BufferFill)
					return 0;
				int read = queue.Dequeue(data);
				if (BufferMode == Mode.BufferRead && queue.Length < lowerBufferByteSize)
					return 0;
				return read;
			}
		}

		private enum Mode
		{
			/// <summary>Only writing into the buffer. Read will return 0.</summary>
			BufferFill,
			/// <summary>Filling the buffer and reading are both allowed.</summary>
			Hybrid,
			/// <summary>Reading is allowed. The buffer will currently not be filled.</summary>
			BufferRead,
		}

		// TODO replace with channel ?

		// Source: https://github.com/kelindar/circular-buffer
		/*************************************************************************
		 * 
		 * The MIT License (MIT)
		 * 
		 * Copyright (c) 2014 Roman Atachiants (kelindar@gmail.com)

		 * Permission is hereby granted, free of charge, to any person obtaining a copy
		 * of this software and associated documentation files (the "Software"), to deal
		 * in the Software without restriction, including without limitation the rights
		 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
		 * copies of the Software, and to permit persons to whom the Software is
		 * furnished to do so, subject to the following conditions:
		 * 
		 * The above copyright notice and this permission notice shall be included in
		 * all copies or substantial portions of the Software.
		 * 
		 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
		 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
		 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
		 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
		 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
		 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
		 * THE SOFTWARE.
		*************************************************************************/

		/// <summary>
		/// Defines a class that represents a resizable circular byte queue.
		/// </summary>
		public sealed class ByteQueue
		{
			// Private fields
			private int fHead;
			private int fTail;
			private byte[] fInternalBuffer = Array.Empty<byte>();

			/// <summary>
			/// Gets the length of the byte queue
			/// </summary>
			public int Length { get; private set; } = 0;

			// TODO
			public bool AutoGrow { get; set; } = false;

			public ByteQueue() { }

			/// <summary>
			/// Constructs a new instance of a byte queue.
			/// </summary>
			public ByteQueue(int size)
			{
				fInternalBuffer = new byte[RoundTo2048(size)];
			}

			/// <summary>
			/// Clears the byte queue
			/// </summary>
			public void Clear()
			{
				fHead = 0;
				fTail = 0;
				Length = 0;
			}

			/// <summary>
			/// Extends the capacity of the bytequeue
			/// </summary>
			private void SetCapacity(int capacity)
			{
				byte[] newBuffer = new byte[capacity];

				if (Length > 0)
				{
					if (fHead < fTail)
					{
						fInternalBuffer.AsSpan(fHead, Length).CopyTo(newBuffer);
						//Buffer.BlockCopy(fInternalBuffer, fHead, newBuffer, 0, fSize);
					}
					else
					{
						var fInternalSpan = fInternalBuffer.AsSpan();
						fInternalSpan[fHead..].CopyTo(newBuffer);
						//Buffer.BlockCopy(fInternalBuffer, fHead, newBuffer, 0, fInternalBuffer.Length - fHead);
						fInternalSpan.Slice(0, fInternalSpan.Length - fHead).CopyTo(newBuffer.AsSpan(fInternalBuffer.Length - fHead));
						//Buffer.BlockCopy(fInternalBuffer, 0, newBuffer, fInternalBuffer.Length - fHead, fTail);
					}
				}

				fHead = 0;
				fTail = Length;
				fInternalBuffer = newBuffer;
			}

			/// <summary>
			/// Enqueues a buffer to the queue and inserts it to a correct position
			/// </summary>
			/// <param name="buffer">Buffer to enqueue</param>
			/// <param name="offset">The zero-based byte offset in the buffer</param>
			/// <param name="size">The number of bytes to enqueue</param>
			public void Enqueue(ReadOnlySpan<byte> buffer)
			{
				if (buffer.Length == 0)
					return;

				var fInternalSpan = fInternalBuffer.AsSpan();

				if ((Length + buffer.Length) > fInternalSpan.Length)
					SetCapacity(RoundTo2048(Length + buffer.Length));

				if (fHead < fTail)
				{
					int rightLength = (fInternalSpan.Length - fTail);

					if (rightLength >= buffer.Length)
					{
						buffer.CopyTo(fInternalSpan[fTail..]);
						//Buffer.BlockCopy(buffer, offset, fInternalBuffer, fTail, size);
					}
					else
					{
						buffer[..rightLength].CopyTo(fInternalSpan[fTail..]);
						//Buffer.BlockCopy(buffer, offset, fInternalBuffer, fTail, rightLength);
						buffer[rightLength..].CopyTo(fInternalSpan);
						//Buffer.BlockCopy(buffer, offset + rightLength, fInternalBuffer, 0, size - rightLength);
					}
				}
				else
				{
					buffer.CopyTo(fInternalSpan[fTail..]);
				}

				fTail = (fTail + buffer.Length) % fInternalBuffer.Length;
				Length += buffer.Length;
			}

			/// <summary>
			/// Dequeues a buffer from the queue
			/// </summary>
			/// <param name="buffer">Buffer to enqueue</param>
			/// <param name="offset">The zero-based byte offset in the buffer</param>
			/// <param name="size">The number of bytes to dequeue</param>
			/// <returns>Number of bytes dequeued</returns>
			public int Dequeue(Span<byte> buffer)
			{
				if (Length == 0 || buffer.Length == 0)
					return 0;

				if (buffer.Length > Length)
					buffer = buffer[..Length];

				var fInternalSpan = fInternalBuffer.AsSpan();

				if (fHead < fTail)
				{
					fInternalSpan.Slice(fHead, buffer.Length).CopyTo(buffer);
					//Buffer.BlockCopy(fInternalSpan, fHead, buffer, offset, size);
				}
				else
				{
					int rightLength = (fInternalSpan.Length - fHead);

					if (rightLength >= buffer.Length)
					{
						fInternalSpan.Slice(fHead, buffer.Length).CopyTo(buffer);
						//Buffer.BlockCopy(fInternalBuffer, fHead, buffer, offset, size);
					}
					else
					{
						fInternalSpan[fHead..].CopyTo(buffer);
						//Buffer.BlockCopy(fInternalBuffer, fHead, buffer, offset, rightLength);
						fInternalSpan[..(buffer.Length - rightLength)].CopyTo(buffer[rightLength..]);
						//Buffer.BlockCopy(fInternalBuffer, 0, buffer, offset + rightLength, size - rightLength);
					}
				}

				fHead = (fHead + buffer.Length) % fInternalSpan.Length;
				Length -= buffer.Length;

				if (Length == 0)
				{
					fHead = 0;
					fTail = 0;
				}

				return buffer.Length;
			}

			private static int RoundTo2048(int num) => (num + 2047) & ~2047;
		}
	}
}
