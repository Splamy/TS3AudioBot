// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Linq;

namespace TSLib.Helper
{
	internal static class DebugUtil
	{
		public static string DebugToHex(byte[] bytes) => bytes is null ? "<null>" : DebugToHex(bytes.AsSpan());

		public static string DebugToHex(ReadOnlySpan<byte> bytes)
		{
			char[] c = new char[bytes.Length * 3];
			for (int bx = 0, cx = 0; bx < bytes.Length; ++bx, ++cx)
			{
				byte b = (byte)(bytes[bx] >> 4);
				c[cx] = (char)(b > 9 ? b - 10 + 'A' : b + '0');

				b = (byte)(bytes[bx] & 0x0F);
				c[++cx] = (char)(b > 9 ? b - 10 + 'A' : b + '0');
				c[++cx] = ' ';
			}
			return new string(c);
		}

		public static byte[] DebugFromHex(string hex)
			=> hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(x => Convert.ToByte(x, 16)).ToArray();
	}
}
