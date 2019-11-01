// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.IO;
using System.Runtime.InteropServices;

namespace TS3AudioBot.ResourceFactories.AudioTags
{
	internal static class BinaryReaderBigEndianExtensions
	{
		public static short ReadInt16Be(this BinaryReader br)
		{
			return BitConverterBigEndian.ToInt16(br.ReadBytes(2));
		}
		public static int ReadInt24Be(this BinaryReader br)
		{
			return BitConverterBigEndian.ToInt24(br.ReadBytes(3));
		}
		public static int ReadInt32Be(this BinaryReader br)
		{
			return BitConverterBigEndian.ToInt32(br.ReadBytes(4));
		}
		public static long ReadInt64Be(this BinaryReader br)
		{
			return BitConverterBigEndian.ToInt64(br.ReadBytes(8));
		}

		public static ushort ReadUInt16Be(this BinaryReader br)
		{
			return BitConverterBigEndian.ToUInt16(br.ReadBytes(2));
		}
		public static uint ReadUInt32Be(this BinaryReader br)
		{
			return BitConverterBigEndian.ToUInt32(br.ReadBytes(4));
		}
		public static ulong ReadUInt64Be(this BinaryReader br)
		{
			return BitConverterBigEndian.ToUInt64(br.ReadBytes(8));
		}

		public static int ReadId3Int(this BinaryReader br)
		{
			int num = 0;
			num |= br.ReadByte() << (3 * 7);
			num |= br.ReadByte() << (2 * 7);
			num |= br.ReadByte() << (1 * 7);
			num |= br.ReadByte() << (0 * 7);
			return num;
		}
	}

	internal static class BitConverterBigEndian
	{
		private const int BitsInByte = 8;

		public static short ToInt16(byte[] bytes)
		{
			return (short)(
				(bytes[0] << 1 * BitsInByte) |
				(bytes[1] << 0 * BitsInByte));
		}
		public static int ToInt24(byte[] bytes)
		{
			return
				(bytes[0] << 2 * BitsInByte) |
				(bytes[1] << 1 * BitsInByte) |
				(bytes[2] << 0 * BitsInByte);
		}
		public static int ToInt32(byte[] bytes)
		{
			return
				(bytes[0] << 3 * BitsInByte) |
				(bytes[1] << 2 * BitsInByte) |
				(bytes[2] << 1 * BitsInByte) |
				(bytes[3] << 0 * BitsInByte);
		}
		public static long ToInt64(byte[] bytes)
		{
			ReinterpretInt ri;
			ri.value = 0;
			ri.HDW = // High double word
				(bytes[0] << 3 * BitsInByte) |
				(bytes[1] << 2 * BitsInByte) |
				(bytes[2] << 1 * BitsInByte) |
				(bytes[3] << 0 * BitsInByte);
			ri.LDW = // Low double word
				(bytes[4] << 3 * BitsInByte) |
				(bytes[5] << 2 * BitsInByte) |
				(bytes[6] << 1 * BitsInByte) |
				(bytes[7] << 0 * BitsInByte);
			return ri.value;
		}

		public static ushort ToUInt16(byte[] bytes)
		{
			return (ushort)(
				(bytes[0] << 1 * BitsInByte) |
				(bytes[1] << 0 * BitsInByte));
		}
		public static uint ToUInt32(byte[] bytes)
		{
			return
				((uint)bytes[0] << 3 * BitsInByte) |
				((uint)bytes[1] << 2 * BitsInByte) |
				((uint)bytes[2] << 1 * BitsInByte) |
				((uint)bytes[3] << 0 * BitsInByte);
		}
		public static ulong ToUInt64(byte[] bytes)
		{
			ReinterpretInt ri;
			ri.value = 0;
			ri.HDW = // High double word
				(bytes[0] << 3 * BitsInByte) |
				(bytes[1] << 2 * BitsInByte) |
				(bytes[2] << 1 * BitsInByte) |
				(bytes[3] << 0 * BitsInByte);
			ri.LDW = // Low double word
				(bytes[4] << 3 * BitsInByte) |
				(bytes[5] << 2 * BitsInByte) |
				(bytes[6] << 1 * BitsInByte) |
				(bytes[7] << 0 * BitsInByte);
			return unchecked((ulong)ri.value);
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct ReinterpretInt
		{
			[FieldOffset(0)]
			public int LDW;
			[FieldOffset(4)]
			public int HDW;
			[FieldOffset(0)]
			public long value;
		}
	}
}
