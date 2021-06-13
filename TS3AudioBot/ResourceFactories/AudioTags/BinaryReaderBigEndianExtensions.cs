// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Buffers.Binary;
using System.IO;

namespace TS3AudioBot.ResourceFactories.AudioTags
{
	internal static class BinaryReaderBigEndianExtensions
	{
		public static short ReadInt16Be(this BinaryReader br)
		{
			Span<byte> buf = stackalloc byte[2];
			br.Read(buf);
			return BinaryPrimitives.ReadInt16BigEndian(buf);
		}
		public static int ReadInt24Be(this BinaryReader br)
		{
			Span<byte> buf = stackalloc byte[4];
			br.Read(buf[1..]); // Reading [0, X, X, X] will read like 24bit int, since buf[0] is the MSB
			return BinaryPrimitives.ReadInt32BigEndian(buf);
		}
		public static int ReadInt32Be(this BinaryReader br)
		{
			Span<byte> buf = stackalloc byte[4];
			br.Read(buf);
			return BinaryPrimitives.ReadInt32BigEndian(buf);
		}
		public static long ReadInt64Be(this BinaryReader br)
		{
			Span<byte> buf = stackalloc byte[8];
			br.Read(buf);
			return BinaryPrimitives.ReadInt64BigEndian(buf);
		}

		public static ushort ReadUInt16Be(this BinaryReader br)
		{
			Span<byte> buf = stackalloc byte[2];
			br.Read(buf);
			return BinaryPrimitives.ReadUInt16BigEndian(buf);
		}
		public static uint ReadUInt32Be(this BinaryReader br)
		{
			Span<byte> buf = stackalloc byte[4];
			br.Read(buf);
			return BinaryPrimitives.ReadUInt32BigEndian(buf);
		}
		public static ulong ReadUInt64Be(this BinaryReader br)
		{
			Span<byte> buf = stackalloc byte[8];
			br.Read(buf);
			return BinaryPrimitives.ReadUInt64BigEndian(buf);
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

	internal static class BinaryPrimitivesExt
	{
		private const int BitsInByte = 8;

		public static int ReadInt24BigEndian(ReadOnlySpan<byte> bytes)
		{
			return
				(bytes[0] << 2 * BitsInByte) |
				(bytes[1] << 1 * BitsInByte) |
				(bytes[2] << 0 * BitsInByte);
		}
	}
}
