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
	using System.IO;
	using System.Runtime.CompilerServices;

	/// <summary>An alternative QuickLZ compression implementation for C#.</summary>
	public static class QuickerLz
	{
		private const int TableSize = 4096;
		private const uint SetControl = 0x8000_0000;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetCompressedSize(byte[] data) => (data[0] & 0x02) != 0 ? ReadI32(data, 1) : data[1];

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetDecompressedSize(byte[] data) => (data[0] & 0x02) != 0 ? ReadI32(data, 5) : data[2];

		[ThreadStatic]
		private static int[] hashtable;
		[ThreadStatic]
		private static bool[] hashCounter;
		[ThreadStatic]
		private static int[] cachetable;

		public static ArrLim Compress(byte[] data, int level)
		{
			if (level != 1) // && level != 3
				throw new ArgumentException("This QuickLZ implementation supports only level 1 and 3 compress");
			if (data.LongLength >= int.MaxValue)
				throw new ArgumentException($"This QuickLZ can only compress up to {int.MaxValue}");

			int headerlen = data.Length < 216 ? 3 : 9;
			var dest = new byte[data.Length + 400];
			int destPos = headerlen + 4;

			uint control = SetControl;
			int controlPos = headerlen;
			int sourcePos = 0;

			if (level == 1)
			{
				int unmatched = 0;

				if (hashtable == null) hashtable = new int[TableSize];
				if (hashCounter == null) hashCounter = new bool[TableSize];
				else Array.Clear(hashCounter, 0, TableSize);
				if (cachetable == null) cachetable = new int[TableSize];
				else Array.Clear(cachetable, 0, TableSize);

				int sourceLimit = data.Length - 10;
				while (sourcePos < sourceLimit)
				{
					if ((control & 1) != 0)
					{
						if (sourcePos > data.Length / 2 && destPos > sourcePos - (sourcePos / 32))
						{
							Array.Copy(data, 0, dest, headerlen, data.Length);
							destPos = headerlen + data.Length;
							WriteHeader(dest, destPos, data.Length, level, headerlen, false);
							return new ArrLim(dest, destPos);
						}
						WriteU32(dest, controlPos, (control >> 1) | SetControl); // C
						controlPos = destPos;
						destPos += 4;
						control = SetControl;
					}

					var next = Read24(data, sourcePos);
					var hash = Hash(next);
					var offset = hashtable[hash];
					var chache = cachetable[hash];
					cachetable[hash] = next;
					hashtable[hash] = sourcePos;

					if (chache == next
						&& hashCounter[hash]
						&& (sourcePos - offset >= 3
							|| sourcePos == offset + 1
							&& unmatched >= 3
							&& sourcePos > 3
							&& Is6Same(data, sourcePos - 3)))
					{
						control = (control >> 1) | SetControl;
						int matchlen = 3;
						int remainder = Math.Min(data.Length - 4 - sourcePos, 0xFF);
						while (data[offset + matchlen] == data[sourcePos + matchlen] && matchlen < remainder)
							matchlen++;
						if (matchlen < 18)
						{
							WriteU16(dest, destPos, hash << 4 | (matchlen - 2));
							destPos += 2;
						}
						else
						{
							Write24(dest, destPos, hash << 4 | (matchlen << 16));
							destPos += 3;
						}
						sourcePos += matchlen;
						unmatched = 0;
					}
					else
					{
						unmatched++;
						hashCounter[hash] = true;

						dest[destPos++] = data[sourcePos++];
						control >>= 1;
					}
				}
			}

			while (sourcePos < data.Length)
			{
				if ((control & 1) != 0)
				{
					WriteU32(dest, controlPos, (control >> 1) | SetControl); // C
					controlPos = destPos;
					destPos += 4;
					control = SetControl;
				}
				dest[destPos++] = data[sourcePos++];
				control >>= 1;
			}

			while ((control & 1) == 0)
				control >>= 1;
			WriteU32(dest, controlPos, (control >> 1) | SetControl); // C

			WriteHeader(dest, destPos, data.Length, level, headerlen, true);
			return new ArrLim(dest, destPos);
		}

		public static byte[] Decompress(byte[] data, int maxSize)
		{
			// Read header
			byte flags = data[0];
			int level = (flags >> 2) & 0b11;
			if (level != 3 && level != 1)
				throw new NotSupportedException("This QuickLZ implementation supports only level 1 and 3 decompress");

			int headerlen = (flags & 0x02) != 0 ? 9 : 3;
			int compressedSize = GetCompressedSize(data);
			int decompressedSize = GetDecompressedSize(data);

			if (decompressedSize >= maxSize)
				throw new NotSupportedException("Maximum uncompressed size exceeded");

			var dest = new byte[decompressedSize];

			if ((flags & 0x01) == 0)
			{
				// Uncompressed
				if (compressedSize - headerlen != decompressedSize)
					throw new InvalidDataException("Compressed and uncompressed size of uncompressed data do not match");
				Array.Copy(data, headerlen, dest, 0, decompressedSize);
				return dest;
			}

			if (level == 1)
			{
				uint control = 1;
				int sourcePos = headerlen;
				int destPos = 0;
				int nextHashed = 0;

				if (hashtable == null) hashtable = new int[TableSize];
				Array.Clear(hashtable, 0, TableSize);

				while (true)
				{
					if (control == 1)
					{
						control = ReadU32(data, sourcePos);
						sourcePos += 4;
					}

					if ((control & 1) != 0)
					{
						// Found a reference
						control >>= 1;
						byte next = data[sourcePos++];
						int hash = (next >> 4) | (data[sourcePos++] << 4);

						int matchlen = next & 0x0F;
						if (matchlen != 0)
							matchlen += 2;
						else
							matchlen = data[sourcePos++];

						int offset = hashtable[hash];
						destPos = CopyBufferBytes(dest, destPos, offset, matchlen);

						int end = destPos + 1 - matchlen;
						UpdateHashtable(dest, nextHashed, end);
						nextHashed = destPos;
					}
					else if (destPos >= Math.Max(decompressedSize, 10) - 10)
					{
						while (destPos < decompressedSize)
						{
							if (control == 1)
								sourcePos += 4;
							control >>= 1;
							dest[destPos++] = data[sourcePos++];
						}
						break;
					}
					else
					{
						dest[destPos++] = data[sourcePos++];
						control >>= 1;
						int end = Math.Max(destPos - 2, 0);
						UpdateHashtable(dest, nextHashed, end);
						nextHashed = Math.Max(nextHashed, end);
					}
				}
			}

			return dest;
		}

		private static void WriteHeader(byte[] dest, int destLen, int srcLen, int level, int headerlen, bool compressed)
		{
			byte flags;
			if (compressed)
				flags = (byte)(0x01 | (level << 2) | 0x40);
			else
				flags = (byte)((level << 2) | 0x40);

			if (headerlen == 3)
			{
				// short header
				dest[0] = flags;
				dest[1] = (byte)destLen;
				dest[2] = (byte)srcLen;
			}
			else if (headerlen == 9)
			{
				// long header
				dest[0] = (byte)(flags | 0x02);
				WriteI32(dest, 1, destLen);
				WriteI32(dest, 5, srcLen);
			}
			else
			{
				throw new NotSupportedException();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WriteU16(byte[] outArr, int outOff, int value)
		{
			outArr[outOff + 0] = unchecked((byte)(value >> 0));
			outArr[outOff + 1] = unchecked((byte)(value >> 8));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Write24(byte[] outArr, int outOff, int value)
		{
			outArr[outOff + 0] = unchecked((byte)(value >> 00));
			outArr[outOff + 1] = unchecked((byte)(value >> 08));
			outArr[outOff + 2] = unchecked((byte)(value >> 16));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WriteI32(byte[] outArr, int outOff, int value)
		{
			outArr[outOff + 0] = unchecked((byte)(value >> 00));
			outArr[outOff + 1] = unchecked((byte)(value >> 08));
			outArr[outOff + 2] = unchecked((byte)(value >> 16));
			outArr[outOff + 3] = unchecked((byte)(value >> 24));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WriteU32(byte[] outArr, int outOff, uint value)
		{
			outArr[outOff + 0] = unchecked((byte)(value >> 00));
			outArr[outOff + 1] = unchecked((byte)(value >> 08));
			outArr[outOff + 2] = unchecked((byte)(value >> 16));
			outArr[outOff + 3] = unchecked((byte)(value >> 24));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static ushort ReadU16(byte[] intArr, int inOff)
			=> unchecked((ushort)(intArr[inOff] | (intArr[inOff + 1] << 8)));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int Read24(byte[] intArr, int inOff)
			=> unchecked(intArr[inOff] | (intArr[inOff + 1] << 8) | (intArr[inOff + 2] << 16));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int ReadI32(byte[] intArr, int inOff)
			=> unchecked(intArr[inOff] | (intArr[inOff + 1] << 8) | (intArr[inOff + 2] << 16) | (intArr[inOff + 3] << 24));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint ReadU32(byte[] intArr, int inOff)
			=> unchecked ((uint)(intArr[inOff] | (intArr[inOff + 1] << 8) | (intArr[inOff + 2] << 16) | (intArr[inOff + 3] << 24)));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int Hash(int value) => ((value >> 12) ^ value) & 0xfff;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Is6Same(byte[] arr, int i)
		{
			return arr[i + 0] == arr[i + 1]
				&& arr[i + 1] == arr[i + 2]
				&& arr[i + 2] == arr[i + 3]
				&& arr[i + 3] == arr[i + 4]
				&& arr[i + 4] == arr[i + 5];
		}

		/// <summary>Copy <code>[start; start + length)</code> bytes from `data` to the end of `data`</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int CopyBufferBytes(byte[] data, int destPos, int start, int length)
		{
			data[destPos + 0] = data[start + 0];
			data[destPos + 1] = data[start + 1];
			data[destPos + 2] = data[start + 2];
			for (int i = 3; i < length; i++)
				data[destPos + i] = data[start + i];
			return destPos + length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void UpdateHashtable(byte[] dest, int start, int end)
		{
			if (start >= end)
				return;
			int next = Read24(dest, start);
			hashtable[Hash(next)] = start;
			for (int i = start + 1; i < end; i++)
			{
				next = (next >> 8) | (dest[i + 2] << 16);
				hashtable[Hash(next)] = i;
			}
		}
	}

	public struct ArrLim
	{
		public readonly byte[] Data;
		public readonly int Length;

		public ArrLim(byte[] data, int length)
		{
			Data = data;
			Length = length;
		}

		public byte[] ToArr()
		{
			var res = new byte[Length];
			Array.Copy(Data, res, Length);
			return res;
		}
	}
}
