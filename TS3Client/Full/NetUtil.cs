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
	using System.Net;
	using System.Runtime.CompilerServices;

	internal static class NetUtil
	{
		// Common Network to Host and Host to Network swaps
		public static uint H2N(uint value) => unchecked((uint)IPAddress.HostToNetworkOrder((int)value));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort N2Hushort(byte[] intArr, int inOff)
		{
			return unchecked((ushort)((intArr[inOff] << 8) | intArr[inOff + 1]));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int N2Hint(byte[] intArr, int inOff)
		{
			return unchecked((intArr[inOff] << 24) | (intArr[inOff + 1] << 16) | (intArr[inOff + 2] << 8) | intArr[inOff + 3]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void H2N(ushort value, byte[] outArr, int outOff)
		{
			outArr[outOff + 0] = unchecked((byte)(value >> 8));
			outArr[outOff + 1] = unchecked((byte)(value >> 0));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void H2N(uint value, byte[] outArr, int outOff)
		{
			outArr[outOff + 0] = unchecked((byte)(value >> 24));
			outArr[outOff + 1] = unchecked((byte)(value >> 16));
			outArr[outOff + 2] = unchecked((byte)(value >> 08));
			outArr[outOff + 3] = unchecked((byte)(value >> 00));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void H2N(ulong value, byte[] outArr, int outOff)
		{
			outArr[outOff + 0] = unchecked((byte)(value >> 56));
			outArr[outOff + 1] = unchecked((byte)(value >> 48));
			outArr[outOff + 2] = unchecked((byte)(value >> 40));
			outArr[outOff + 3] = unchecked((byte)(value >> 32));
			outArr[outOff + 4] = unchecked((byte)(value >> 24));
			outArr[outOff + 5] = unchecked((byte)(value >> 16));
			outArr[outOff + 6] = unchecked((byte)(value >> 08));
			outArr[outOff + 7] = unchecked((byte)(value >> 00));
		}
	}
}
