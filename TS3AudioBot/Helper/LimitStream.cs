// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.IO;

namespace TS3AudioBot.Helper
{
	public class LimitStream : Stream
	{
		private readonly Stream baseStream;

		public LimitStream(Stream baseStream, long maxLength)
		{
			this.baseStream = baseStream;
			MaxLength = maxLength;
		}

		public long MaxLength { get; }
		public long IOBytes { get; private set; }

		public override bool CanRead => baseStream.CanRead;
		public override bool CanSeek => false;
		public override bool CanWrite => baseStream.CanWrite;
		public override long Length => baseStream.Length;
		public override long Position { get => baseStream.Position; set => baseStream.Position = value; }

		public override void Flush() => baseStream.Flush();

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (IOBytes + count > MaxLength)
			{
				count = (int)(MaxLength - IOBytes);
				if (count <= 0)
					throw new EntityTooLargeException(MaxLength);
			}
			int read = baseStream.Read(buffer, offset, count);
			IOBytes += read;
			return read;
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => baseStream.SetLength(value);

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (IOBytes + count > MaxLength)
				throw new EntityTooLargeException(MaxLength);
			IOBytes += count;
			baseStream.Write(buffer, offset, count);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				baseStream.Dispose();
			}
		}
	}

	[Serializable]
	public class EntityTooLargeException : Exception
	{
		private const string ErrMsg = "Content exceeds the limit of {0} bytes";

		public EntityTooLargeException() { }
		public EntityTooLargeException(long maxLen) : base(string.Format(ErrMsg, maxLen)) { }
	}
}
